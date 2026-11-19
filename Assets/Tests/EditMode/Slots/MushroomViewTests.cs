using System;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor.Archetype;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BalloonParty.Tests.Slots
{
    /// <summary>
    ///     A mushroom on the board (<see cref="MushroomView" />, @ref plan_meadow_mushrooms): it answers the bounce
    ///     off its own slot, hands the shader the cap's lean, ticks only while the cap moves, and leaves nothing
    ///     running when it goes back to the pool. Edit mode runs no <c>Awake</c> or <c>Update</c>, so the tests
    ///     call the frame themselves.
    /// </summary>
    [TestFixture]
    public class MushroomViewTests
    {
        private const float Frame = 1f / 60f;
        private const float BallSpeed = 10f;

        private static readonly int LeanId = Shader.PropertyToID("_Lean");
        private static readonly int PulseId = Shader.PropertyToID("_Pulse");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int CapGrowId = Shader.PropertyToID("_CapGrow");
        private static readonly int CutLineId = Shader.PropertyToID("_CutLine");
        private static readonly int CutLeanId = Shader.PropertyToID("_CutLean");
        private static readonly int CutPiecesId = Shader.PropertyToID("_CutPieces");
        private static readonly int CutFallId = Shader.PropertyToID("_CutFall");
        private static readonly Vector2Int Slot = new(2, 4);
        private static readonly Vector2Int OtherSlot = new(4, 6);
        private static readonly Vector3 MushroomAt = new(1.5f, -0.75f, 0f);

        private GameObject _mushroom;
        private GameObject _other;
        private SpriteRenderer _body;
        private MushroomView _view;
        private MushroomSettings _settings;
        private SlotGrid _grid;
        private IMessageHandler<MushroomBouncedMessage> _bounced;
        private IMessageHandler<MushroomCutMessage> _cutBy;
        private IMessageHandler<MushroomRegrownMessage> _regrown;
        private IDisposable _subscription;
        private IDisposable _cutSubscription;
        private IDisposable _regrownSubscription;
        private IMessageHandler<MushroomSwayedMessage> _swayed;
        private IDisposable _swayedSubscription;
        private MaterialPropertyBlock _block;

        [SetUp]
        public void SetUp()
        {
            var config = Substitute.For<ISlotGridConfig>();
            config.SlotsSize.Returns(MushroomTestBoard.GridSize);
            config.SlotSeparation.Returns(new Vector2(0.375f, 0.65f));
            config.SlotsOffset.Returns(new Vector2(5f, 3.25f));
            _grid = new SlotGrid(config, new BalancePathHolder());
            _settings = new MushroomSettings();
            _block = new MaterialPropertyBlock();

            _subscription = Substitute.For<IDisposable>();
            var subscriber = Substitute.For<ISubscriber<MushroomBouncedMessage>>();
            subscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<MushroomBouncedMessage>>(handler => _bounced = handler),
                    Arg.Any<MessageHandlerFilter<MushroomBouncedMessage>[]>())
                .Returns(_subscription);
            _cutSubscription = Substitute.For<IDisposable>();
            var cutSubscriber = Substitute.For<ISubscriber<MushroomCutMessage>>();
            cutSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<MushroomCutMessage>>(handler => _cutBy = handler),
                    Arg.Any<MessageHandlerFilter<MushroomCutMessage>[]>())
                .Returns(_cutSubscription);
            _regrownSubscription = Substitute.For<IDisposable>();
            var regrownSubscriber = Substitute.For<ISubscriber<MushroomRegrownMessage>>();
            regrownSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<MushroomRegrownMessage>>(handler => _regrown = handler),
                    Arg.Any<MessageHandlerFilter<MushroomRegrownMessage>[]>())
                .Returns(_regrownSubscription);
            _swayedSubscription = Substitute.For<IDisposable>();
            var swayedSubscriber = Substitute.For<ISubscriber<MushroomSwayedMessage>>();
            swayedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<MushroomSwayedMessage>>(handler => _swayed = handler),
                    Arg.Any<MessageHandlerFilter<MushroomSwayedMessage>[]>())
                .Returns(_swayedSubscription);

            _mushroom = new GameObject("Mushroom");
            _mushroom.transform.position = MushroomAt;
            _body = _mushroom.AddComponent<SpriteRenderer>();
            _view = _mushroom.AddComponent<MushroomView>();
            SetField("_body", _body);
            _view.Construct(_settings, subscriber, cutSubscriber, regrownSubscriber, swayedSubscriber, _grid);
            _view.OnSpawned();
            _grid.Place(new MushroomBumperModel(0), _view, Slot);

            _other = new GameObject("Other mushroom");
            _grid.Place(new MushroomBumperModel(1), _other.AddComponent<MushroomView>(), OtherSlot);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_mushroom);
            Object.DestroyImmediate(_other);
        }

        [Test]
        public void ASpawnedMushroom_IsSwitchedOff_AndDrawnAtRest()
        {
            Assert.IsFalse(_view.enabled, "a resting mushroom has no Update");
            Assert.AreEqual(Vector2.zero, _view.Lean);
            Assert.AreEqual(Vector4.zero, Pushed(LeanId));
            Assert.AreEqual(_settings.Radius, PushedFloat(RadiusId), "the shader draws the settings' radius");
        }

        [Test]
        public void ABounceOffItsOwnSlot_SwitchesItOn_AndLeansTheCapTheWayTheBallWasGoing()
        {
            var incoming = new Vector2(1f, 0f);

            _bounced.Handle(Bounce(Slot, incoming, MushroomReward.None));
            Assert.IsTrue(_view.enabled);
            for (var i = 0; i < 4; i++)
            {
                _view.Tick(Frame);
            }

            Assert.Greater(_view.Lean.x, 0.03f, "hit from the left, the cap leans right");
            Assert.AreEqual(0f, _view.Lean.y, 1e-5f);
            var pushed = Pushed(LeanId);
            Assert.AreEqual(_view.Lean.x, pushed.x, 1e-6f, "what the shader draws is what the spring holds");
            Assert.AreEqual(_view.Lean.y, pushed.y, 1e-6f);
            Assert.AreEqual(0f, PushedFloat(PulseId), "a bounce that paid nothing does not swell the cap");
        }

        [Test]
        public void ABounceFromBelow_LeansTheCapUp()
        {
            _bounced.Handle(Bounce(Slot, new Vector2(0f, 1f), MushroomReward.None));
            for (var i = 0; i < 4; i++)
            {
                _view.Tick(Frame);
            }

            Assert.Greater(_view.Lean.y, 0.03f);
            Assert.AreEqual(0f, _view.Lean.x, 1e-5f);
        }

        [Test]
        public void ABounceOffAnotherMushroom_LeavesItAsleep()
        {
            _bounced.Handle(Bounce(OtherSlot, Vector2.right, MushroomReward.Arm));

            Assert.IsFalse(_view.enabled);
            Assert.AreEqual(Vector2.zero, _view.Lean);
        }

        [Test]
        public void ABounceThatArms_SwellsTheCapToo()
        {
            _bounced.Handle(Bounce(Slot, Vector2.right, MushroomReward.Arm));
            for (var i = 0; i < 4; i++)
            {
                _view.Tick(Frame);
            }

            Assert.Greater(PushedFloat(PulseId), 0.2f);
        }

        [Test]
        public void OnceTheCapIsStill_TheViewSwitchesItselfOff_AndIsDrawnAtRest()
        {
            _bounced.Handle(Bounce(Slot, new Vector2(0.6f, 0.8f), MushroomReward.Arm));

            var frames = 0;
            while (_view.enabled)
            {
                _view.Tick(Frame);
                frames++;
                Assert.Less(frames, 600, "still ticking after ten seconds");
            }

            Assert.Greater(frames, 20);
            Assert.AreEqual(Vector2.zero, _view.Lean);
            Assert.AreEqual(Vector4.zero, Pushed(LeanId));
            Assert.AreEqual(0f, PushedFloat(PulseId));
        }

        [Test]
        public void AnIdleCap_IsHandedItsBreathAndItsRocking_AndTheViewStaysOff()
        {
            var idle = _settings.Idle;

            Assert.IsFalse(_view.enabled, "the breath and the rocking are the shader's: nothing ticks for them");
            Assert.AreEqual(idle.BreathVector, Pushed(Shader.PropertyToID("_Breath")));
            Assert.AreEqual(idle.LookVector, Pushed(Shader.PropertyToID("_BreathLook")));
            Assert.AreEqual(idle.RockVector(_settings.Radius), Pushed(Shader.PropertyToID("_Rock")));
        }

        [Test]
        public void ASecondHit_WakesItAgain()
        {
            _bounced.Handle(Bounce(Slot, Vector2.right, MushroomReward.None));
            while (_view.enabled)
            {
                _view.Tick(Frame);
            }

            _bounced.Handle(Bounce(Slot, Vector2.left, MushroomReward.None));
            Assert.IsTrue(_view.enabled);
            _view.Tick(Frame);
            _view.Tick(Frame);

            Assert.Less(_view.Lean.x, 0f, "and leans the new way");
        }

        [Test]
        public void BackInThePool_NothingIsLeftRunning()
        {
            _bounced.Handle(Bounce(Slot, Vector2.right, MushroomReward.Arm));
            _view.Tick(Frame);
            _view.Tick(Frame);

            _view.OnDespawned();

            Assert.IsFalse(_view.enabled, "no Update");
            _subscription.Received(1).Dispose();
            _cutSubscription.Received(1).Dispose();
            _regrownSubscription.Received(1).Dispose();
            _swayedSubscription.Received(1).Dispose();
            Assert.AreEqual(Vector2.zero, _view.Lean, "the spring is reset");
            Assert.AreEqual(Vector4.zero, Pushed(LeanId), "and the next level's mushroom is not drawn mid wobble");
            Assert.AreEqual(0f, PushedFloat(PulseId));
        }

        [Test]
        public void SpawnedAgain_ItListensAgain_FromRest()
        {
            _bounced.Handle(Bounce(Slot, Vector2.right, MushroomReward.None));
            _view.Tick(Frame);
            _view.OnDespawned();
            _bounced = null;

            _view.OnSpawned();

            Assert.IsNotNull(_bounced, "subscribed again");
            Assert.IsFalse(_view.enabled);
            Assert.AreEqual(Vector2.zero, _view.Lean);
        }

        [Test]
        public void ASpawnedMushroom_IsWhole()
        {
            Assert.IsFalse(_view.IsCut);
            Assert.AreEqual(1f, PushedFloat(CapGrowId), "the whole cap is drawn");
            Assert.AreEqual(0f, Pushed(CutFallId).x, "and no pieces: the shader skips the cut's work");
        }

        [Test]
        public void ACutOfItsOwnSlot_TakesTheCapOff_SwitchesItOn_AndHandsTheShaderTheLine()
        {
            var through = (Vector2)MushroomAt + new Vector2(0.1f, 0.05f);
            var way = new Vector2(0.6f, 0.8f);

            _cutBy.Handle(new MushroomCutMessage(Slot, through, way));

            Assert.IsTrue(_view.IsCut);
            Assert.IsTrue(_view.enabled, "the pieces have to fall");
            Assert.AreEqual(0f, PushedFloat(CapGrowId), "the whole cap is gone");
            Assert.AreEqual(1f, Pushed(CutFallId).x, "and its pieces are there");
            var line = Pushed(CutLineId);
            Assert.AreEqual(0.1f, line.x, 1e-5f, "the cut's point, in the quad's own space");
            Assert.AreEqual(0.05f, line.y, 1e-5f);
            Assert.AreEqual(0.6f, line.z, 1e-5f, "and the lance's way");
            Assert.AreEqual(0.8f, line.w, 1e-5f);
        }

        [Test]
        public void ACutOfAnotherMushroom_LeavesItWhole()
        {
            _cutBy.Handle(new MushroomCutMessage(OtherSlot, Vector2.zero, Vector2.right));

            Assert.IsFalse(_view.IsCut);
            Assert.IsFalse(_view.enabled);
            Assert.AreEqual(1f, PushedFloat(CapGrowId));
        }

        [Test]
        public void ThePiecesPart_ThenTheViewSwitchesItselfOff_OnABareStem()
        {
            _cutBy.Handle(new MushroomCutMessage(Slot, MushroomAt, Vector2.right));

            for (var i = 0; i < 6; i++)
            {
                _view.Tick(Frame);
            }

            Assert.Greater(Pushed(CutPiecesId).x, 0.05f, "the pieces have moved off the line");
            Assert.Greater(Pushed(CutPiecesId).z, 0f, "and turned");

            var frames = 6;
            while (_view.enabled)
            {
                _view.Tick(Frame);
                frames++;
                Assert.Less(frames, 600, "still ticking after ten seconds");
            }

            Assert.AreEqual(_settings.Cut.FallSeconds, frames * Frame, 3f * Frame);
            Assert.IsTrue(_view.IsCut, "a bare stem costs nothing a frame, and stays cut");
            Assert.AreEqual(0f, PushedFloat(CapGrowId));
            Assert.AreEqual(0f, Pushed(CutFallId).x, "the pieces are gone, and their work with them");
        }

        [Test]
        public void AHitOnACutMushroom_DoesNothing()
        {
            _cutBy.Handle(new MushroomCutMessage(Slot, MushroomAt, Vector2.right));
            while (_view.enabled)
            {
                _view.Tick(Frame);
            }

            _bounced.Handle(Bounce(Slot, Vector2.right, MushroomReward.Arm));

            Assert.IsFalse(_view.enabled, "nothing bounces off a bare stem");
            Assert.AreEqual(Vector2.zero, _view.Lean);
            Assert.AreEqual(0f, PushedFloat(PulseId));
        }

        [Test]
        public void ACutMidLean_StartsThePiecesWhereTheCapStood_AndLeavesTheStemStraight()
        {
            _bounced.Handle(Bounce(Slot, Vector2.right, MushroomReward.None));
            for (var i = 0; i < 4; i++)
            {
                _view.Tick(Frame);
            }

            var leaning = _view.Lean;
            Assert.Greater(leaning.x, 0.03f);

            _cutBy.Handle(new MushroomCutMessage(Slot, MushroomAt, Vector2.up));

            var cutLean = Pushed(CutLeanId);
            Assert.AreEqual(leaning.x, cutLean.x, 1e-6f, "the pieces are pieces of the cap as it stood: nothing jumps");
            Assert.AreEqual(leaning.y, cutLean.y, 1e-6f);
            Assert.AreEqual(Vector2.zero, _view.Lean, "and the spring is still");
            Assert.AreEqual(Vector4.zero, Pushed(LeanId));
        }

        [Test]
        public void ACutAsTheCapPopsBack_StartsThePiecesAtTheSizeItHad()
        {
            _cutBy.Handle(new MushroomCutMessage(Slot, MushroomAt, Vector2.right));
            Assert.AreEqual(1f, Pushed(CutLeanId).z, "a whole cap's pieces are full size");
            while (_view.enabled)
            {
                _view.Tick(Frame);
            }

            _regrown.Handle(new MushroomRegrownMessage(Slot));
            _view.Tick(Frame);
            _view.Tick(Frame);
            var grown = PushedFloat(CapGrowId);
            Assert.That(grown, Is.InRange(0.05f, 0.9f), "part of the way back");

            _cutBy.Handle(new MushroomCutMessage(Slot, MushroomAt, Vector2.up));

            Assert.IsTrue(_view.IsCut);
            Assert.AreEqual(grown, Pushed(CutLeanId).z, 1e-6f, "the pieces are of the cap as it stood: nothing pops");

            _view.OnDespawned();
            Assert.AreEqual(1f, Pushed(CutLeanId).z, "and the next mushroom's are full size again");
        }

        [Test]
        public void ACapAlreadyCut_IsNotCutAgain()
        {
            _cutBy.Handle(new MushroomCutMessage(Slot, MushroomAt, Vector2.right));
            while (_view.enabled)
            {
                _view.Tick(Frame);
            }

            _cutBy.Handle(new MushroomCutMessage(Slot, MushroomAt, Vector2.up));

            Assert.IsFalse(_view.enabled, "there is nothing left to cut");
            Assert.AreEqual(0f, Pushed(CutFallId).x);
        }

        [Test]
        public void ARegrowOfItsOwnSlot_PopsTheCapBack_ThenTheViewSwitchesItselfOff_Whole()
        {
            _cutBy.Handle(new MushroomCutMessage(Slot, MushroomAt, Vector2.right));
            while (_view.enabled)
            {
                _view.Tick(Frame);
            }

            _regrown.Handle(new MushroomRegrownMessage(OtherSlot));
            Assert.IsTrue(_view.IsCut, "another mushroom's regrow is not this one's");

            _regrown.Handle(new MushroomRegrownMessage(Slot));
            Assert.IsFalse(_view.IsCut);
            Assert.IsTrue(_view.enabled);

            var largest = 0f;
            var frames = 0;
            while (_view.enabled)
            {
                _view.Tick(Frame);
                largest = Mathf.Max(largest, PushedFloat(CapGrowId));
                frames++;
                Assert.Less(frames, 600, "still ticking after ten seconds");
            }

            Assert.Greater(largest, 1.02f, "it pops a little past its size");
            Assert.AreEqual(1f, PushedFloat(CapGrowId), "and ends exactly whole");
            Assert.AreEqual(_settings.Cut.RegrowSeconds, frames * Frame, 3f * Frame);
        }

        [Test]
        public void ARegrowOfAWholeMushroom_LeavesItAsleep()
        {
            _regrown.Handle(new MushroomRegrownMessage(Slot));

            Assert.IsFalse(_view.enabled);
            Assert.AreEqual(1f, PushedFloat(CapGrowId));
        }

        [Test]
        public void BackInThePoolMidFall_TheCapIsWhole_AndSoIsTheNextSpawn()
        {
            _cutBy.Handle(new MushroomCutMessage(Slot, MushroomAt, Vector2.right));
            _view.Tick(Frame);
            _view.Tick(Frame);

            _view.OnDespawned();

            Assert.IsFalse(_view.enabled);
            Assert.IsFalse(_view.IsCut, "the next level's mushroom is not a stem");
            Assert.AreEqual(1f, PushedFloat(CapGrowId));
            Assert.AreEqual(0f, Pushed(CutFallId).x);

            _view.OnSpawned();

            Assert.IsFalse(_view.IsCut);
            Assert.AreEqual(1f, PushedFloat(CapGrowId));
            Assert.IsNotNull(_cutBy, "and it listens for a cut again");
        }

        [Test]
        public void ABlast_ThrowsTheCapOffWhole_OnePiece_AwayFromTheBlast_WithNoCutBand()
        {
            var away = new Vector2(0.6f, 0.8f);

            _cutBy.Handle(new MushroomCutMessage(Slot, MushroomAt, away, MushroomCutCause.Blast));

            Assert.IsTrue(_view.IsCut, "the stem is bare, as after a lance");
            Assert.IsTrue(_view.enabled);
            Assert.AreEqual(0f, PushedFloat(CapGrowId));
            Assert.AreEqual(1f, Pushed(CutFallId).x, "the cap that came off is there");
            Assert.AreEqual(1f, Pushed(CutFallId).z, "and the shader is told it is one piece");

            // The cap is the piece on the left of a line that lies behind it, across the throw: so the shader's
            // move off the line is a move along the throw, and no point of the cap is on the line's band of flesh.
            var line = Pushed(CutLineId);
            var way = new Vector2(line.z, line.w);
            var left = new Vector2(-way.y, way.x);
            Assert.AreEqual(away.x, left.x, 1e-5f, "the line's left is the way of the throw");
            Assert.AreEqual(away.y, left.y, 1e-5f);
            var behind = Vector2.Dot(new Vector2(line.x, line.y), left);
            Assert.Less(behind, -_settings.Radius * 1.1f, "the line stands clear of the cap's rim: no cut band");

            for (var i = 0; i < 12; i++)
            {
                _view.Tick(Frame);
            }

            var pieces = Pushed(CutPiecesId);
            Assert.Greater(pieces.x, _settings.Cut.PartDistance, "thrown further than a cut piece parts");
            Assert.AreEqual(0f, pieces.y, "and not carried sideways");
            Assert.Greater(pieces.w, 1f, "lifted: seen from above it is larger");
            Assert.Greater(Pushed(CutFallId).y, 1f, "and its shadow lies further off");
            Assert.Less(pieces.z, 0f, "thrown to the right it tumbles clockwise");
            Assert.AreEqual(
                -_settings.Radius, Vector2.Dot((Vector2)Pushed(CutLineId), left), 0.02f,
                "once it is up, the line touches its trailing rim, where its underside shows");
        }

        [Test]
        public void ABlownCap_Lands_ThenTheViewSwitchesItselfOff_OnABareStem()
        {
            _cutBy.Handle(new MushroomCutMessage(Slot, MushroomAt, Vector2.left, MushroomCutCause.Blast));

            var frames = 0;
            while (_view.enabled)
            {
                _view.Tick(Frame);
                frames++;
                Assert.Less(frames, 600, "still ticking after ten seconds");
            }

            Assert.AreEqual(_settings.Cut.Blown.Seconds, frames * Frame, 3f * Frame);
            Assert.IsTrue(_view.IsCut);
            Assert.AreEqual(0f, Pushed(CutFallId).x, "the cap is gone, and its work with it");
            Assert.AreEqual(0f, Pushed(CutFallId).z, "and the next cut is two pieces again");

            _regrown.Handle(new MushroomRegrownMessage(Slot));
            Assert.IsFalse(_view.IsCut, "it grows back as a cut cap does");
        }

        [Test]
        public void ABlastOnAnotherMushroom_OrOnACapAlreadyOff_DoesNothing()
        {
            _cutBy.Handle(new MushroomCutMessage(OtherSlot, Vector2.zero, Vector2.up, MushroomCutCause.Blast));
            Assert.IsFalse(_view.IsCut);

            _cutBy.Handle(new MushroomCutMessage(Slot, MushroomAt, Vector2.right));
            while (_view.enabled)
            {
                _view.Tick(Frame);
            }

            _cutBy.Handle(new MushroomCutMessage(Slot, MushroomAt, Vector2.up, MushroomCutCause.Blast));

            Assert.IsFalse(_view.enabled, "there is no cap left to blow off");
            Assert.AreEqual(0f, Pushed(CutFallId).x);
        }

        [TestCase(1f, 0f)]
        [TestCase(0f, -1f)]
        [TestCase(-0.6f, 0.8f)]
        public void ASway_LeansTheCapAwayFromTheBlast_ThenSwingsItBackTowardIt(float x, float y)
        {
            var away = new Vector2(x, y);

            _swayed.Handle(new MushroomSwayedMessage(Slot, away, 1f));

            Assert.IsTrue(_view.enabled);
            var furthest = 0f;
            var back = 0f;
            for (var i = 0; i < 40; i++)
            {
                _view.Tick(Frame);
                var along = Vector2.Dot(_view.Lean, away);
                furthest = Mathf.Max(furthest, along);
                back = Mathf.Min(back, along);
                Assert.AreEqual(0f, Vector2.Dot(_view.Lean, new Vector2(-away.y, away.x)), 1e-4f, "on the blast's line");
            }

            Assert.AreEqual(_settings.Lean.SwayLean, furthest, _settings.Lean.SwayLean * 0.08f, "the tuned sway");
            Assert.Less(back, -_settings.Lean.SwayLean * 0.3f, "and back past rest, toward the blast");
            Assert.AreEqual(0f, PushedFloat(PulseId), "a sway pays nothing: no swell");
        }

        [Test]
        public void AFainterSway_LeansLess_AndAnothersSwayNotAtAll()
        {
            _swayed.Handle(new MushroomSwayedMessage(OtherSlot, Vector2.right, 1f));
            Assert.IsFalse(_view.enabled, "another mushroom's sway is not this one's");

            _swayed.Handle(new MushroomSwayedMessage(Slot, Vector2.right, 0.5f));

            var furthest = 0f;
            for (var i = 0; i < 20; i++)
            {
                _view.Tick(Frame);
                furthest = Mathf.Max(furthest, _view.Lean.x);
            }

            Assert.AreEqual(_settings.Lean.SwayLean * 0.5f, furthest, _settings.Lean.SwayLean * 0.05f);
        }

        [Test]
        public void ABareStem_DoesNotSway_CutOrBlown()
        {
            _cutBy.Handle(new MushroomCutMessage(Slot, MushroomAt, Vector2.up, MushroomCutCause.Blast));
            _view.Tick(Frame);

            _swayed.Handle(new MushroomSwayedMessage(Slot, Vector2.right, 1f));
            _view.Tick(Frame);

            Assert.AreEqual(Vector2.zero, _view.Lean, "a cap in the air has no stem to swing on");
            while (_view.enabled)
            {
                _view.Tick(Frame);
            }

            _swayed.Handle(new MushroomSwayedMessage(Slot, Vector2.right, 1f));

            Assert.IsFalse(_view.enabled, "and a bare stem stays asleep");
            Assert.AreEqual(Vector2.zero, _view.Lean);
        }

        [Test]
        public void ABlastMidLean_ThrowsTheCapFromWhereItStood()
        {
            _swayed.Handle(new MushroomSwayedMessage(Slot, Vector2.right, 1f));
            for (var i = 0; i < 4; i++)
            {
                _view.Tick(Frame);
            }

            var leaning = _view.Lean;
            Assert.Greater(leaning.x, 0.03f);

            _cutBy.Handle(new MushroomCutMessage(Slot, MushroomAt, Vector2.up, MushroomCutCause.Blast));

            Assert.AreEqual(leaning.x, Pushed(CutLeanId).x, 1e-6f, "nothing jumps");
            Assert.AreEqual(Vector2.zero, _view.Lean, "and the stem is left straight");
            Assert.AreEqual(leaning.x, Pushed(CutLineId).x, 1e-5f, "the line lies behind the cap where it stood");
        }

        [Test]
        public void ItsSpecks_LeaveFromTheBurstsOrigin_InsideItsFan_GreenishToBluish_AndGoWithTheMushroom()
        {
            var spores = Spores();
            var origin = (Vector2)MushroomAt + new Vector2(0.3f, 0f);
            var burst = new MushroomSporeBurst(
                MushroomSporeKind.Hit, origin, Vector2.right, 0.6f, 0.3f, 0.5f, 5, 2.4f, 28f, 0f);

            _view.EmitSpores(burst);

            var specks = new ParticleSystem.Particle[16];
            var count = spores.GetParticles(specks);
            Assert.AreEqual(5, count, "as many specks as the burst asks");
            var tuning = _settings.Spores;
            for (var i = 0; i < count; i++)
            {
                Vector2 velocity = specks[i].velocity;
                Assert.LessOrEqual(Vector2.Angle(Vector2.right, velocity), 14.5f, "inside the cone");
                Assert.That(velocity.magnitude, Is.InRange(2.4f * 0.6f, 2.4f * 1.4f));
                Assert.Less(((Vector2)specks[i].position - origin).magnitude, 0.1f, "from the cap's far rim");
                Assert.That(specks[i].startLifetime, Is.InRange(tuning.SpeckSeconds * 0.6f, tuning.SpeckSeconds * 1.4f));
                Color colour = specks[i].startColor;
                Assert.Greater(colour.g, colour.r, "greenish to bluish, never warm");
                Assert.That(colour.b, Is.InRange(tuning.SpeckGreen.b - 0.02f, tuning.SpeckBlue.b + 0.02f));
            }

            _view.OnDespawned();

            Assert.AreEqual(0, spores.particleCount, "back in the pool it has no specks in the air");
        }

        [Test]
        public void OfManySpecks_MostAreSmall_AndAFewBigOnesLead()
        {
            var spores = Spores();
            var burst = new MushroomSporeBurst(
                MushroomSporeKind.Blast, MushroomAt, Vector2.up, 1f, 0.9f, 1f, 60, 3f, 170f, 0f);

            // Awake seeds the view's rolls, and edit mode runs none: unseeded, every roll is 0.
            typeof(MushroomView)
                .GetField("_sporeRoll", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(_view, new XorShift32(7));

            _view.EmitSpores(burst);

            var specks = new ParticleSystem.Particle[64];
            var count = spores.GetParticles(specks);
            Assert.AreEqual(60, count);
            var size = _settings.Spores.SpeckSize;
            var small = 0;
            var big = 0;
            for (var i = 0; i < count; i++)
            {
                Assert.That(specks[i].startSize, Is.InRange(size * 0.59f, size * 1.91f));
                small += specks[i].startSize < size ? 1 : 0;
                big += specks[i].startSize > size * 1.4f ? 1 : 0;
            }

            Assert.Greater(small, count / 2, "most are under the size the settings name");
            Assert.That(big, Is.InRange(2, count / 4), "a few are well over it");
        }

        [Test]
        public void AMushroomWithNoSpecksOfItsOwn_LetsNoneGo_AndDoesNotThrow()
        {
            Assert.DoesNotThrow(() => _view.EmitSpores(new MushroomSporeBurst(
                MushroomSporeKind.Blast, MushroomAt, Vector2.up, 1f, 0.9f, 1f, 22, 3f, 150f, 0.3f)));
        }

        private ParticleSystem Spores()
        {
            var holder = new GameObject("Spores");
            holder.transform.SetParent(_mushroom.transform, false);
            var spores = holder.AddComponent<ParticleSystem>();
            spores.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = spores.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            var emission = spores.emission;
            emission.enabled = false;
            SetField("_spores", spores);
            return spores;
        }

        private MushroomBouncedMessage Bounce(Vector2Int slot, Vector2 incoming, MushroomReward reward)
        {
            return new MushroomBouncedMessage(null, slot, 0, Vector3.zero, incoming, BallSpeed, 1, reward);
        }

        private Vector4 Pushed(int property)
        {
            _body.GetPropertyBlock(_block);
            return _block.GetVector(property);
        }

        private float PushedFloat(int property)
        {
            _body.GetPropertyBlock(_block);
            return _block.GetFloat(property);
        }

        private void SetField(string name, Object value)
        {
            typeof(MushroomView)
                .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(_view, value);
        }
    }
}
