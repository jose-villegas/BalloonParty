using System;
using BalloonParty.Audio;
using BalloonParty.Audio.Routing;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Items;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class ItemSoundRouterTests
    {
        private ISoundPlayer _player;
        private IMessageHandler<ItemActivatedMessage> _itemActivatedHandler;
        private IMessageHandler<OverflowHeartRequestedMessage> _overflowHeartHandler;
        private IMessageHandler<ProjectileLoadedMessage> _loadedHandler;

        [SetUp]
        public void SetUp()
        {
            _player = Substitute.For<ISoundPlayer>();

            var itemActivatedSubscriber = CaptureSubscriber<ItemActivatedMessage>(h => _itemActivatedHandler = h);
            var overflowHeartSubscriber = CaptureSubscriber<OverflowHeartRequestedMessage>(h => _overflowHeartHandler = h);
            var loadedSubscriber = CaptureSubscriber<ProjectileLoadedMessage>(h => _loadedHandler = h);

            var router = new ItemSoundRouter(_player, itemActivatedSubscriber, overflowHeartSubscriber, loadedSubscriber);
            router.Start();
        }

        // Not a [TestCase]-parameterized method: NUnit only runs public test methods, but a public
        // method can't take the internal GameSoundId as a parameter (CS0051). Looping the pairs in a
        // public [Test] keeps full coverage — a mismatch still names the missing Play(id) call.
        // Each item type is tested in isolation (fresh flight) so the pitch-ramp counter is always 0.
        [Test]
        public void OnItemActivated_ItemType_PlaysMatchingSoundId()
        {
            (ItemType Item, GameSoundId Expected)[] cases =
            {
                (ItemType.Bomb, GameSoundId.ItemBomb),
                (ItemType.Laser, GameSoundId.ItemLaser),
                (ItemType.Lightning, GameSoundId.ItemLightning),
                (ItemType.Paint, GameSoundId.ItemPaint),
                (ItemType.Snipe, GameSoundId.ItemSnipe),
                (ItemType.Shield, GameSoundId.ItemShield),
            };

            foreach (var (item, expected) in cases)
            {
                // Reset the per-flight counter so each type is tested at offset 0.
                _loadedHandler.Handle(new ProjectileLoadedMessage(null));

                var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
                balloon.Item.Value = item;

                _itemActivatedHandler.Handle(new ItemActivatedMessage(balloon));

                _player.Received().Play(expected, null, null, 0, 1f);
            }
        }

        [Test]
        public void OnItemActivated_PitchRamps_FirstPickupAtZeroSemitones()
        {
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Item.Value = ItemType.Bomb;

            _itemActivatedHandler.Handle(new ItemActivatedMessage(balloon));

            _player.Received(1).Play(GameSoundId.ItemBomb, null, null, 0, 1f);
        }

        [Test]
        public void OnItemActivated_PitchRamps_SecondPickupAtTwoSemitones()
        {
            var b1 = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            b1.Item.Value = ItemType.Bomb;
            var b2 = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            b2.Item.Value = ItemType.Laser;

            _itemActivatedHandler.Handle(new ItemActivatedMessage(b1));
            _itemActivatedHandler.Handle(new ItemActivatedMessage(b2));

            _player.Received(1).Play(GameSoundId.ItemLaser, null, null, 2, 1f);
        }

        [Test]
        public void OnItemActivated_PitchRamps_ThirdPickupAtFourSemitones()
        {
            var b1 = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            b1.Item.Value = ItemType.Bomb;
            var b2 = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            b2.Item.Value = ItemType.Laser;
            var b3 = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            b3.Item.Value = ItemType.Lightning;

            _itemActivatedHandler.Handle(new ItemActivatedMessage(b1));
            _itemActivatedHandler.Handle(new ItemActivatedMessage(b2));
            _itemActivatedHandler.Handle(new ItemActivatedMessage(b3));

            _player.Received(1).Play(GameSoundId.ItemLightning, null, null, 4, 1f);
        }

        [Test]
        public void OnItemActivated_PitchRamps_ResetsOnProjectileLoaded()
        {
            var b1 = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            b1.Item.Value = ItemType.Bomb;
            var b2 = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            b2.Item.Value = ItemType.Laser;

            _itemActivatedHandler.Handle(new ItemActivatedMessage(b1));
            _itemActivatedHandler.Handle(new ItemActivatedMessage(b2));

            // New flight — counter resets.
            _loadedHandler.Handle(new ProjectileLoadedMessage(null));

            var b3 = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            b3.Item.Value = ItemType.Paint;
            _itemActivatedHandler.Handle(new ItemActivatedMessage(b3));

            _player.Received(1).Play(GameSoundId.ItemPaint, null, null, 0, 1f);
        }

        [Test]
        public void OnItemActivated_PitchRamps_DifferentItemTypesShareCounter()
        {
            // Shield then Bomb — both increment the same counter.
            var b1 = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            b1.Item.Value = ItemType.Shield;
            var b2 = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            b2.Item.Value = ItemType.Bomb;

            _itemActivatedHandler.Handle(new ItemActivatedMessage(b1));
            _itemActivatedHandler.Handle(new ItemActivatedMessage(b2));

            _player.Received(1).Play(GameSoundId.ItemShield, null, null, 0, 1f);
            _player.Received(1).Play(GameSoundId.ItemBomb, null, null, 2, 1f);
        }

        [Test]
        public void OnItemActivated_ItemTypeNone_DoesNotPlay()
        {
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Item.Value = ItemType.None;

            _itemActivatedHandler.Handle(new ItemActivatedMessage(balloon));

            _player.DidNotReceive().Play(Arg.Any<GameSoundId>(), Arg.Any<Vector3?>());
        }

        [Test]
        public void OnItemActivated_BalloonNotIHasItemSlot_DoesNotPlay()
        {
            // ToughBalloonModel is a real IBalloonModel that deliberately does NOT implement
            // IHasItemSlot (ItemSlotTests locks this conformance) — the router's downcast guard
            // must skip it rather than throw or play a stale sound.
            var tough = new ToughBalloonModel(new BalloonModelConfig());

            _itemActivatedHandler.Handle(new ItemActivatedMessage(tough));

            _player.DidNotReceive().Play(Arg.Any<GameSoundId>(), Arg.Any<Vector3?>());
        }

        [Test]
        public void OnOverflowHeart_ForwardsHeartDrainAtTargetPosition()
        {
            var position = new Vector3(5f, 1f, 0f);

            _overflowHeartHandler.Handle(new OverflowHeartRequestedMessage(3, position));

            _player.Received(1).Play(GameSoundId.HeartDrain, position);
        }

        private static ISubscriber<T> CaptureSubscriber<T>(Action<IMessageHandler<T>> capture)
        {
            var subscriber = Substitute.For<ISubscriber<T>>();
            subscriber
                .Subscribe(
                    Arg.Do(capture),
                    Arg.Any<MessageHandlerFilter<T>[]>())
                .Returns(Substitute.For<IDisposable>());
            return subscriber;
        }
    }
}
