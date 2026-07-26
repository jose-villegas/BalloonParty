using System;
using BalloonParty.Configuration.Items;
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
        private readonly ISubscriber<SpawnBlockedMessage> _spawnBlockedSubscriber;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        private int _itemPickupsThisFlight;

        [Inject]
        public ItemSoundRouter(ISoundPlayer player,
            ISubscriber<ItemActivatedMessage> itemActivatedSubscriber,
            ISubscriber<OverflowHeartRequestedMessage> overflowHeartSubscriber,
            ISubscriber<SpawnBlockedMessage> spawnBlockedSubscriber,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber)
        {
            _player = player;
            _itemActivatedSubscriber = itemActivatedSubscriber;
            _overflowHeartSubscriber = overflowHeartSubscriber;
            _spawnBlockedSubscriber = spawnBlockedSubscriber;
            _loadedSubscriber = loadedSubscriber;
        }

        public void Start()
        {
            _itemActivatedSubscriber.Subscribe(OnItemActivated).AddTo(_subscriptions);
            _overflowHeartSubscriber.Subscribe(OnOverflowHeart).AddTo(_subscriptions);
            _spawnBlockedSubscriber.Subscribe(OnSpawnBlocked).AddTo(_subscriptions);
            _loadedSubscriber.Subscribe(_ => _itemPickupsThisFlight = 0).AddTo(_subscriptions);
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

            _player.Play(id, null, semitoneOffset: _itemPickupsThisFlight * MusicalPitchExtensions.WholeToneSemitones);
            _itemPickupsThisFlight++;
        }

        private void OnOverflowHeart(OverflowHeartRequestedMessage message)
        {
            _player.Play(GameSoundId.HeartDrain, message.TargetPosition);
        }

        private void OnSpawnBlocked(SpawnBlockedMessage message)
        {
            _player.Play(GameSoundId.OverflowThud, message.Position);
        }
    }
}
