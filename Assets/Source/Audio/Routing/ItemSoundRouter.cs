using System;
using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using UniRx;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Audio.Routing
{
    internal sealed class ItemSoundRouter : IStartable, IDisposable
    {
        private readonly ISoundPlayer _player;
        private readonly ISubscriber<ItemActivatedMessage> _itemActivatedSubscriber;
        private readonly ISubscriber<OverflowHeartRequestedMessage> _overflowHeartSubscriber;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly IProjectileRegistry _projectiles;
        private readonly CompositeDisposable _subscriptions = new();

        // Pickups each ball has set off, so every ball climbs its own ramp: two balls in flight must
        // not push each other's pickups up the scale. Dropped when the ball retires.
        private readonly Dictionary<IProjectileModel, int> _pickupsByBall = new();

        // Pickups no ball set off. Reset on every load, as the one per-flight counter always was.
        private int _unattributedPickups;

        [Inject]
        public ItemSoundRouter(ISoundPlayer player,
            ISubscriber<ItemActivatedMessage> itemActivatedSubscriber,
            ISubscriber<OverflowHeartRequestedMessage> overflowHeartSubscriber,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            IProjectileRegistry projectiles)
        {
            _player = player;
            _itemActivatedSubscriber = itemActivatedSubscriber;
            _overflowHeartSubscriber = overflowHeartSubscriber;
            _loadedSubscriber = loadedSubscriber;
            _projectiles = projectiles;
        }

        public void Start()
        {
            _itemActivatedSubscriber.Subscribe(OnItemActivated).AddTo(_subscriptions);
            _overflowHeartSubscriber.Subscribe(OnOverflowHeart).AddTo(_subscriptions);
            _loadedSubscriber.Subscribe(_ => _unattributedPickups = 0).AddTo(_subscriptions);
            _projectiles.Retired.Subscribe(ball => _pickupsByBall.Remove(ball)).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnItemActivated(ItemActivatedMessage message)
        {
            if (message.Balloon is not IHasItemSlot slot)
            {
                return;
            }

            var id = slot.Item.Value switch
            {
                ItemType.Bomb => GameSoundId.ItemBomb,
                ItemType.Laser => GameSoundId.ItemLaser,
                ItemType.Lightning => GameSoundId.ItemLightning,
                ItemType.Paint => GameSoundId.ItemPaint,
                ItemType.Snipe => GameSoundId.ItemSnipe,
                ItemType.Shield => GameSoundId.ItemShield,
                _ => GameSoundId.None
            };

            if (id == GameSoundId.None)
            {
                return;
            }

            var pickups = NextPickupIndex(message.Projectile);
            _player.Play(id, null, semitoneOffset: pickups * MusicalPitchExtensions.WholeToneSemitones);
        }

        // A ball the registry no longer tracks (retired before its item fired a frame later) shares the
        // unattributed ramp, so a late pickup can't create an entry nothing would ever drop.
        private int NextPickupIndex(IProjectileModel projectile)
        {
            if (projectile == null || !_projectiles.IsTracked(projectile))
            {
                return _unattributedPickups++;
            }

            _pickupsByBall.TryGetValue(projectile, out var pickups);
            _pickupsByBall[projectile] = pickups + 1;
            return pickups;
        }

        private void OnOverflowHeart(OverflowHeartRequestedMessage message)
        {
            _player.Play(GameSoundId.HeartDrain, message.TargetPosition);
        }
    }
}
