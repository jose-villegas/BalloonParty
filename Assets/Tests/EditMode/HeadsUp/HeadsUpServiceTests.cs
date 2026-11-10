using BalloonParty.Configuration.HeadsUp;
using BalloonParty.Game.HeadsUp;
using BalloonParty.Game.Level;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using static BalloonParty.Tests.HeadsUp.HeadsUpRouterTestKit;
using static BalloonParty.Tests.HeadsUp.HeadsUpTestSettings;
using CallKind = BalloonParty.Tests.HeadsUp.RecordingHeadsUpPresenter.CallKind;

namespace BalloonParty.Tests.HeadsUp
{
    /// <summary>
    ///     <see cref="HeadsUpService" /> between the queue and the presenter: what the presenter is told for each
    ///     answer, lines finishing freeing their slots, time standing still under a pause, and the clears when play
    ///     stops.
    /// </summary>
    [TestFixture]
    public class HeadsUpServiceTests
    {
        private const HeadsUpStyleId Line = HeadsUpStyleId.BounceWall;
        private const HeadsUpStyleId Loud = HeadsUpStyleId.Streak;
        private const HeadsUpStyleId Cooled = HeadsUpStyleId.SpeedTap;

        private static readonly object Ball = new();

        private RecordingHeadsUpPresenter _presenter;
        private PauseService _pause;
        private ReactiveProperty<LevelUpPhase> _phase;
        private ReactiveProperty<NavigationState> _navigation;
        private IMessageHandler<RunResetMessage> _onReset;
        private IMessageHandler<GameOverMessage> _onGameOver;
        private HeadsUpService _service;

        [SetUp]
        public void SetUp()
        {
            _presenter = new RecordingHeadsUpPresenter();
            _pause = new PauseService(Substitute.For<IPublisher<PausedMessage>>(),
                Substitute.For<IPublisher<ResumedMessage>>());
            _phase = new ReactiveProperty<LevelUpPhase>(LevelUpPhase.Playing);
            _navigation = new ReactiveProperty<NavigationState>(NavigationState.Game);
            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Phase.Returns(_phase);
            var navigation = Substitute.For<INavigation>();
            navigation.Current.Returns(_navigation);

            var settings = With(1, Style(Line), Style(Loud, 3), Style(Cooled).WithCooldown(1f));
            _service = new HeadsUpService(settings, _presenter, _pause, levelProgress, navigation,
                CaptureSubscriber<RunResetMessage>(h => _onReset = h),
                CaptureSubscriber<GameOverMessage>(h => _onGameOver = h));
            _service.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            _presenter.Dispose();
        }

        [Test]
        public void AShownRequest_IsHandedToThePresenter_WithItsStyle()
        {
            var handle = _service.Request(Request(Line));

            Assert.IsTrue(handle.IsValid);
            var shown = _presenter.Of(CallKind.Show);
            Assert.AreEqual(1, shown.Count);
            Assert.AreEqual(handle, shown[0].Handle);
            Assert.AreEqual(Line, shown[0].Request.Style);
            Assert.AreEqual(Line, shown[0].Style.Id);
        }

        [Test]
        public void ADroppedRequest_ReachesNoPresenter_AndGivesNoHandle()
        {
            _service.Request(Request(Loud));
            _presenter.Calls.Clear();

            var handle = _service.Request(Request(Line));

            Assert.IsFalse(handle.IsValid);
            Assert.IsEmpty(_presenter.Calls);
        }

        [Test]
        public void AnEviction_ReleasesTheOldLine_BeforeShowingTheNew()
        {
            var old = _service.Request(Request(Line));
            _presenter.Calls.Clear();

            var next = _service.Request(Request(Loud));

            Assert.AreEqual(2, _presenter.Calls.Count);
            Assert.AreEqual(CallKind.Release, _presenter.Calls[0].Kind);
            Assert.AreEqual(old, _presenter.Calls[0].Handle);
            Assert.AreEqual(CallKind.Show, _presenter.Calls[1].Kind);
            Assert.AreEqual(next, _presenter.Calls[1].Handle);
        }

        [Test]
        public void AMergedRequest_RewritesTheShowingLine()
        {
            var first = _service.Request(Request(Loud, Key(Ball, "Red"), 3));

            var second = _service.Request(Request(Loud, Key(Ball, "Red"), 4));

            Assert.AreEqual(first, second);
            var rewrites = _presenter.Of(CallKind.Rewrite);
            Assert.AreEqual(1, rewrites.Count);
            Assert.AreEqual(first, rewrites[0].Handle);
            Assert.AreEqual(4, rewrites[0].Request.Arg);
            Assert.AreEqual(1, _presenter.Of(CallKind.Show).Count);
        }

        [Test]
        public void Release_AShowingLine_FreesItsSlot_AndTellsThePresenter()
        {
            var handle = _service.Request(Request(Line));

            _service.Release(handle);

            Assert.AreEqual(handle, _presenter.Of(CallKind.Release)[0].Handle);
            Assert.IsTrue(_service.Request(Request(Line)).IsValid);
        }

        // The cannon releases its ready on every shot, including after a clear already took it.
        [Test]
        public void Release_AStaleHandle_IsIgnored()
        {
            var handle = _service.Request(Request(Line));
            _onReset.Handle(new RunResetMessage(false));

            _service.Release(handle);
            _service.Release(HeadsUpHandle.None);

            Assert.IsEmpty(_presenter.Of(CallKind.Release));
        }

