using System;
using BalloonParty.Audio;
using BalloonParty.Audio.Routing;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Items;
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
        private IMessageHandler<SpawnBlockedMessage> _spawnBlockedHandler;

        [SetUp]
        public void SetUp()
        {
            _player = Substitute.For<ISoundPlayer>();

            var itemActivatedSubscriber = CaptureSubscriber<ItemActivatedMessage>(h => _itemActivatedHandler = h);
            var overflowHeartSubscriber = CaptureSubscriber<OverflowHeartRequestedMessage>(h => _overflowHeartHandler = h);
            var spawnBlockedSubscriber = CaptureSubscriber<SpawnBlockedMessage>(h => _spawnBlockedHandler = h);

            var router = new ItemSoundRouter(_player, itemActivatedSubscriber, overflowHeartSubscriber, spawnBlockedSubscriber);
            router.Start();
        }

        [TestCase(ItemType.Bomb, GameSoundId.ItemBomb)]
        [TestCase(ItemType.Laser, GameSoundId.ItemLaser)]
        [TestCase(ItemType.Lightning, GameSoundId.ItemLightning)]
        [TestCase(ItemType.Paint, GameSoundId.ItemPaint)]
        [TestCase(ItemType.Snipe, GameSoundId.ItemSnipe)]
        [TestCase(ItemType.Shield, GameSoundId.ItemShield)]
        internal void OnItemActivated_ItemType_PlaysMatchingSoundId(ItemType itemType, GameSoundId expectedId)
        {
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Item.Value = itemType;

            _itemActivatedHandler.Handle(new ItemActivatedMessage(balloon));

            _player.Received(1).Play(expectedId, null);
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

        [Test]
        public void OnSpawnBlocked_ForwardsOverflowThudAtPosition()
        {
            var position = new Vector3(2f, 0f, 0f);

            _spawnBlockedHandler.Handle(new SpawnBlockedMessage(1, position));

            _player.Received(1).Play(GameSoundId.OverflowThud, position);
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
