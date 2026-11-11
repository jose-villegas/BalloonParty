using System;
using System.Collections.Generic;
using BalloonParty.Configuration.CardReveal;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Random = System.Random;

namespace BalloonParty.Game.CardReveal
{
    /// <summary>
    ///     Queues a reveal for each card the player wins: a level-up's card as it's held
    ///     (<see cref="UpgradeCardHeldMessage" />) and a retry's bonus card as it's dealt
    ///     (<see cref="BonusCardDealtMessage" />). One plays at a time, and only over the board (in play, or a
    ///     level-up's ascent, where a level-up's card plays), so a bonus dealt behind the game-over screen waits
    ///     for play to resume. Each reveal gets a pop chosen at random,
    ///     never one of the last <see cref="ICardRevealSettings.RecentPopsAvoided" /> shown. Nothing is queued
    ///     while no stage is attached, so until the reveal's view exists the cards stay the tooltip's.
    /// </summary>
    internal sealed class CardRevealController : ICardReveals, IStartable, IRunResettable, IDisposable
    {
        private readonly ICardRevealSettings _settings;
        private readonly INavigation _navigation;
        private readonly ISubscriber<UpgradeCardHeldMessage> _heldSubscriber;
        private readonly ISubscriber<BonusCardDealtMessage> _bonusSubscriber;
        private readonly Random _random;
        private readonly Queue<(IUpgradeCardSettings Card, int Stacks, bool IsBonus)> _queue = new();
        private readonly List<int> _recentPops = new();
        private readonly ReactiveProperty<RevealedCard> _current = new();
        private readonly CompositeDisposable _subscriptions = new();

        private int _stages;

        // Before RunUpgrades (Counters) deals a retry's bonus card in the same reset, so clearing the old run's
        // reveals never drops the new one's.
        public int ResetOrder => RunResetOrder.Quiesce;
        public IReadOnlyReactiveProperty<RevealedCard> Current => _current;
        public bool HasStage => _stages > 0;

        [Inject]
        internal CardRevealController(ICardRevealSettings settings, INavigation navigation,
            ISubscriber<UpgradeCardHeldMessage> heldSubscriber, ISubscriber<BonusCardDealtMessage> bonusSubscriber)
            : this(settings, navigation, heldSubscriber, bonusSubscriber, new Random())
        {
        }

        internal CardRevealController(ICardRevealSettings settings, INavigation navigation,
            ISubscriber<UpgradeCardHeldMessage> heldSubscriber, ISubscriber<BonusCardDealtMessage> bonusSubscriber,
            Random random)
        {
            _settings = settings;
            _navigation = navigation;
            _heldSubscriber = heldSubscriber;
            _bonusSubscriber = bonusSubscriber;
            _random = random;
        }

        public void Start()
        {
            _heldSubscriber.Subscribe(message => Enqueue(message.Card, message.Stacks, isBonus: false))
                .AddTo(_subscriptions);
            _bonusSubscriber.Subscribe(message => Enqueue(message.Card, message.Stacks, isBonus: true))
                .AddTo(_subscriptions);
            _navigation.Current.Where(IsOnStage)
                .Subscribe(_ => TryStartNext())
                .AddTo(_subscriptions);
        }

        // The recent pops are kept: a new run's first reveal shouldn't repeat the last run's either.
        public void ResetRun(int generation)
        {
            _queue.Clear();
            _current.Value = null;
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
            _current.Dispose();
        }

        public void AttachStage()
        {
            _stages++;
            TryStartNext();
        }

        // The last stage leaving takes the queue with it: with no stage, nothing would ever finish them.
        public void DetachStage()
        {
            _stages = Mathf.Max(0, _stages - 1);
            if (_stages == 0)
            {
                _queue.Clear();
                _current.Value = null;
            }
        }

        public void Finish(RevealedCard done)
        {
            if (done == null || !ReferenceEquals(done, _current.Value))
            {
                return;
            }

            _current.Value = null;
            TryStartNext();
        }

        private void Enqueue(IUpgradeCardSettings card, int stacks, bool isBonus)
        {
            if (!HasStage || card == null)
            {
                return;
            }

            _queue.Enqueue((card, stacks, isBonus));
            TryStartNext();
        }

        // Play, or a level-up's ceremony and ascent, which is where a level-up's card plays: over the board, with
        // nothing in front of it. Not the launch or game-over screens. A reveal already queued (a bonus card) can so
        // start during the ceremony, before the level-up's own card: with the pick screen off, nothing covers it.
        private static bool IsOnStage(NavigationState state)
        {
            return state == NavigationState.Game || state == NavigationState.LevelUp;
        }

        private void TryStartNext()
        {
            if (_current.Value != null || _queue.Count == 0 || !IsOnStage(_navigation.Current.Value))
            {
                return;
            }

            var (card, stacks, isBonus) = _queue.Dequeue();
            _current.Value = new RevealedCard(card, stacks, isBonus, PickPop());
        }

        // Uniform over the pops not among the last few shown; those stay distinct, since each pick skips them.
        private int PickPop()
        {
            var count = _settings.PopCount;
            var avoided = Mathf.Min(_settings.RecentPopsAvoided, count - 1);
            while (_recentPops.Count > avoided)
            {
                _recentPops.RemoveAt(0);
            }

            var pick = _random.Next(count - _recentPops.Count);
            var pop = 0;
            for (; pop < count; pop++)
            {
                if (_recentPops.Contains(pop))
                {
                    continue;
                }

                if (pick-- == 0)
                {
                    break;
                }
            }

            if (avoided > 0)
            {
                _recentPops.Add(pop);
                if (_recentPops.Count > avoided)
                {
                    _recentPops.RemoveAt(0);
                }
            }

            return pop;
        }
    }
}
