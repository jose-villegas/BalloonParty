using BalloonParty.Audio.Configuration;
using BalloonParty.Audio.View;
using BalloonParty.Shared.Pool;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Audio
{
    internal sealed class SfxVoicePoolBootstrap : IStartable
    {
        private readonly PoolManager _poolManager;
        private readonly AudioSourceVoice _prefab;
        private readonly ISoundBankConfiguration _bank;

        [Inject]
        public SfxVoicePoolBootstrap(PoolManager poolManager, AudioSourceVoice prefab, ISoundBankConfiguration bank)
        {
            _poolManager = poolManager;
            _prefab = prefab;
            _bank = bank;
        }

        public void Start()
        {
            _poolManager.Register(AudioPoolKeys.VoicePoolKey, new SimplePoolChannel<AudioSourceVoice>(_prefab));
            _poolManager.Prewarm(AudioPoolKeys.VoicePoolKey, _bank.GlobalVoiceCap);
        }
    }
}
