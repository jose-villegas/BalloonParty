#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Cheats;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Palette;
using BalloonParty.Editor.Maps;
using BalloonParty.Game;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Game.Score;
using BalloonParty.Game.Upgrades;
using BalloonParty.Item.Snipe;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Rendering;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using BalloonParty.Solver;
using BalloonParty.Thrower;
using BalloonParty.UI;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using VContainer;
using Object = UnityEngine.Object;

namespace BalloonParty.Editor.Staging
{
    /// <summary>
    ///     Puts a still scene together in Play mode, for judging a look (a pop, a balloon at night) without playing
    ///     for it: holds the world still, clears slots, places balloons, sets the time of day, pops a balloon or
    ///     freezes its pop at a chosen moment, and renders the camera to a PNG. Driven from outside through the Unity
    ///     CLI by <c>Tools/stage/stage.py</c>, one call per eval, so every method returns at once with a line saying
    ///     what it did; nothing here waits. Public, unlike the rest of the Editor assembly, so an eval script can
    ///     call it without reflection.
    /// </summary>
    public static class SceneStage
    {
        private const int DefaultSeed = 4242;
        private const int ScenarioSearchLevels = 500;
        private const string ProjectilePrefabPath = "Assets/Prefabs/Projectile/Projectile.prefab";

        private static readonly Regex RelativeInclude = new("#include\\s+\"((?:\\.\\./|\\./)[^\"]+)\"");

        private static readonly List<GameObject> Effects = new();
        private static readonly List<Object> Scratch = new();
        private static readonly List<Renderer> Hidden = new();
        private static readonly List<(ProjectileModel Model, GameObject Look)> Balls = new();

        private static float _advanceUntil = -1f;
        private static Transform _flyingBall;
        private static Vector3 _flightFrom;
        private static Vector3 _flightTo;
        private static float _flightStart;
        private static float _flightSeconds;

        /// <summary>
        ///     Where things stand: <c>off</c> (no Play mode), <c>launch</c>, <c>loading</c>, or
        ///     <c>game L&lt;level&gt; held|running|advancing angle &lt;degrees&gt;</c>.
        /// </summary>
        public static string Status()
        {
            if (!EditorApplication.isPlaying)
            {
                return "off";
            }

            if (Navigation.Current.Value == NavigationState.Launch)
            {
                return "launch";
            }

            var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
            if (scope == null || scope.Container == null)
            {
                return "loading";
            }

            var state = _advanceUntil >= 0f ? "advancing" : IsHeld() ? "held" : "running";
            return $"game L{Resolve<ILevelProgress>().Level.Value} {state} angle " +
                   $"{Resolve<TimeOfDayClock>().CurrentAngleDegrees.ToString("0", CultureInfo.InvariantCulture)}";
        }

        /// <summary>
        ///     Gets to a held game in one step once Play mode is up: straight past the launch screen (its Play button's own
        ///     actions, the button hidden and the launcher handed over to the game scene, without the cloud scroll), or a fresh run after a game over,
        ///     with game time held from the same frame, the clock and the level-up stopped, so nothing plays out on its
        ///     own. Enters Play mode first if it's off; call it until it answers <c>ready</c>.
        /// </summary>
        public static string Begin()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
                return "entering Play mode";
            }

            // In the background the Editor stops the player loop unless told otherwise.
            Application.runInBackground = true;
            if (Object.FindAnyObjectByType<GameLifetimeScope>() == null)
            {
                return "loading";
            }

            CheatState.TimeOfDaySpeedScale = 0f;
            CheatState.BlockLevelUp = true;
            CheatState.EmptyBoard = true;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Hold();
            switch (Navigation.Current.Value)
            {
                case NavigationState.Launch:
                    PressLaunchPlay();
                    Navigation.TransitionTo(NavigationState.Game);
                    break;
                case NavigationState.GameOver:
                    Resolve<RunController>().RestartRun();
                    break;
                default:
                    // A run already under way: its balloons go, so the stage starts as bare as a fresh one.
                    Resolve<BalloonControllerRegistry>().ClearAll(false);
                    break;
            }

