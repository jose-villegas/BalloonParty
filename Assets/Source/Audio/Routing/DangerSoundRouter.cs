using System;
using BalloonParty.Audio.Configuration;
using BalloonParty.Game.Danger;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Audio.Routing
{
    // Plays a sustained warning while the board's danger level (IDangerLevel, 0-1) sits at or above the
    // configured threshold, and stops it — fading out per the entry — once the board is safe again.
    // Author DangerWarn as a loop (SingleInstance keeps the retrigger from restarting it).
    internal sealed class DangerSoundRouter : IStartable, IDisposable
    {
        private readonly ISoundPlayer _player;
        private readonly IDangerLevel _dangerLevel;
        private readonly ISoundBankConfiguration _bank;
        private readonly CompositeDisposable _subscriptions = new();

        private SoundHandle _handle = SoundHandle.None;

        public DangerSoundRouter(ISoundPlayer player, IDangerLevel dangerLevel, ISoundBankConfiguration bank)
        {
            _player = player;
            _dangerLevel = dangerLevel;
            _bank = bank;
        }

        public void Start()
        {
            _dangerLevel.Level.Subscribe(OnDangerLevelChanged).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnDangerLevelChanged(float level)
        {
            var inDanger = level >= _bank.DangerLevelThreshold;
            if (inDanger && !_handle.IsValid)
            {
                _handle = _player.Play(GameSoundId.DangerWarn, null);
            }
            else if (!inDanger && _handle.IsValid)
            {
                _player.Stop(_handle);
                _handle = SoundHandle.None;
            }
        }
    }
}
