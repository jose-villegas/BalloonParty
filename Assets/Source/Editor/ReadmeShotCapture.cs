#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE
using System;
using System.Collections.Generic;
using System.IO;
using BalloonParty.Balloon.Model;
using BalloonParty.Cheats;
using BalloonParty.Cheats.Autoplay;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using BalloonParty.Game;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Takes the repository README's stills in Play mode, as a fixed script, so they can be retaken after any
    ///     change to the game's look with nothing chosen by hand: each scenario at morning and at night, paused on
    ///     a full board, and each item set off on a balloon in the middle of a fresh level-1 board, frozen at the
    ///     moment its effect is fullest. PNGs at the Game view's resolution, then <see cref="DoneFile" />, holding
    ///     "ok" or what went wrong. Run by <c>Tools/readme_shots/capture.py</c>, which composes them.
    /// </summary>
    internal static class ReadmeShotCapture
    {
        internal const string DoneFile = "done.txt";

        private const float MorningAngle = 60f;
        private const float NightAngle = 300f;
        private const float ItemAngle = 120f;
        private const float LaunchTimeout = 120f;
        private const float BoardSettleSeconds = 3f;
        private const float ItemAppearSeconds = 1.5f;
        private const float LightSettleSeconds = 1.5f;
        private const float FrameSeconds = 0.4f;
        private const int ScenarioSearchLevels = 500;

        // A jump's board overflows within a couple of seconds (it starts packed at the stretch's level), so the
        // scenario shots pause it just after it fills.
        private const float JumpPauseSeconds = 0.6f;

        private static readonly (ScenarioKind Kind, string Name)[] Scenarios =
        {
            (ScenarioKind.Sky, "sky"), (ScenarioKind.Ground, "meadow"), (ScenarioKind.Water, "water")
        };

        // Each item and when, after its balloon pops, its effect is fullest, in seconds of game time.
        private static readonly (ItemType Item, float Seconds)[] Items =
        {
            (ItemType.Bomb, 0.3f), (ItemType.Laser, 0.3f), (ItemType.Lightning, 0.5f),
            (ItemType.Paint, 0.5f), (ItemType.Shield, 0.3f), (ItemType.Snipe, 0.3f)
        };

        private static readonly float[] BurstSeconds = { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.8f, 1f, 1.2f };

        private static readonly List<Step> Steps = new();

        private static string _outDir;
        private static int _index;
        private static double _stepStartedAt;
        private static IWriteableSlotActor _host;
        private static Vector3 _hostPosition;

        /// <summary>
        ///     Starts the shots into <paramref name="outDir" />; Play mode must be on, at the launch screen or about to
        ///     reach it. With <paramref name="burst" />, each item is taken at a run of moments instead of its own, to
        ///     choose those moments by.
        /// </summary>
        internal static string Run(string outDir, bool burst)
        {
            if (!EditorApplication.isPlaying)
            {
                return "Play mode is off";
            }

            _outDir = outDir;
            Directory.CreateDirectory(outDir);
            File.Delete(Path.Combine(outDir, DoneFile));
            Steps.Clear();
            Plan(burst);
            _index = 0;
            _stepStartedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            return $"started, {Steps.Count} steps";
        }

        private static void Plan(bool burst)
        {
            // In the background the Editor stops the player loop unless told otherwise.
            Do(() => Application.runInBackground = true);
            Until(() => Navigation.Current.Value == NavigationState.Launch, LaunchTimeout);
            Wait(1.5f);
            Until(() => AutoplayTap.ButtonThatCalls<LaunchPlayTrigger>("Play"), LaunchTimeout);
            Until(() => Navigation.Current.Value == NavigationState.Game, LaunchTimeout);
            Do(Prepare);

            foreach (var (item, seconds) in Items)
            {
                PlanItem(item, burst ? BurstSeconds : new[] { seconds });
            }

            foreach (var (kind, name) in Scenarios)
            {
                PlanScenario(kind, name);
            }

            Do(Unfreeze);
        }

        private static void PlanItem(ItemType item, IReadOnlyList<float> moments)
        {
            Do(() =>
            {
                Unfreeze();
                CheatState.StartLevel = 1;
                Resolve<RunController>().RestartRun();
            });
            Wait(BoardSettleSeconds);
            Do(() =>
            {
                SetAngle(ItemAngle);
                Arm(item);
            });
            Wait(ItemAppearSeconds);
            Do(Pop);
            var since = 0f;
            for (var i = 0; i < moments.Count; i++)
            {
                var name = moments.Count == 1 ? $"item_{Lower(item)}" : $"item_{Lower(item)}_{moments[i]:0.0}";
                Wait(moments[i] - since);
                since = moments[i];
                Freeze(name);
            }

            Wait(1.5f);
        }

        private static void PlanScenario(ScenarioKind kind, string name)
        {
            Do(() =>
            {
                Unfreeze();
                JumpTo(kind);
            });
            Wait(JumpPauseSeconds);
            Do(FreezeWorld);
            foreach (var (angle, time) in new[] { (MorningAngle, "morning"), (NightAngle, "night") })
            {
                Do(() => SetAngle(angle));
                Wait(LightSettleSeconds);
                Do(() => Capture($"scene_{name}_{time}"));
                Wait(FrameSeconds);
            }
        }

        // Pauses the game, takes the shot once the paused frame is drawn, and lets it run on.
        private static void Freeze(string name)
        {
            Do(FreezeWorld);
            Wait(FrameSeconds);
            Do(() => Capture(name));
            Wait(FrameSeconds);
            Do(Unfreeze);
        }

        private static void FreezeWorld()
        {
            Resolve<ITimeScaleClaims>().ClaimExclusive(TimeScaleSource.Capture, 0f);
        }

        private static void Unfreeze()
        {
            Resolve<ITimeScaleClaims>().ReleaseExclusive(TimeScaleSource.Capture);
        }

        private static void Prepare()
        {
            CheatState.TimeOfDaySpeedScale = 0f;
            CheatState.BlockLevelUp = true;
            var fps = Object.FindAnyObjectByType<FPSCounter>();
            if (fps != null)
            {
                fps.enabled = false;
            }
        }

        // Puts the item on the plain balloon nearest the board's middle, so its effect has board all round it.
        private static void Arm(ItemType item)
        {
            var grid = Resolve<SlotGrid>();
            var actors = new List<(IWriteableSlotActor Actor, Vector3 Position)>();
            var middle = Vector3.zero;
            var occupied = 0;
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    var actor = grid.At(slot);
                    if (actor == null)
                    {
                        continue;
                    }

                    var position = grid.IndexToWorldPosition(slot);
                    middle += position;
                    occupied++;
                    if (actor is IHasWriteableItemSlot holder && holder.Item.Value == ItemType.None &&
                        !holder.Color.Value.StartsWith("__"))
                    {
                        actors.Add((actor, position));
                    }
                }
            }

            if (actors.Count == 0)
            {
                throw new InvalidOperationException($"No plain balloon to put the {item} on");
            }

            middle /= occupied;
            actors.Sort((a, b) => (a.Position - middle).sqrMagnitude.CompareTo((b.Position - middle).sqrMagnitude));
            (_host, _hostPosition) = actors[0];
            ((IHasWriteableItemSlot)_host).Item.Value = item;
        }

        // A piercing hit from below, as a shot going up would make: it pops the balloon whatever it is.
        private static void Pop()
        {
            var hit = ActorHitMessage.From((IHitable)_host, _hostPosition, Vector3.up,
                new DamageContext(1, DamageFlags.Piercing));
            Resolve<IHitDispatcher>().Dispatch(hit);
        }

        private static void JumpTo(ScenarioKind kind)
        {
            var pacing = Resolve<ILevelPacingConfiguration>();
            var seed = Resolve<IRunSeed>().Value;
            var level = JumpToScenarioCheat.NextStretchStart(l => pacing.ScenarioForLevel(l, seed),
                Resolve<ILevelProgress>().Level.Value, kind, ScenarioSearchLevels);
            if (level < 1)
            {
                throw new InvalidOperationException($"No {kind} stretch in the next {ScenarioSearchLevels} levels");
            }

            CheatState.StartLevel = level;
            CheatState.KeepRunSeed = true;
            Resolve<RunController>().RestartRun();
        }

        private static void SetAngle(float degrees)
        {
            Resolve<TimeOfDayClock>().SetAngleDegrees(degrees);
        }

        private static void Capture(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(_outDir, name + ".png"));
        }

        private static T Resolve<T>()
        {
            return Object.FindAnyObjectByType<GameLifetimeScope>().Container.Resolve<T>();
        }

        private static string Lower(ItemType item)
        {
            return item.ToString().ToLowerInvariant();
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                Finish($"Play mode ended at step {_index}");
                return;
            }

            try
            {
                while (_index < Steps.Count)
                {
                    var step = Steps[_index];
                    var elapsed = (float)(EditorApplication.timeSinceStartup - _stepStartedAt);
                    if (elapsed < step.Wait || step.Until != null && !step.Until())
                    {
                        if (step.Until != null && elapsed > step.Timeout)
                        {
                            Finish($"step {_index} timed out");
                        }

                        return;
                    }

                    step.Do?.Invoke();
                    _index++;
                    _stepStartedAt = EditorApplication.timeSinceStartup;
                }
            }
            catch (Exception e)
            {
                Finish($"step {_index} failed: {e}");
                return;
            }

            Finish("ok");
        }

        private static void Finish(string outcome)
        {
            EditorApplication.update -= Tick;
            if (EditorApplication.isPlaying && Object.FindAnyObjectByType<GameLifetimeScope>() != null)
            {
                Unfreeze();
            }

            File.WriteAllText(Path.Combine(_outDir, DoneFile), outcome);
        }

        private static void Do(Action action)
        {
            Steps.Add(new Step(0f, null, 0f, action));
        }

        private static void Wait(float seconds)
        {
            Steps.Add(new Step(seconds, null, 0f, null));
        }

        private static void Until(Func<bool> condition, float timeout)
        {
            Steps.Add(new Step(0f, condition, timeout, null));
        }

        private readonly struct Step
        {
            internal readonly float Wait;
            internal readonly Func<bool> Until;
            internal readonly float Timeout;
            internal readonly Action Do;

            internal Step(float wait, Func<bool> until, float timeout, Action @do)
            {
                Wait = wait;
                Until = until;
                Timeout = timeout;
                Do = @do;
            }
        }
    }
}
#endif
