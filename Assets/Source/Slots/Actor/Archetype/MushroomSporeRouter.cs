using System;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Scenario;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    ///     Lets a meadow mushroom's spores go (@ref plan_meadow_mushrooms): hears what happens to a cap (a
    ///     ball's bounce, a lance's cut, a blast), asks <see cref="MushroomSporePlanner" /> what that is worth,
    ///     and sends the smoke to the game's smoke field and the specks to the mushroom's own view.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The smoke is the smoke field's.</b> A cone is three or four of its capsule stamps laid end to
    ///         end along the burst's way, each wider than the last and thinner (<see cref="PaintSource.MushroomSpores" />
    ///         is the profile: its colour, its base opacity). The field lights it, drifts it on its wind and
    ///         fades it as it does all its smoke: a hit's small puff is gone in about a second.
    ///     </para>
    ///     <para>
    ///         <b>A blast's burst waits.</b> A bomb's own cloud covers the mushroom at first, so the burst is
    ///         held for the tuned delay in a small fixed list, with the view it is for, and let go from
    ///         <see cref="Tick()" />. It is let go only if that view still stands in that slot, and the list is
    ///         emptied when the level changes and when the run starts over: a burst never lands on the mushroom
    ///         a new board put where the old one stood.
    ///     </para>
    /// </remarks>
    internal sealed class MushroomSporeRouter : IStartable, ITickable, IRunResettable, IDisposable
    {
        // The most bursts that can be waiting: a nuke blows every cap of the fullest board off within its sweep.
        internal const int MostWaiting = 8;

        // The smoke's cone, in the smoke field's capsules: how many, how narrow it starts, and how much thinner
        // the last is than the first.
        private const int ConeSteps = 4;
        private const int BlastSteps = 4;
        private const float ConeStartRadius = 0.08f;
        private const float ConeFarOpacity = 0.65f;

        private readonly IMushroomSettings _settings;
        private readonly SlotGrid _grid;
        private readonly SmokeFieldService _smoke;
        private readonly ILevelProgress _levelProgress;
        private readonly ISubscriber<MushroomBouncedMessage> _bounced;
        private readonly ISubscriber<MushroomCutMessage> _cut;
        private readonly MushroomSporePlanner _planner = new();
        private readonly Waiting[] _waiting = new Waiting[MostWaiting];
        private readonly CompositeDisposable _subscriptions = new();

        private int _waitingCount;

        public int ResetOrder => RunResetOrder.Quiesce;

        /// <summary>How many bursts are waiting for their delay.</summary>
        internal int WaitingCount => _waitingCount;

        [Inject]
        public MushroomSporeRouter(
            IMushroomSettings settings, SlotGrid grid, SmokeFieldService smoke, ILevelProgress levelProgress,
            ISubscriber<MushroomBouncedMessage> bounced, ISubscriber<MushroomCutMessage> cut)
        {
            _settings = settings;
            _grid = grid;
            _smoke = smoke;
            _levelProgress = levelProgress;
            _bounced = bounced;
            _cut = cut;
        }

        public void Start()
        {
            _bounced.Subscribe(message => OnBounced(message, Time.time)).AddTo(_subscriptions);
            _cut.Subscribe(message => OnCut(message, Time.time)).AddTo(_subscriptions);

            // The first value is the level the scope came up on; every one after it is a new board.
            _levelProgress.Level.Skip(1).Subscribe(_ => DropWaiting()).AddTo(_subscriptions);
        }

        public void Tick()
        {
            Tick(Time.time);
        }

        public void ResetRun(int generation)
        {
            DropWaiting();
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        /// <summary>Lets go every waiting burst whose delay has passed by <paramref name="now" />.</summary>
        internal void Tick(float now)
        {
            for (var i = _waitingCount - 1; i >= 0; i--)
            {
                if (now < _waiting[i].Due)
                {
                    continue;
                }

                var due = _waiting[i];
                _waiting[i] = _waiting[--_waitingCount];
                _waiting[_waitingCount] = default;
                Release(due.Slot, due.View, due.Burst);
            }
        }

        internal void OnBounced(MushroomBouncedMessage message, float now)
        {
            if (!TryViewAt(message.Slot, out var mushroom))
            {
                return;
            }

            Vector2 centre = _grid.IndexToWorldPosition(message.Slot);
            if (_planner.TryHit(
                    _settings.Spores, _settings.Radius, message.Slot, centre, message.Incoming, message.Speed,
                    message.Reward == MushroomReward.Arm, now, out var burst))
            {
                Release(message.Slot, mushroom, burst);
            }
        }

        internal void OnCut(MushroomCutMessage message, float now)
        {
            if (!TryViewAt(message.Slot, out var mushroom))
            {
                return;
            }

            if (message.Cause == MushroomCutCause.Blast)
            {
                if (_planner.TryBlast(_settings.Spores, message.Point, message.Direction, now, out var blast))
                {
                    Hold(message.Slot, mushroom, blast, now);
                }

                return;
            }

            if (_planner.TryCut(_settings.Spores, message.Point, message.Direction, now, out var cut))
            {
                Release(message.Slot, mushroom, cut);
            }
        }

        private bool TryViewAt(Vector2Int slot, out MushroomView mushroom)
        {
            mushroom = _grid.InBounds(slot) ? _grid.ViewAt(slot) as MushroomView : null;
            return mushroom != null;
        }

        private void Hold(Vector2Int slot, MushroomView mushroom, in MushroomSporeBurst burst, float now)
        {
            if (burst.Delay <= 0f || _waitingCount == MostWaiting)
            {
                Release(slot, mushroom, burst);
                return;
            }

            _waiting[_waitingCount++] = new Waiting
            {
                Slot = slot, View = mushroom, Burst = burst, Due = now + burst.Delay
            };
        }

        private void DropWaiting()
        {
            Array.Clear(_waiting, 0, _waiting.Length);
            _waitingCount = 0;
        }

        private void Release(Vector2Int slot, MushroomView mushroom, in MushroomSporeBurst burst)
        {
            // The mushroom may have left the board while its burst waited: then nothing goes. The view is the
            // one the burst was for, not whatever stands in the slot now.
            if (mushroom == null || !_grid.InBounds(slot) || !ReferenceEquals(_grid.ViewAt(slot), mushroom))
            {
                return;
            }

            Smoke(burst);
            mushroom.EmitSpores(burst);
        }

        // A cone, as capsules end to end along the burst's way: each wider than the last, and thinner.
        private void Smoke(in MushroomSporeBurst burst)
        {
            // A scope with no smoke field still lets the specks go.
            if (_smoke == null || burst.Smoke <= 0f || burst.Length <= 0f)
            {
                return;
            }

            var steps = burst.Kind == MushroomSporeKind.Blast ? BlastSteps : ConeSteps;
            var endRadius = Mathf.Max(ConeStartRadius, burst.EndWidth * 0.5f);
            Vector3 from = burst.Origin;
            for (var i = 1; i <= steps; i++)
            {
                var along = (float)i / steps;
                Vector3 to = burst.Origin + (burst.Direction * (burst.Length * along));
                _smoke.Paint(
                    PaintSource.MushroomSpores, to, from, Mathf.Lerp(ConeStartRadius, endRadius, along),
                    burst.Smoke * Mathf.Lerp(1f, ConeFarOpacity, along));
                from = to;
            }
        }

        private struct Waiting
        {
            public Vector2Int Slot;
            public MushroomView View;
            public MushroomSporeBurst Burst;
            public float Due;
        }
    }
}