            return Navigation.Current.Value == NavigationState.Game ? "ready" : $"waiting ({Navigation.Current.Value})";
        }

        /// <summary>Clears the frozen pops, gives the game back its own clock, level-ups and balloons, and leaves Play mode.</summary>
        public static string End()
        {
            if (EditorApplication.isPlaying)
            {
                ClearEffects();
                Release();
                EditorApplication.isPlaying = false;
            }

            ResetCheats();
            return "off";
        }

        /// <summary>Stops game time (time scale 0) until <see cref="Release" /> or <see cref="Advance" />.</summary>
        public static string Hold()
        {
            Resolve<ITimeScaleClaims>().ClaimExclusive(TimeScaleSource.Capture, 0f);
            return "held";
        }

        /// <summary>Lets game time run again.</summary>
        public static string Release()
        {
            Resolve<ITimeScaleClaims>().ReleaseExclusive(TimeScaleSource.Capture);
            return "running";
        }

        /// <summary>
        ///     Lets game time run for <paramref name="seconds" /> and holds it again, so a pop plays out or a balloon
        ///     settles. Returns at once; <see cref="Status" /> says <c>advancing</c> until it's done.
        /// </summary>
        public static string Advance(float seconds)
        {
            Release();
            _advanceUntil = Time.time + seconds;
            EditorApplication.update -= AdvanceTick;
            EditorApplication.update += AdvanceTick;
            return $"advancing {seconds:0.##} s";
        }

        /// <summary>
        ///     Deals a fresh board: the run restarted at <paramref name="level" /> (0 = the current one), or at the first
        ///     level from there in <paramref name="scenario" /> (sky, meadow, water, or paper: the next bonus level;
        ///     empty = any), on run seed <paramref name="seed" /> (0 = keep the current one), which also seeds Unity's
        ///     random, so the same call deals the same board. The board spawns over the next moments of game time
        ///     (<see cref="Advance" />).
        /// </summary>
        public static string Deal(string scenario, int level, int seed)
        {
            ClearEffects();
            var runSeed = seed != 0 ? seed : Resolve<IRunSeed>().Value;
            var start = level > 0 ? level : Resolve<ILevelProgress>().Level.Value;
            if (!string.IsNullOrEmpty(scenario))
            {
                var kind = ParseScenario(scenario);
                var pacing = Resolve<ILevelPacingConfiguration>();

                // A stretch's start is where the scenario begins, so search from the level before.
                start = JumpToScenarioCheat.NextStretchStart(l => pacing.BackdropForLevel(l, runSeed), Math.Max(1, start - 1),
                    kind, ScenarioSearchLevels);
                if (start < 1)
                {
                    return $"error: no {kind} stretch in the {ScenarioSearchLevels} levels from {level}";
                }
            }

            CheatState.StartLevel = start;
            CheatState.ForcedRunSeed = runSeed;
            UnityEngine.Random.InitState(runSeed);
            Resolve<RunController>().RestartRun();
            return $"dealt level {start} ({Resolve<ILevelPacingConfiguration>().BackdropForLevel(start, runSeed)}), seed {runSeed}";
        }

        /// <summary>
        ///     Takes kinds of things off the board at once, with no pop: <paramref name="what" /> is a comma list of
        ///     <c>balloons</c>, <c>clouds</c>, <c>bushes</c>, <c>enemies</c> (deflectors, absorbers and gatekeepers),
        ///     <c>obstacles</c> (all of those but the balloons), or a <see cref="GridActorType" /> by name.
        /// </summary>
        public static string Strip(string what)
        {
            var spawner = Resolve<StaticActorSpawner>();
            var stripped = new List<string>();
            foreach (var part in what.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var name = part.Trim().ToLowerInvariant();
                switch (name)
                {
                    case "balloons":
                        Resolve<BalloonControllerRegistry>().ClearAll(false);
                        break;
                    case "obstacles":
                        spawner.ClearStaticActors();
                        break;
                    default:
                        foreach (var type in ActorTypes(name))
                        {
                            spawner.ClearStaticActors(type);
                        }

                        break;
                }

                stripped.Add(name);
            }

            return $"stripped {string.Join(", ", stripped)}";
        }

        /// <summary>Sets the time of day (the light's angle: 45 dawn, 135 noon, 225 dusk, 315 night) and stops the clock.</summary>
        public static string SetTime(float degrees)
        {
            CheatState.TimeOfDaySpeedScale = 0f;
            Resolve<TimeOfDayClock>().SetAngleDegrees(degrees);
            return $"angle {degrees:0}";
        }

        /// <summary>Every balloon and obstacle on the board, one per line: type, slot, world position.</summary>
        public static string List()
        {
            var lines = new StringBuilder();
            foreach (var view in Object.FindObjectsByType<BalloonView>())
            {
                if (view.Model == null)
                {
                    continue;
                }

                var slot = view.Model.SlotIndex.Value;
                var position = view.transform.position;
                lines.AppendLine(FormattableString.Invariant(
                    $"{view.Model.TypeName} {slot.x},{slot.y} {position.x:0.00},{position.y:0.00}"));
            }

            var grid = Resolve<SlotGrid>();
            foreach (var slot in grid.AllOccupiedSlots())
            {
                if (grid.At(slot) is IGridActorModel actor)
                {
                    var position = grid.IndexToWorldPosition(slot);
                    lines.AppendLine(FormattableString.Invariant(
                        $"{actor.ActorType} {slot.x},{slot.y} {position.x:0.00},{position.y:0.00}"));
                }
            }

            lines.Append($"grid {grid.Columns}x{grid.Rows}");
            return lines.ToString();
        }

        /// <summary>
        ///     Pops every balloon in the slot rectangle from (<paramref name="fromCol" />, <paramref name="fromRow" />)
        ///     to (<paramref name="toCol" />, <paramref name="toRow" />), inclusive, the way the Remove Balloons cheat
        ///     does. Their pops need game time to play out (<see cref="Advance" />).
        /// </summary>
        public static string Clear(int fromCol, int fromRow, int toCol, int toRow)
        {
            var grid = Resolve<SlotGrid>();
            var popped = 0;
            for (var col = Math.Max(0, Math.Min(fromCol, toCol)); col <= Math.Min(grid.Columns - 1, Math.Max(fromCol, toCol)); col++)
            {
                for (var row = Math.Max(0, Math.Min(fromRow, toRow)); row <= Math.Min(grid.Rows - 1, Math.Max(fromRow, toRow)); row++)
                {
                    popped += PopSlot(grid, new Vector2Int(col, row)) ? 1 : 0;
                }
            }

            return $"popped {popped}";
        }

        /// <summary>Pops every balloon within <paramref name="radius" /> world units of (x, y).</summary>
        public static string ClearAround(float x, float y, float radius)
        {
            var grid = Resolve<SlotGrid>();
            var centre = new Vector2(x, y);
            var popped = 0;
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    if (Vector2.Distance(grid.IndexToWorldPosition(slot), centre) <= radius)
                    {
                        popped += PopSlot(grid, slot) ? 1 : 0;
                    }
                }
            }

            return $"popped {popped}";
        }

        /// <summary>
        ///     Puts a balloon of <paramref name="type" /> (a <see cref="BalloonType" /> name) in a slot, through the
        ///     Spawn Balloon cheat, optionally holding <paramref name="item" /> (an <see cref="ItemType" /> name). A taken
        ///     slot is an error unless <paramref name="replace" />, which pops what's there and spawns into the slot in
        ///     the same frame: given any game time first, a balloon below would float up into it.
        /// </summary>
        public static string Place(string type, int col, int row, string item = "None", bool replace = false)
        {
            var grid = Resolve<SlotGrid>();
            var slot = new Vector2Int(col, row);
            if (col < 0 || row < 0 || col >= grid.Columns || row >= grid.Rows)
            {
                return $"error: {col},{row} is off the {grid.Columns}x{grid.Rows} grid";
            }

            if (!grid.IsEmpty(col, row) && replace)
            {
                PopSlot(grid, slot);
            }

            if (!grid.IsEmpty(col, row))
            {
                return $"error: {col},{row} is taken";
            }

            var spawner = FindCheat<SpawnBalloonCheat>();
            var balloonType = (BalloonType)Enum.Parse(typeof(BalloonType), type, true);
            var entry = spawner.FindEntry(balloonType);
            if (entry == null)
            {
                return $"error: no catalogue entry for {balloonType}";
            }

            spawner.SpawnAt(slot, entry, (ItemType)Enum.Parse(typeof(ItemType), item, true));
            Settle(grid.ViewAt(slot) as BalloonView);
            return FormattableString.Invariant($"placed {balloonType} at {col},{row} ({grid.IndexToWorldPosition(slot).x:0.00},{grid.IndexToWorldPosition(slot).y:0.00})");
        }

        /// <summary>
        ///     Pops a balloon for real, its own pop effect and all: <paramref name="target" /> is a balloon type (the first
        ///     on the board) or a slot, "col,row".
        /// </summary>
        public static string Pop(string target)
        {
            var view = FindView(target);
            if (view == null)
            {
                return $"error: no balloon at {target}";
            }

            // Read before the pop: a balloon that despawns at once (a reward balloon) has no model after it.
            var typeName = view.Model.TypeName;
            var slot = view.Model.SlotIndex.Value;
            PopSlot(Resolve<SlotGrid>(), slot);
            return $"popped {typeName} at {slot.x},{slot.y}";
        }

        /// <summary>
        ///     Shows a balloon's pop effect stopped <paramref name="seconds" /> in, at the balloon, from a fixed random
        ///     seed so two runs throw the same particles; the effect stays until <see cref="ClearEffects" />.
        ///     <paramref name="options" /> is <c>key=value</c> pairs split by <c>;</c>: <c>hide=1</c> hides the
        ///     balloon (as popped), <c>dx</c>/<c>dy</c> move the effect off it in world units, <c>seed</c>,
        ///     <c>texture</c> (a PNG path) swaps the sheet the shards draw, <c>startFrame</c> (0..1 of the sheet) their
        ///     random first frame, <c>shader</c> a shader by name, <c>shaderFile</c> a .shader path compiled on the spot
        ///     (so one not yet merged can be tried; its relative includes resolve against its own folder), and any key
        ///     starting with <c>_</c> sets that float on the material. Swaps go on copies, so no asset changes.
        /// </summary>
        public static string FreezePop(string target, float seconds, string options = "")
        {
            var view = FindView(target);
            if (view == null)
            {
                return $"error: no balloon at {target}";
            }

            var settings = ParseOptions(options);
            var prefab = PopPrefab(view.Model, out var tint);
            if (prefab == null)
            {
                return $"error: {view.Model.TypeName} has no pop effect";
            }

            var offset = new Vector3(Float(settings, "dx", 0f), Float(settings, "dy", 0f), 0f);
            var effect = Object.Instantiate(prefab, view.transform.position + offset, prefab.transform.rotation);
            effect.gameObject.name = $"Stage {prefab.name}";
            Effects.Add(effect.gameObject);

            // Not pooled, so tracked here: the effects map renders only while its layer has something alive.
            LayerPresence.Track(effect.gameObject);
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var material = MaterialSwap(settings);
            foreach (var system in effect.GetComponentsInChildren<ParticleSystem>())
            {
                Prepare(system, settings, system == effect ? tint : null, material);
            }

            if (Float(settings, "hide", 0f) > 0.5f)
            {
                HideBalloon(view);
            }

            effect.Simulate(seconds, true, true);
            return $"froze {prefab.name} {seconds:0.###} s in";
        }

        /// <summary>Removes every effect <see cref="FreezePop" /> left and shows the balloons it hid.</summary>
        public static string ClearEffects()
        {
            var removed = Effects.Count;
            foreach (var effect in Effects)
            {
                if (effect != null)
                {
                    Object.DestroyImmediate(effect);
                }
            }

            foreach (var renderer in Hidden)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }

            foreach (var scratch in Scratch)
            {
                if (scratch != null)
                {
                    Object.DestroyImmediate(scratch);
                }
            }

            EditorApplication.update -= FlyTick;
            _flyingBall = null;
            if (Balls.Count > 0)
            {
                var registry = Resolve<IProjectileRegistryWriter>();
                foreach (var (model, look) in Balls)
                {
                    registry.Retire(model);
                    if (look != null)
                    {
                        Object.DestroyImmediate(look);
                    }
                }

                removed += Balls.Count;
            }

            Effects.Clear();
            Hidden.Clear();
            Scratch.Clear();
            Balls.Clear();
            return $"removed {removed}";
        }

        /// <summary>
        ///     Sets up a level-up for the next shot: turns every balloon on the board the level's first colour, fills
        ///     every other colour's bar, leaves that one a point short, and lets the level-up through (<see cref="Begin" />
        ///     blocks it). The next pop then completes the level, so the level-up cinematic plays out for real, zoom
        ///     and all. Fire with the Fire Best Shot cheat and <see cref="Release" />.
        /// </summary>
        public static string LevelUpReady()
        {
            var colors = Resolve<IActiveLevelParameters>().Current.AllowedColors;
            var lastColor = colors[0];
            foreach (var view in Object.FindObjectsByType<BalloonView>())
            {
                if (view.Model is BalloonModel balloon)
                {
                    balloon.Color.Value = lastColor;
                }
            }

            CheatState.BlockLevelUp = false;
            var progress = Resolve<ILevelProgress>();
            var required = Resolve<ILevelThresholds>().PointsRequiredForLevel(progress.Level.Value);
            var palette = Resolve<IGamePalette>();
            foreach (var color in colors)
            {
                ScoreCheatHelper.FillColor(palette.GetEntry(color), color == lastColor ? required - 1 : required,
                    progress, Resolve<IHitDispatcher>(), Resolve<ColorStreakTracker>());
            }

            return $"level up on the next {lastColor} pop";
        }

        /// <summary>
        ///     Puts a ball in flight at (<paramref name="x" />, <paramref name="y" />), still, so a capture shows the
        ///     effects cleared around it (PLAN-EffectsLayerClearing). With <paramref name="fromPoint" /> ("x,y") and
        ///     <paramref name="seconds" />, it flies there in a straight line over that much game time first, then holds,
        ///     leaving the clearing it opened behind it to close up. It's the ball's look only (its sprite and trail, no
        ///     flight or hits) on the registry's in-flight list, which is what the clearing follows, carrying
        ///     <paramref name="shields" />, which size its clearing. <see cref="ClearEffects" /> takes it away.
        /// </summary>
        public static string Ball(float x, float y, string fromPoint = "", float seconds = 0f, int shields = 1)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
            if (prefab == null)
            {
                return $"error: no prefab at {ProjectilePrefabPath}";
            }

            var to = new Vector3(x, y, 0f);
            var start = string.IsNullOrEmpty(fromPoint) ? to : WorldPoint(fromPoint.Replace(',', ';'));
            var look = BallLook(prefab, start);
            var model = new ProjectileModel();
            model.ShieldsRemaining.Value = shields;
            var registry = Resolve<IProjectileRegistryWriter>();
            registry.Load(model, look.transform);
            registry.Launch(model);
            Balls.Add((model, look));

            if (seconds <= 0f || start == to)
            {
                return $"ball at {x:0.##},{y:0.##}";
            }

            _flyingBall = look.transform;
            _flightFrom = start;
            _flightTo = to;
            _flightStart = Time.time;
            _flightSeconds = seconds;
            EditorApplication.update -= FlyTick;
            EditorApplication.update += FlyTick;
            Advance(seconds);
            return $"ball flying to {x:0.##},{y:0.##} over {seconds:0.##} s";
        }

        /// <summary>
        ///     Fires the ball loaded in the thrower for real, at <paramref name="degrees" /> (0 = right, 90 = straight
        ///     up), through <c>ThrowerController.FireAt</c> as the Fire Best Shot cheat does: it flies, bounces, hits and
        ///     scores as a player's shot would, once game time runs (<see cref="Advance" />). With
        ///     <paramref name="lance" /> it is armed as a Snipe lance first (<c>ISnipeArmer</c>, the Sharpshooter
        ///     card's path), so it plows the toughs in its way and shatters them at the next wall. An error if the
        ///     thrower is not up, holds no ball, or would not fire (a level-up, or the most balls already in flight).
        /// </summary>
        public static string Fire(float degrees, bool lance = false)
        {
            var scope = Object.FindAnyObjectByType<ThrowerLifetimeScope>();
            if (scope == null)
            {
                return "error: no thrower in the scene";
            }

            var registry = scope.Container.Resolve<IProjectileRegistry>();
            if (registry.Loaded is not IWriteableProjectileModel loaded)
            {
                return "error: no ball loaded in the thrower";
            }

            if (lance)
            {
                scope.Container.Resolve<ISnipeArmer>().Arm(loaded);
            }

            var from = Object.FindAnyObjectByType<ThrowerView>().SpawnPointPosition;
            var inFlight = registry.InFlightCount;
            var thrower = scope.Container.Resolve<ThrowerController>();
            thrower.AimHold = null;
            thrower.FireAt(ShotBoardGather.DirectionFromDegrees(degrees));
            if (registry.InFlightCount <= inFlight)
            {
                return "error: the thrower would not fire";
            }

            return string.Format(CultureInfo.InvariantCulture, "fired {0} at {1:0.##} degrees from {2:0.##},{3:0.##}",
                lance ? "a lance" : "a ball", degrees, from.x, from.y);
        }

        /// <summary>
        ///     Cuts the cap off the mushroom in a slot, as a lance flying at <paramref name="degrees" /> (0 = right,
        ///     90 = straight up) would: through the cap's middle, or <paramref name="offset" /> world units to the
        ///     left of it, which takes a sliver off near the edge. It is the cut a lance makes in play: the board
        ///     takes the cap out of its bumpers (<see cref="IBumperField.Cut" />), so a ball flies through where it
        ///     stood, and the message the flight publishes is published, so the view, the sound and the counters
        ///     answer as they do in play. The cap grows back by itself on the board's beat, as in play, or at once
        ///     with <see cref="RegrowCap" />. An error when the slot holds no mushroom or its cap is already cut.
        /// </summary>
        public static string CutCap(int col, int row, float degrees, float offset = 0f)
        {
            var slot = new Vector2Int(col, row);
            var grid = Resolve<SlotGrid>();
            if (!grid.InBounds(slot) || grid.ViewAt(slot) is not MushroomView)
            {
                return $"error: no mushroom at {col},{row}";
            }

            var field = Resolve<IBumperField>();
            var index = -1;
            for (var i = 0; i < field.Bumpers.Count; i++)
            {
                if (field.SlotAt(i) == slot)
                {
                    index = i;
                }
            }

            if (index < 0)
            {
                return $"error: the cap at {col},{row} is already cut";
            }

            // The same record the flight makes of a cut, from a point a unit back along the lance's line.
            var cap = field.Bumpers[index];
            Vector2 way = ShotBoardGather.DirectionFromDegrees(degrees);
            var from = cap.Center + (new Vector2(-way.y, way.x) * offset) - way;
            var cut = new CapCut(cap, slot, from, way, Resolve<IMushroomSettings>().Radius);
            field.Cut(index);
            Publish(cut.ToMessage());
            return FormattableString.Invariant(
                $"cut the cap at {col},{row} along {degrees:0.##} degrees, {offset:0.##} to the left of its middle");
        }

        /// <summary>
        ///     Grows the cut cap in a slot back now, without waiting for the board's beat: the board puts it back
        ///     among its bumpers (<see cref="IMushroomRegrowth.Regrow" />) and the message is published, as
        ///     <c>MushroomRegrowController</c> does. An error when the slot holds no mushroom or its cap is whole.
        /// </summary>
        public static string RegrowCap(int col, int row)
        {
            var slot = new Vector2Int(col, row);
            var grid = Resolve<SlotGrid>();
            if (!grid.InBounds(slot) || grid.ViewAt(slot) is not MushroomView)
            {
                return $"error: no mushroom at {col},{row}";
            }

            var caps = Resolve<IMushroomRegrowth>();
            for (var i = 0; i < caps.CutCount; i++)
            {
                if (caps.CutSlotAt(i) != slot)
                {
                    continue;
                }

                caps.Regrow(i);
                Publish(new MushroomRegrownMessage(slot));
                return $"regrowing the cap at {col},{row}";
            }

            return $"error: the cap at {col},{row} is not cut";
        }

        /// <summary>
        ///     Holds the thrower's aim at <paramref name="degrees" /> (0 = right, 90 = straight up) and draws its aim
        ///     line, as a player holding the aim there would see it, without firing: the line as the game predicts
        ///     it, its bend off a mushroom included. <see cref="AimOff" /> or <see cref="Fire" /> lets go.
        /// </summary>
        public static string Aim(float degrees)
        {
            var scope = Object.FindAnyObjectByType<ThrowerLifetimeScope>();
            if (scope == null)
            {
                return "error: no thrower in the scene";
            }

            scope.Container.Resolve<ThrowerController>().AimHold = ShotBoardGather.DirectionFromDegrees(degrees);
            return FormattableString.Invariant($"aiming at {degrees:0.##} degrees");
        }

        /// <summary>Lets go of the aim <see cref="Aim" /> holds, and takes its line down.</summary>
        public static string AimOff()
        {
            var scope = Object.FindAnyObjectByType<ThrowerLifetimeScope>();
            if (scope != null)
            {
                scope.Container.Resolve<ThrowerController>().AimHold = null;
            }

            return "aim let go";
        }

        /// <summary>
        ///     Where to aim to be paid by the meadow's mushrooms on the board as it stands: the shot solver flies
        ///     every angle the thrower can aim at, and the answer is the middle of the widest run of angles whose
        ///     flight is armed as the lance by the walls of caps (three hits, wall to wall), for
        ///     <see cref="Fire" />. A flight need not touch every cap, nor every wall: the rule counts crossings
        ///     between walls. It also says how many angles arm, how many whole caps stand in how many walls (a cap
        ///     a lance cut is not counted until it has grown back), and where the cannon stands: the layouts were
        ///     baked for the cannon at rest, so ask once it has entered (<see cref="Advance" /> a second). An error
        ///     with no thrower, no whole cap, no angle that arms, or a ball loaded as a lance: a lance bounces off
        ///     no mushroom, it cuts their caps, so no chain is counted for it.
        /// </summary>
        public static string Chain()
        {
            var scope = Object.FindAnyObjectByType<ThrowerLifetimeScope>();
            var thrower = Object.FindAnyObjectByType<ThrowerView>();
            if (scope == null || thrower == null)
            {
                return "error: no thrower in the scene";
            }

            var container = scope.Container;
            var field = container.Resolve<IBumperField>();

            // The whole caps only: a cut cap is no bumper, and no chain is flown off it.
            var mushrooms = field.Bumpers.Count;
            var cutCaps = container.Resolve<IMushroomRegrowth>().CutCount;
            if (mushrooms == 0)
            {
                return "error: no whole mushroom cap on the board";
            }

            var config = container.Resolve<IProjectileFlightConfig>();
            var itemConfig = container.Resolve<IItemConfiguration>();
            var context = ShotBoardGather.Gather(
                container.Resolve<SlotGrid>(), config, container.Resolve<IWeaponStats>().StartingShields,
                container.Resolve<ISlotGridConfig>(), container.Resolve<IBalloonsConfiguration>(), itemConfig, thrower,
                container.Resolve<ThrowerSettings>(), container.Resolve<IGamePalette>(),
                container.Resolve<IActiveLevelParameters>(), container.Resolve<IRunConfig>(), 0f,
                container.Resolve<BalloonBalancer>().SecondsUntilNextPulse,
                container.Resolve<IProjectileRegistry>().Loaded,
                ShotBumperLayer.Gather(
                    field, container.Resolve<IBumperBounceRule>(), container.Resolve<IMushroomSettings>(), itemConfig));
            // The solver flies a shot only while something is left to pop, and a stage starts with nothing on it:
            // an empty board gets one target far outside the walls, and none of the layers that follow balloons.
            var board = context.Board;
            var emptyBoard = board.Count == 0;
            if (emptyBoard)
            {
                board = new[] { ShotBalloonSnapshot.ForToughTarget(new Vector2(1000f, 1000f), 0.1f, 0, int.MaxValue) };
            }

            if (context.LoadedSeed.IsPiercing)
            {
                return "error: the loaded ball is a lance already, and a lance cuts the caps and counts no chain: " +
                    "fire it away first";
            }

            var workingSet = new ShotBalloonState[board.Count];

            // An aim that is not on the thrower's grid cannot be fired, so only the grid is flown.
            const int continuousSamples = 1024;
            var samples = AimAngleGrid.ResolveSweepSampleCount(
                config.AimAngleMinDegrees, config.AimAngleMaxDegrees, config.AimAngleStepDegrees, continuousSamples);
            var flying = 0;
            var run = 0;
            var widestRun = 0;
            var widestEnd = -1;
            for (var i = 0; i < samples; i++)
            {
                var angle = AimAngleGrid.ResolveSweepAngle(
                    i, config.AimAngleMinDegrees, config.AimAngleMaxDegrees, config.AimAngleStepDegrees,
                    continuousSamples);
                var tally = ShotSimulator.Simulate(
                    board, context.WallLimitsClockwise, ShotBoardGather.OriginForAngle(angle, context),
                    ShotBoardGather.DirectionFromDegrees(angle), context.StartingShields,
                    context.ProjectileContactRadius, workingSet, context.RippleDamage,
                    projectileSpeed: context.ProjectileSpeed, cruiseConfig: context.CruiseConfig,
                    dynamics: emptyBoard ? null : context.Dynamics, allowedColors: context.AllowedColors,
                    rainbowColorId: context.RainbowColorId, seed: context.LoadedSeed,
                    items: emptyBoard ? null : context.Items, streakGrantsShields: context.StreakGrantsShields,
                    rippleRadius: context.RippleRadius, bumpers: context.Bumpers).Mushrooms;
                var flies = tally.LancesArmed > 0;
                run = flies ? run + 1 : 0;
                flying += flies ? 1 : 0;
                if (run > widestRun)
                {
                    widestRun = run;
                    widestEnd = i;
                }
            }

            var walls = new HashSet<int>();
            for (var i = 0; i < mushrooms; i++)
            {
                walls.Add(field.Bumpers[i].Wall);
            }

            var cannon = thrower.Position;
            var where = FormattableString.Invariant(
                $"{mushrooms} whole caps in {walls.Count} walls, {cutCaps} cut, the cannon at {cannon.x:0.###},{cannon.y:0.###}");
            if (widestEnd < 0)
            {
                return $"error: none of {samples} angles arms the lance ({where})";
            }

            var best = AimAngleGrid.ResolveSweepAngle(
                widestEnd - (widestRun / 2), config.AimAngleMinDegrees, config.AimAngleMaxDegrees,
                config.AimAngleStepDegrees, continuousSamples);
            var arming = $"{flying} of {samples} angles arm the lance";
            return FormattableString.Invariant(
                $"angle {best:0.###} (the middle of {widestRun} in a row; {arming}; {where})");
        }

        /// <summary>
        ///     Renders the main camera (straight to a texture, so it's fresh even while the Editor is unfocused) and
        ///     writes it to <paramref name="path" />: the whole frame, or with <paramref name="around" /> (a balloon type,
        ///     "col,row", or a world point "x;y") a <paramref name="size" />-pixel square centred there.
        /// </summary>
        public static string Capture(string path, string around = "", int size = 480)
        {
            var camera = Camera.main;
            var width = camera.pixelWidth;
            var height = camera.pixelHeight;
            // A float target, as the screen's HDR intermediate is: rendered into an 8-bit one, URP composites the
            // main camera's HDR effects map differently from the Game view (wide, flat glows on the lightning).
            // Read back into an 8-bit texture, it clamps as the screen does.
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGBHalf);
            var previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = previous;

            var x0 = 0;
            var y0 = 0;
            var cropWidth = width;
            var cropHeight = height;
            if (!string.IsNullOrEmpty(around))
            {
                var centre = camera.WorldToScreenPoint(WorldPoint(around));
                cropWidth = cropHeight = Math.Min(size, Math.Min(width, height));
                x0 = Mathf.Clamp(Mathf.RoundToInt(centre.x) - cropWidth / 2, 0, width - cropWidth);
                y0 = Mathf.Clamp(Mathf.RoundToInt(centre.y) - cropHeight / 2, 0, height - cropHeight);
            }

            var active = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(cropWidth, cropHeight, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(x0, y0, cropWidth, cropHeight), 0, 0);
            pixels.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            Object.DestroyImmediate(pixels);
            return $"wrote {cropWidth}x{cropHeight} to {path}";
        }

        /// <summary>
        ///     Writes every render map the Game Render Maps window lists into <paramref name="directory" />, as it
        ///     stands this frame and at its own resolution: <c>name-rgb.png</c> holds its colour channels and
        ///     <c>name-a.png</c> its alpha as grey. Channels brighter than 1 are scaled down to fit. Answers one
        ///     entry per map, <c>name=WxH,max colour,max alpha</c> or <c>name=none</c> for a map that doesn't exist
        ///     yet, joined by <c>|</c>.
        /// </summary>
        public static string Maps(string directory)
        {
            Directory.CreateDirectory(directory);
            var answer = new StringBuilder();
            foreach (var (name, texture) in GameRenderMapsWindow.CurrentMaps())
            {
                var file = name.ToLowerInvariant().Replace(' ', '-');
                answer.Append(answer.Length > 0 ? "|" : "").Append(file).Append('=');
                answer.Append(texture != null ? WriteMap(texture, Path.Combine(directory, file)) : "none");
            }

            return answer.ToString();
        }

        private static string WriteMap(Texture texture, string pathWithoutSuffix)
        {
            // Through a float target, so a map's own format (half, 16-bit, single channel) reads the same way.
            var target = RenderTexture.GetTemporary(
                texture.width, texture.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, target);
            var active = RenderTexture.active;
            RenderTexture.active = target;
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBAFloat, false, true);
            read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target);

            var pixels = read.GetPixels();
            var maxColor = 0f;
            var maxAlpha = 0f;
            foreach (var pixel in pixels)
            {
                maxColor = Mathf.Max(maxColor, pixel.maxColorComponent);
                maxAlpha = Mathf.Max(maxAlpha, pixel.a);
            }

            var colorScale = maxColor > 1f ? 1f / maxColor : 1f;
            var alphaScale = maxAlpha > 1f ? 1f / maxAlpha : 1f;
            var color = new Color32[pixels.Length];
            var alpha = new Color32[pixels.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                color[i] = new Color(pixel.r * colorScale, pixel.g * colorScale, pixel.b * colorScale, 1f);
                var grey = Mathf.Clamp01(pixel.a * alphaScale);
                alpha[i] = new Color(grey, grey, grey, 1f);
            }

            var written = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
            written.SetPixels32(color);
            File.WriteAllBytes(pathWithoutSuffix + "-rgb.png", written.EncodeToPNG());
            written.SetPixels32(alpha);
            File.WriteAllBytes(pathWithoutSuffix + "-a.png", written.EncodeToPNG());
            Object.DestroyImmediate(written);
            Object.DestroyImmediate(read);
            return FormattableString.Invariant($"{texture.width}x{texture.height},{maxColor:0.##},{maxAlpha:0.##}");
        }

        private static ScenarioKind ParseScenario(string scenario)
        {
            return scenario.ToLowerInvariant() switch
            {
                "meadow" or "ground" => ScenarioKind.Ground,
                _ => (ScenarioKind)Enum.Parse(typeof(ScenarioKind), scenario, true)
            };
        }

        private static IEnumerable<GridActorType> ActorTypes(string name)
        {
            return name switch
            {
                "clouds" or "puffs" => new[] { GridActorType.Puff },
                "bushes" => new[] { GridActorType.Bush },
                "enemies" => new[] { GridActorType.Deflector, GridActorType.Absorber, GridActorType.Gatekeeper },
                _ => new[] { (GridActorType)Enum.Parse(typeof(GridActorType), name, true) }
            };
        }

        // The launch Play button's own actions (it hides itself, the launch screen animates out, the preloaded game
        // scene takes over from the launcher's), all but LaunchPlayTrigger's, which waits out the cloud scroll
        // before the switch to the game that Begin makes at once.
        private static void PressLaunchPlay()
        {
            foreach (var button in Object.FindObjectsByType<ScaleDownButton>())
            {
                if (button.OnScaleComplete is not UnityEvent onPress)
                {
                    continue;
                }

                var states = new List<(int index, UnityEventCallState state)>();
                for (var i = 0; i < onPress.GetPersistentEventCount(); i++)
                {
                    if (onPress.GetPersistentTarget(i) is LaunchPlayTrigger)
                    {
                        states.Add((i, onPress.GetPersistentListenerState(i)));
                        onPress.SetPersistentListenerState(i, UnityEventCallState.Off);
                    }
                }

                if (states.Count == 0)
                {
                    continue;
                }

                try
                {
                    onPress.Invoke();
                }
                finally
                {
                    foreach (var (index, state) in states)
                    {
                        onPress.SetPersistentListenerState(index, state);
                    }
                }

                return;
            }
        }

        // However Play mode ends (End, or the Editor's own Play button), the next session plays as the game does.
        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                ResetCheats();
            }
        }

        private static void ResetCheats()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= AdvanceTick;
            _advanceUntil = -1f;
            CheatState.EmptyBoard = false;
            CheatState.BlockLevelUp = false;
            CheatState.TimeOfDaySpeedScale = 1f;

            // What a dealt board left behind: the level its run began at and the mark that a cheat was used. Play
            // mode resets both on its way in, but edit-mode tests run in this same domain and read them as they are.
            CheatState.StartLevel = 1;
            CheatState.AnyCheatUsed = false;
        }

        private static void AdvanceTick()
        {
            if (!EditorApplication.isPlaying || Object.FindAnyObjectByType<GameLifetimeScope>() == null)
            {
                EditorApplication.update -= AdvanceTick;
                _advanceUntil = -1f;
                return;
            }

            if (Time.time < _advanceUntil)
            {
                return;
            }

            EditorApplication.update -= AdvanceTick;
            _advanceUntil = -1f;
            Hold();
        }

        // Moves the staged ball along its line by game time, so the clearing decays behind it as it would in a shot.
        private static void FlyTick()
        {
            if (!EditorApplication.isPlaying || _flyingBall == null)
            {
                EditorApplication.update -= FlyTick;
                _flyingBall = null;
                return;
            }

            var t = Mathf.Clamp01((Time.time - _flightStart) / _flightSeconds);
            _flyingBall.position = Vector3.Lerp(_flightFrom, _flightTo, t);
            if (t >= 1f)
            {
                EditorApplication.update -= FlyTick;
                _flyingBall = null;
            }
        }

        // The ball's look without its behaviour: a copy of the prefab built under an inactive parent, so none of its
        // scripts wake up (they'd want injecting and a flight), then stripped of scripts and physics.
        private static GameObject BallLook(GameObject prefab, Vector3 position)
        {
            var holder = new GameObject("StagedBallHolder");
            holder.SetActive(false);
            var look = Object.Instantiate(prefab, position, Quaternion.identity, holder.transform);
            look.name = "StagedBall";
            foreach (var behaviour in look.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Object.DestroyImmediate(behaviour);
            }

            foreach (var collider in look.GetComponentsInChildren<Collider2D>(true))
            {
                Object.DestroyImmediate(collider);
            }

            foreach (var body in look.GetComponentsInChildren<Rigidbody2D>(true))
            {
                Object.DestroyImmediate(body);
            }

            look.transform.SetParent(null, true);
            Object.DestroyImmediate(holder);
            look.SetActive(true);
            return look;
        }

        // A placed balloon appears whole and at rest: its spawn scale-in finished (held time would leave it at 0),
        // and its trails cleared of the line they'd draw from wherever the pool last had it.
        private static void Settle(BalloonView view)
        {
            if (view == null)
            {
                return;
            }

            DOTween.Complete(view.transform);
            foreach (var trail in view.GetComponentsInChildren<TrailRenderer>())
            {
                trail.Clear();
            }
        }

        private static bool IsHeld()
        {
            return Mathf.Approximately(Time.timeScale, 0f);
        }

        // Seeds one system of a frozen pop and applies the options' swaps; the sheet's only to a system drawing one.
        private static void Prepare(ParticleSystem system, IReadOnlyDictionary<string, string> settings, Color? tint,
            Material material)
        {
            system.useAutoRandomSeed = false;
            system.randomSeed = (uint)Float(settings, "seed", DefaultSeed);
            if (tint.HasValue)
            {
                var main = system.main;
                main.startColor = tint.Value;
            }

            var sheet = system.textureSheetAnimation;
            if (!sheet.enabled)
            {
                return;
            }

            if (settings.TryGetValue("startFrame", out var startFrame))
            {
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, float.Parse(startFrame, CultureInfo.InvariantCulture));
            }

            if (material != null)
            {
                var renderer = system.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = Swapped(renderer.sharedMaterial, material);
            }
        }

        private static void HideBalloon(BalloonView view)
        {
            foreach (var renderer in view.GetComponentsInChildren<Renderer>())
            {
                if (renderer.enabled)
                {
                    renderer.enabled = false;
                    Hidden.Add(renderer);
                }
            }
        }

        // A forced pop, as the Remove Balloons cheat lands it: whatever the balloon, it goes.
        private static bool PopSlot(SlotGrid grid, Vector2Int slot)
        {
            var actor = grid.At(slot);
            if (actor == null)
            {
                return false;
            }

            Resolve<IHitDispatcher>().Dispatch(new ActorHitMessage(actor, grid.IndexToWorldPosition(slot), Vector3.zero,
                HitOutcome.Pop, new DamageContext(1)));
            return true;
        }

        private static BalloonView FindView(string target)
        {
            var slot = ParseSlot(target);
            foreach (var view in Object.FindObjectsByType<BalloonView>())
            {
                if (view.Model == null)
                {
                    continue;
                }

                if (slot.HasValue
                        ? view.Model.SlotIndex.Value == slot.Value
                        : string.Equals(view.Model.TypeName.ToString(), target, StringComparison.OrdinalIgnoreCase))
                {
                    return view;
                }
            }

            return null;
        }

        private static Vector3 WorldPoint(string around)
        {
            var parts = around.Split(';');
            if (parts.Length == 2)
            {
                return new Vector3(float.Parse(parts[0], CultureInfo.InvariantCulture),
                    float.Parse(parts[1], CultureInfo.InvariantCulture), 0f);
            }

            var view = FindView(around);
            if (view == null)
            {
                throw new ArgumentException($"no balloon at {around}");
            }

            return view.transform.position;
        }

        private static Vector2Int? ParseSlot(string target)
        {
            var parts = target.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out var col) && int.TryParse(parts[1], out var row))
            {
                return new Vector2Int(col, row);
            }

            return null;
        }

        // The pop the balloon itself would play, as BalloonView.PlayHitVfxForOutcome picks it: its catalogue entry's
        // Pop override, else the default pop; a coloured balloon's tints the effect's root, as the pool plays it.
        private static ParticleSystem PopPrefab(IBalloonModel model, out Color? tint)
        {
            tint = null;
            if (model is IHasColor colored && !string.IsNullOrEmpty(colored.Color.Value))
            {
                tint = Resolve<IGamePalette>().GetColor(colored.Color.Value);
            }

            var config = Resolve<IBalloonsConfiguration>();
            foreach (var entry in config.Entries)
            {
                if (entry.BalloonType != model.TypeName || entry.HitVfxOverrides == null)
                {
                    continue;
                }

                foreach (var vfx in entry.HitVfxOverrides)
                {
                    if (vfx.AppliesTo.HasFlag(HitOutcome.Pop))
                    {
                        return vfx.Prefab;
                    }
                }
            }

            return config.DefaultPopVfxPrefab;
        }

        // The swaps the options ask for, as a template material the effect's own are copied over; null when none.
        private static Material MaterialSwap(IReadOnlyDictionary<string, string> settings)
        {
            Shader shader = null;
            if (settings.TryGetValue("shaderFile", out var shaderFile))
            {
                shader = CompileShader(shaderFile);
            }
            else if (settings.TryGetValue("shader", out var shaderName))
            {
                shader = Shader.Find(shaderName) ?? throw new ArgumentException($"no shader {shaderName}");
            }

            Texture2D texture = null;
            if (settings.TryGetValue("texture", out var texturePath))
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                texture.LoadImage(File.ReadAllBytes(texturePath));
                Scratch.Add(texture);
            }

            var floats = new Dictionary<string, float>();
            foreach (var (key, value) in settings)
            {
                if (key.StartsWith("_"))
                {
                    floats[key] = float.Parse(value, CultureInfo.InvariantCulture);
                }
            }

            if (shader == null && texture == null && floats.Count == 0)
            {
                return null;
            }

            var template = new Material(shader != null ? shader : Shader.Find("Sprites/Default"))
            {
                mainTexture = texture
            };
            foreach (var (key, value) in floats)
            {
                template.SetFloat(key, value);
            }

            template.name = shader != null ? "swap shader" : "swap";
            Scratch.Add(template);
            return template;
        }

        private static Material Swapped(Material original, Material swap)
        {
            var copy = new Material(original);
            if (swap.name == "swap shader")
            {
                copy.shader = swap.shader;
            }

            if (swap.mainTexture != null)
            {
                copy.mainTexture = swap.mainTexture;
            }

            foreach (var key in swap.GetPropertyNames(MaterialPropertyType.Float))
            {
                if (swap.HasFloat(key) && copy.HasFloat(key))
                {
                    copy.SetFloat(key, swap.GetFloat(key));
                }
            }

            Scratch.Add(copy);
            return copy;
        }

        // Compiles a .shader from disk without importing it. Its relative includes are rewritten to project paths
        // from its own folder (Assets/...), which resolve against the open project.
        private static Shader CompileShader(string file)
        {
            var full = Path.GetFullPath(file).Replace('\\', '/');
            var assets = full.LastIndexOf("/Assets/", StringComparison.Ordinal);
            if (assets < 0)
            {
                throw new ArgumentException($"{file} isn't under an Assets folder");
            }

            var folder = Path.GetDirectoryName(full.Substring(assets + 1))?.Replace('\\', '/');
            var source = RelativeInclude.Replace(File.ReadAllText(full), match =>
            {
                var resolved = Path.GetFullPath(Path.Combine("/" + folder, match.Groups[1].Value)).Replace('\\', '/');
                return $"#include \"{resolved.Substring(resolved.IndexOf("Assets/", StringComparison.Ordinal))}\"";
            });
            var shader = ShaderUtil.CreateShaderAsset(source, true);
            Scratch.Add(shader);
            if (ShaderUtil.ShaderHasError(shader))
            {
                throw new ArgumentException($"{file} doesn't compile");
            }

            return shader;
        }

        private static Dictionary<string, string> ParseOptions(string options)
        {
            var parsed = new Dictionary<string, string>();
            foreach (var pair in (options ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var split = pair.IndexOf('=');
                if (split > 0)
                {
                    parsed[pair.Substring(0, split).Trim()] = pair.Substring(split + 1).Trim();
                }
            }

            return parsed;
        }

        private static float Float(IReadOnlyDictionary<string, string> settings, string key, float fallback)
        {
            return settings.TryGetValue(key, out var value) ? float.Parse(value, CultureInfo.InvariantCulture) : fallback;
        }

        private static T FindCheat<T>() where T : class, ICheat
        {
            foreach (var cheat in Resolve<IEnumerable<ICheat>>())
            {
                if (cheat is T found)
                {
                    return found;
                }
            }

            throw new InvalidOperationException($"no {typeof(T).Name}");
        }

        private static T Resolve<T>()
        {
            return Object.FindAnyObjectByType<GameLifetimeScope>().Container.Resolve<T>();
        }

        // Publishes a message as the game does. This assembly does not reference MessagePipe, so the game's
        // publisher of the message is found and called by name.
        private static void Publish<T>(T message)
        {
            var publisherType = Type.GetType("MessagePipe.IPublisher`1, MessagePipe", true).MakeGenericType(typeof(T));
            var publisher = Object.FindAnyObjectByType<GameLifetimeScope>().Container.Resolve(publisherType);
            publisherType.GetMethod("Publish").Invoke(publisher, new object[] { message });
        }
    }
}
#endif
