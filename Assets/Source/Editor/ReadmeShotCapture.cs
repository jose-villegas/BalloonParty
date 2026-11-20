#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Cheats;
using BalloonParty.Cheats.Autoplay;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using BalloonParty.Editor.Staging;
using BalloonParty.Game;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Projectile;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using BalloonParty.Solver;
using BalloonParty.Thrower;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Takes the repository README's pictures in Play mode, as a fixed script, so they can be retaken after any
    ///     change to the game's look with nothing chosen by hand. Each scenario and the bonus level's page: a bot
    ///     plays the level and the still is its best moment, the one with the most balloons popping at once, taken
    ///     at morning and at night. Each item: set off on a balloon in the middle of a fresh board, frozen at the
    ///     moment its effect is fullest. Each gift balloon: a few seconds of it in the middle of such a board, as a
    ///     run of squares cut out round it, at morning and at night, for an animation. PNGs, then
    ///     <see cref="DoneFile" />, holding "ok" or what went wrong. Run by <c>Tools/readme_shots/capture.py</c>,
    ///     which composes them.
    ///     <para>
    ///         While the bot plays, and while a gift is filmed, every frame lasts one physics step whatever the
    ///         real frame took (<c>Time.captureDeltaTime</c>), so the game runs at its own pace however long a
    ///         picture takes to write. The run's seed is fixed (<see cref="PlaySeed" />), so every retake plays the
    ///         same levels; the play itself still differs a little from one retake to the next.
    ///     </para>
    /// </summary>
    internal static class ReadmeShotCapture
    {
        internal const string DoneFile = "done.txt";

        private const string MomentsFile = "moments.txt";
        private const float MorningAngle = 60f;
        private const float NightAngle = 300f;
        private const float ItemAngle = 120f;
        private const string FireCheat = "Fire Best Shot";

        // Past level 1's single colour, so an item's effect reads against a mixed board, and short of the
        // levels whose starting board overflows at once.
        private const int ItemLevel = 3;

        // How long a ball armed by a popped item takes to be the held one, before it's fired.
        private const float ArmSeconds = 0.6f;
        private const float LaunchTimeout = 120f;
        private const float BoardSettleSeconds = 3f;
        private const float ItemAppearSeconds = 1.5f;
        private const float LightSettleSeconds = 1.5f;
        private const float FrameSeconds = 0.4f;
        private const int ScenarioSearchLevels = 500;

        // Every retake plays the same levels: the run's seed, and the dice each level starts from.
        private const int PlaySeed = 7;

        // The longest the bot plays a scenario's level, in frames of one physics step each: 30 s of game time.
        private const int PlayFrames = 1500;

        // A level is dealt and in play within this many frames of its jump.
        private const int DealFrames = 150;

        // Real seconds a level's play may take, its stills included, before the script gives up on it.
        private const float PlayTimeout = 600f;

        // The least frames between two of the bot's shots: a player's pace, not a machine's.
        private const int ShotGapFrames = 20;

        // A moment's glory is the balloons gone from the board in this many frames up to it, plus the balls in
        // flight. Under MinGlory a moment is not worth a still.
        private const int GloryWindowFrames = 20;
        private const int MinGlory = 7;

        // A burst's shapes, smoke and light are fullest about this long after its pops begin.
        private const int GloryDelayFrames = 12;

        // A still wants a board in it: with fewer balloons left than this (a nuke's blast nearly done) it is
        // not taken.
        private const int MinOnBoard = 8;

        // Two colours and no armour yet: nothing walks across a gift while it is filmed.
        private const int GiftLevel = 2;

        // The balloon it replaces pops as it is placed, and that pop's pieces and score are gone by then.
        private const float GiftSettleSeconds = 2.5f;

        // A gift's picture is a square round the balloon, this share of the frame's width across: the balloon,
        // its rays and a ring of neighbours.
        private const float GiftCropShare = 0.26f;

        // A gift's animation: this many pictures, one every GiftEveryFrames frames of game time.
        private const int GiftPictures = 48;
        private const int GiftEveryFrames = 3;

        private static readonly (ScenarioKind Kind, string Name)[] Scenarios =
        {
            (ScenarioKind.Sky, "sky"), (ScenarioKind.Ground, "meadow"), (ScenarioKind.Water, "water"),
            (ScenarioKind.Paper, "bonus")
        };

        private static readonly (float Angle, string Name)[] Times =
        {
            (MorningAngle, "morning"), (NightAngle, "night")
        };

        private static readonly (BalloonType Type, string Name)[] Gifts =
        {
            (BalloonType.Golden, "golden"), (BalloonType.Heart, "heart"), (BalloonType.Disco, "disco"),
            (BalloonType.Nuke, "nuke")
        };

        // Each item and when its effect is fullest, in seconds of game time: after its balloon pops, or for an
        // item that arms the ball (Fires), after the armed ball is fired, since its effect rides on the ball.
        private static readonly (ItemType Item, float Seconds, bool Fires)[] Items =
        {
            (ItemType.Bomb, 0.3f, false), (ItemType.Laser, 0.3f, false), (ItemType.Lightning, 0.2f, false),
            (ItemType.Paint, 0.4f, false), (ItemType.Shield, 0.2f, true), (ItemType.Snipe, 0.3f, true)
        };

        private static readonly float[] BurstSeconds = { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.8f, 1f, 1.2f };

        private static readonly List<Step> Steps = new();

        // What a still does while the world is held, each after its wait in real seconds.
        private static readonly Queue<(float Wait, Action Do)> Hold = new();

        // The balloons gone in each of the last GloryWindowFrames frames.
        private static readonly Queue<(int Frame, int Gone)> Gone = new();

        private static readonly StringBuilder Moments = new();

        private static string _outDir;
        private static int _index;
        private static double _stepStartedAt;
        private static IWriteableSlotActor _host;
        private static Vector3 _hostPosition;
        private static BalloonType _gift;
        private static GameObject _runner;
        private static ThrowerLifetimeScope _throwerScope;
        private static int _frame;
        private static int _playFrame;
        private static int _nextShotFrame;
        private static int _occupied;
        private static int _goneInWindow;
        private static int _bestGlory;
        private static int _pendingGlory;
        private static int _takeAt;
        private static double _holdStepAt;

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
            Hold.Clear();
            Moments.Clear();
            Plan(burst);
            _index = 0;
            _frame = 0;
            _stepStartedAt = EditorApplication.timeSinceStartup;

            // Stepped from the player's own loop, once a frame, so a step that counts frames counts every one;
            // it also keeps going while the Editor is in the background, where its own update can stall.
            Application.runInBackground = true;
            Object.DestroyImmediate(_runner);
            _runner = new GameObject(nameof(ReadmeShotCapture)) { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(_runner);
            _runner.AddComponent<Runner>();
            return $"started, {Steps.Count} steps";
        }

        private static void Plan(bool burst)
        {
            Until(() => Navigation.Current.Value == NavigationState.Launch, LaunchTimeout);
            Wait(1.5f);
            Until(() => AutoplayTap.ButtonThatCalls<LaunchPlayTrigger>("Play"), LaunchTimeout);
            Until(() => Navigation.Current.Value == NavigationState.Game, LaunchTimeout);
            Do(Prepare);

            foreach (var (item, seconds, fires) in Items)
            {
                PlanItem(item, fires, burst ? BurstSeconds : new[] { seconds });
            }

            foreach (var (type, name) in Gifts)
            {
                PlanGift(type, name);
            }

            foreach (var (kind, name) in Scenarios)
            {
                PlanScenario(kind, name);
            }

            Do(Unfreeze);
        }

        private static void PlanGift(BalloonType type, string name)
        {
            Do(() =>
            {
                Unfreeze();
                CheatState.StartLevel = GiftLevel;
                Resolve<RunController>().RestartRun();
            });
            Wait(BoardSettleSeconds);
            Do(() => PlaceGift(type));
            Wait(GiftSettleSeconds);

            // Held while the light changes, so the board fills no further than its few filmed seconds need.
            Do(FreezeWorld);
            foreach (var (angle, time) in Times)
            {
                Do(() => SetAngle(angle));
                Wait(LightSettleSeconds);
                Do(() =>
                {
                    Time.captureDeltaTime = Time.fixedDeltaTime;
                    Unfreeze();
                });
                Frames(GiftPictures * GiftEveryFrames, frame => FilmGift(frame, $"gift_{name}_{time}"));
                Do(() =>
                {
                    Time.captureDeltaTime = 0f;
                    FreezeWorld();
                });
            }
        }

        private static void PlanItem(ItemType item, bool fires, IReadOnlyList<float> moments)
        {
            Do(() =>
            {
                Unfreeze();
                CheatState.StartLevel = ItemLevel;
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
            if (fires)
            {
                Wait(ArmSeconds);
                Do(Fire);
            }

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
            Do(() => StartPlay(kind));
            Until(() => PlayOn(name), PlayTimeout);
            Do(EndPlay);
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
            CheatState.PinnedRunSeed = PlaySeed;
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
            (_host, _hostPosition, _) = MiddlePlainBalloon();
            ((IHasWriteableItemSlot)_host).Item.Value = item;
        }

        // Swaps the plain balloon nearest the board's middle for the gift, so the picture has board all round it.
        private static void PlaceGift(BalloonType type)
        {
            var (_, _, slot) = MiddlePlainBalloon();
            _gift = type;
            var placed = SceneStage.Place(type.ToString(), slot.x, slot.y, "None", true);
            if (placed.StartsWith("error", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(placed);
            }
        }

        // One picture of the gift's film every GiftEveryFrames frames, round the gift wherever the board has
        // carried it by now: there is one of its kind on the board.
        private static void FilmGift(int frame, string name)
        {
            if (frame % GiftEveryFrames != 0)
            {
                return;
            }

            var size = Mathf.RoundToInt(Camera.main.pixelWidth * GiftCropShare);
            SceneStage.Capture(Path.Combine(_outDir, $"{name}_{frame / GiftEveryFrames:00}.png"), _gift.ToString(),
                size);
        }

        private static (IWriteableSlotActor Actor, Vector3 Position, Vector2Int Slot) MiddlePlainBalloon()
        {
            var grid = Resolve<SlotGrid>();
            var actors = new List<(IWriteableSlotActor Actor, Vector3 Position, Vector2Int Slot)>();
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
                        actors.Add((actor, position, slot));
                    }
                }
            }

            if (actors.Count == 0)
            {
                throw new InvalidOperationException("No plain balloon in the middle of the board");
            }

            middle /= occupied;
            actors.Sort((a, b) => (a.Position - middle).sqrMagnitude.CompareTo((b.Position - middle).sqrMagnitude));
            return actors[0];
        }

        // A piercing hit from below, as a shot going up would make: it pops the balloon whatever it is.
        private static void Pop()
        {
            var hit = ActorHitMessage.From((IHitable)_host, _hostPosition, Vector3.up,
                new DamageContext(1, DamageFlags.Piercing));
            Resolve<IHitDispatcher>().Dispatch(hit);
        }

        private static void Fire()
        {
            Solver().Execute();
        }

        private static FireBestShotCheat Solver()
        {
            foreach (var cheat in Resolve<IEnumerable<ICheat>>())
            {
                if (cheat is FireBestShotCheat solver)
                {
                    return solver;
                }
            }

            throw new InvalidOperationException($"No {FireCheat} cheat");
        }

        // The first level of the seed's run that opens a stretch of the scenario, so each retake jumps to the same one.
        private static void JumpTo(ScenarioKind kind)
        {
            var pacing = Resolve<ILevelPacingConfiguration>();

            // The backdrop, not the schedule: a bonus level inside a stretch shows its page, not the stretch.
            var level = JumpToScenarioCheat.NextStretchStart(l => pacing.BackdropForLevel(l, PlaySeed), 1, kind,
                ScenarioSearchLevels);
            if (level < 1)
            {
                throw new InvalidOperationException($"No {kind} stretch in the first {ScenarioSearchLevels} levels");
            }

            CheatState.StartLevel = level;
            CheatState.ForcedRunSeed = PlaySeed;
            Resolve<RunController>().RestartRun();
        }

        // Real play on the scenario's level: the score counts, the bars fill and a heart can be lost. From here
        // every frame is one physics step long, so the level plays at its own pace while stills are written, and
        // its dice start from the seed.
        private static void StartPlay(ScenarioKind kind)
        {
            Unfreeze();
            CheatState.BlockLevelUp = false;
            JumpTo(kind);
            SetAngle(MorningAngle);
            UnityEngine.Random.InitState(PlaySeed);
            Time.captureDeltaTime = Time.fixedDeltaTime;
            Hold.Clear();
            Gone.Clear();
            _playFrame = 0;
            _nextShotFrame = ShotGapFrames;
            _occupied = 0;
            _goneInWindow = 0;
            _bestGlory = 0;
            _pendingGlory = 0;
            _takeAt = -1;
        }

        private static void EndPlay()
        {
            Time.captureDeltaTime = 0f;
            CheatState.BlockLevelUp = true;
        }

        // One frame of the bot's play, or one step of a still being taken; true once the level's play is over.
        private static bool PlayOn(string name)
        {
            if (Hold.Count > 0)
            {
                StepHold();
                return false;
            }

            // The level's play ends with its time, with the run, or with the level itself: a level-up waits on
            // a card being picked. A level just dealt is not yet in play for a moment, which is not its end.
            var lost = Navigation.Current.Value != NavigationState.Game;
            var inPlay = Resolve<ILevelProgress>().Phase.Value == LevelUpPhase.Playing;
            if (_playFrame >= PlayFrames || lost || !inPlay && _playFrame > DealFrames)
            {
                return HasItsStill(name, lost);
            }

            if (inPlay)
            {
                Shoot();
            }

            WatchForGlory(name, Glory());
            _playFrame++;
            return false;
        }

        private static void StepHold()
        {
            if (EditorApplication.timeSinceStartup - _holdStepAt < Hold.Peek().Wait)
            {
                return;
            }

            Hold.Dequeue().Do();
            _holdStepAt = EditorApplication.timeSinceStartup;
        }

        // Whether the level, its play over, has a still; one that never had a burst big enough gets the board
        // as the bot leaves it.
        private static bool HasItsStill(string name, bool lost)
        {
            if (_bestGlory > 0)
            {
                return true;
            }

            if (lost)
            {
                throw new InvalidOperationException($"The {name} level was lost before any moment worth a still");
            }

            TakeStill(name, 1);
            return false;
        }

        // A still is taken a little after each moment that beats every one before it, over the last, so the
        // one left at the end is the level's best.
        private static void WatchForGlory(string name, int glory)
        {
            if (_takeAt < 0)
            {
                // Not while the level is still being dealt: its balloons change slots as they settle.
                if (_playFrame > DealFrames && glory >= MinGlory && glory > _bestGlory)
                {
                    _pendingGlory = glory;
                    _takeAt = _playFrame + GloryDelayFrames;
                }

                return;
            }

            _pendingGlory = Math.Max(_pendingGlory, glory);
            if (_playFrame < _takeAt)
            {
                return;
            }

            _takeAt = -1;
            if (_occupied >= MinOnBoard)
            {
                TakeStill(name, _pendingGlory);
            }
        }

        // The bot: the solver's best shot whenever the cannon is ready, no sooner than a player would fire again.
        private static void Shoot()
        {
            if (_playFrame < _nextShotFrame)
            {
                return;
            }

            if (_throwerScope == null)
            {
                _throwerScope = Object.FindAnyObjectByType<ThrowerLifetimeScope>();
            }

            if (_throwerScope == null || _throwerScope.Container == null)
            {
                return;
            }

            var thrower = _throwerScope.Container.Resolve<ThrowerController>();
            if (!thrower.IsReadyToFire() || !Solver().TryFindBestAngle(out var angle))
            {
                return;
            }

            thrower.FireAt(ShotBoardGather.DirectionFromDegrees(angle));
            _nextShotFrame = _playFrame + ShotGapFrames;
        }

        // How much is going on this frame: the balloons that left the board in the last GloryWindowFrames
        // frames, plus the balls in flight.
        private static int Glory()
        {
            var grid = Resolve<SlotGrid>();
            var occupied = 0;
            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    if (grid.At(new Vector2Int(col, row)) is BalloonModelBase)
                    {
                        occupied++;
                    }
                }
            }

            var gone = Math.Max(0, _occupied - occupied);
            _occupied = occupied;
            Gone.Enqueue((_playFrame, gone));
            _goneInWindow += gone;
            while (Gone.Peek().Frame <= _playFrame - GloryWindowFrames)
            {
                _goneInWindow -= Gone.Dequeue().Gone;
            }

            return _goneInWindow + Resolve<IProjectileRegistry>().InFlightCount;
        }

        // Holds the world, takes it at morning and at night over the level's last still, and plays on.
        private static void TakeStill(string name, int glory)
        {
            _bestGlory = glory;
            Moments.AppendLine(FormattableString.Invariant(
                $"{name}: glory {glory} at {_playFrame * Time.fixedDeltaTime:0.0} s"));
            FreezeWorld();
            _holdStepAt = EditorApplication.timeSinceStartup;
            Hold.Enqueue((FrameSeconds, () => Capture($"scene_{name}_morning")));
            Hold.Enqueue((FrameSeconds, () => SetAngle(NightAngle)));
            Hold.Enqueue((LightSettleSeconds, () => Capture($"scene_{name}_night")));
            Hold.Enqueue((FrameSeconds, () => SetAngle(MorningAngle)));
            Hold.Enqueue((LightSettleSeconds, Unfreeze));
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
            try
            {
                while (_index < Steps.Count)
                {
                    var step = Steps[_index];
                    if (step.Frames > 0)
                    {
                        // One frame of it a call, so it sees every frame the game plays.
                        step.EachFrame(_frame);
                        _frame++;
                        if (_frame >= step.Frames)
                        {
                            _frame = 0;
                            _index++;
                            _stepStartedAt = EditorApplication.timeSinceStartup;
                        }

                        return;
                    }

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
            var runner = _runner;
            _runner = null;
            Object.Destroy(runner);
            _throwerScope = null;
            Time.captureDeltaTime = 0f;

            // Edit-mode tests run in this same domain and read the cheats as they are left.
            CheatState.PinnedRunSeed = 0;
            CheatState.ForcedRunSeed = 0;
            CheatState.BlockLevelUp = false;
            CheatState.TimeOfDaySpeedScale = 1f;
            CheatState.StartLevel = 1;
            CheatState.AnyCheatUsed = false;
            if (EditorApplication.isPlaying && Object.FindAnyObjectByType<GameLifetimeScope>() != null)
            {
                Unfreeze();
            }

            File.WriteAllText(Path.Combine(_outDir, MomentsFile), Moments.ToString());
            File.WriteAllText(Path.Combine(_outDir, DoneFile), outcome);
        }

        private static void Do(Action action)
        {
            Steps.Add(new Step(0f, null, 0f, action, 0, null));
        }

        private static void Wait(float seconds)
        {
            Steps.Add(new Step(seconds, null, 0f, null, 0, null));
        }

        // Asks once a frame of the game until the answer is yes.
        private static void Until(Func<bool> condition, float timeout)
        {
            Steps.Add(new Step(0f, condition, timeout, null, 0, null));
        }

        // Calls eachFrame once a frame of the game, with the frame's number from 0, for that many frames.
        private static void Frames(int frames, Action<int> eachFrame)
        {
            Steps.Add(new Step(0f, null, 0f, null, frames, eachFrame));
        }

        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                Tick();
            }

            // Play mode left while the script ran: the object goes with it.
            private void OnDestroy()
            {
                if (_runner == gameObject)
                {
                    Finish($"Play mode ended at step {_index}");
                }
            }
        }

        private readonly struct Step
        {
            internal readonly float Wait;
            internal readonly Func<bool> Until;
            internal readonly float Timeout;
            internal readonly Action Do;
            internal readonly int Frames;
            internal readonly Action<int> EachFrame;

            internal Step(float wait, Func<bool> until, float timeout, Action @do, int frames, Action<int> eachFrame)
            {
                Wait = wait;
                Until = until;
                Timeout = timeout;
                Do = @do;
                Frames = frames;
                EachFrame = eachFrame;
            }
        }
    }
}
#endif