        [Test]
        public void ALineFinishing_FreesItsSlot()
        {
            var handle = _service.Request(Request(Line));

            _presenter.Finish(handle);

            Assert.IsTrue(_service.Request(Request(Line)).IsValid);
            Assert.IsEmpty(_presenter.Of(CallKind.Release));
        }

        // An evicted line fades out after its slot went to another; its finish must not free the new line.
        [Test]
        public void AnEvictedLineFinishing_LeavesItsReplacementShowing()
        {
            var old = _service.Request(Request(Line));
            _service.Request(Request(Loud));

            _presenter.Finish(old);

            Assert.IsFalse(_service.Request(Request(Line)).IsValid);
        }

        [Test]
        public void Advance_HandsThePresenterLiveAndUnscaledTime()
        {
            _service.Advance(0.25f);

            var advance = _presenter.Of(CallKind.Advance)[0];
            Assert.AreEqual(0.25f, advance.LiveDelta);
            Assert.AreEqual(0.25f, advance.UnscaledDelta);
        }

        [Test]
        public void Advance_UnderAPause_HandsNoLiveTime()
        {
            _pause.Pause(PauseSource.Cinematic);

            _service.Advance(0.25f);

            var advance = _presenter.Of(CallKind.Advance)[0];
            Assert.AreEqual(0f, advance.LiveDelta);
            Assert.AreEqual(0.25f, advance.UnscaledDelta);
        }

        [Test]
        public void ACooldown_CountsOnlyLiveTime()
        {
            var first = _service.Request(Request(Cooled));
            _presenter.Finish(first);
            _pause.Pause(PauseSource.Cinematic);
            _service.Advance(5f);
            _pause.Resume(PauseSource.Cinematic);
            _service.Advance(0.5f);

            Assert.IsFalse(_service.Request(Request(Cooled)).IsValid);

            _service.Advance(0.5f);
            Assert.IsTrue(_service.Request(Request(Cooled)).IsValid);
        }

        [Test]
        public void ARunReset_ClearsAtOnce_AndFreesEverySlot()
        {
            _service.Request(Request(Loud));

            _onReset.Handle(new RunResetMessage(true));

            Assert.AreEqual(1, _presenter.Of(CallKind.ClearAtOnce).Count);
            Assert.IsTrue(_service.Request(Request(Line)).IsValid);
        }

        [Test]
        public void GameOver_ClearsWithAFade()
        {
            _onGameOver.Handle(new GameOverMessage(3, 100));

            Assert.AreEqual(1, _presenter.Of(CallKind.ClearAnimated).Count);
        }

        [Test]
        public void TheLevelUpPopup_ClearsWithAFade()
        {
            _phase.Value = LevelUpPhase.Completing;
            _phase.Value = LevelUpPhase.Pending;

            Assert.AreEqual(1, _presenter.Of(CallKind.ClearAnimated).Count);
        }

        // The winning pop's slow-motion beat, when the last lines matter most.
        [Test]
        public void TheLevelCompleting_KeepsTheLines()
        {
            _service.Request(Request(Line));

            _phase.Value = LevelUpPhase.Completing;

            Assert.IsEmpty(_presenter.Of(CallKind.ClearAnimated));
            Assert.IsTrue(_service.Request(Request(Loud)).IsValid);
        }

        // An item or trail resolving a frame after the level-up or game-over screen took over.
        [Test]
        public void ARequestOffTheGameScreen_IsDropped()
        {
            _navigation.Value = NavigationState.LevelUp;

            Assert.IsFalse(_service.Request(Request(Line)).IsValid);
            Assert.IsEmpty(_presenter.Of(CallKind.Show));
        }

        // A line the view can't place reports finished from inside Show, and must not keep its slot.
        [Test]
        public void ALineFinishedFromInsideShow_FreesItsSlot()
        {
            _presenter.FinishOnShow = true;
            _service.Request(Request(Loud));
            _presenter.FinishOnShow = false;

            Assert.IsTrue(_service.Request(Request(Line)).IsValid);
        }

        [Test]
        public void LeavingForTheLaunchScreen_ClearsAtOnce()
        {
            _navigation.Value = NavigationState.Launch;

            Assert.AreEqual(1, _presenter.Of(CallKind.ClearAtOnce).Count);
        }

        // Those screens follow a fading clear of their own, which an instant one would cut short.
        [Test]
        public void TheLevelUpOrGameOverScreen_DoesNotClear()
        {
            _navigation.Value = NavigationState.LevelUp;
            _navigation.Value = NavigationState.GameOver;

            Assert.IsEmpty(_presenter.Of(CallKind.ClearAtOnce));
            Assert.IsEmpty(_presenter.Of(CallKind.ClearAnimated));
        }

        private static HeadsUpRequest Request(HeadsUpStyleId style, HeadsUpMergeKey key = default, int arg = 1)
        {
            return new HeadsUpRequest(style, arg, HeadsUpAnchor.At(Vector2.zero), key);
        }

        private static HeadsUpMergeKey Key(object owner, string channel)
        {
            return new HeadsUpMergeKey(owner, channel);
        }
    }
}
