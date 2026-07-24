using System;
using BalloonParty.Audio.Configuration;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using MessagePipe;
using UniRx;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Audio
{
    internal sealed class AudioChannelController : IStartable, IDisposable
    {
        private readonly ISubscriber<PausedMessage> _pausedSubscriber;
        private readonly ISubscriber<ResumedMessage> _resumedSubscriber;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;
        private readonly IAudioMixerRouter _mixerRouter;
        private readonly SfxService _sfxService;
        private readonly CompositeDisposable _subscriptions = new();

        [Inject]
        public AudioChannelController(ISubscriber<PausedMessage> pausedSubscriber,
            ISubscriber<ResumedMessage> resumedSubscriber, ISubscriber<GameOverMessage> gameOverSubscriber,
            IAudioMixerRouter mixerRouter, SfxService sfxService)
        {
            _pausedSubscriber = pausedSubscriber;
            _resumedSubscriber = resumedSubscriber;
            _gameOverSubscriber = gameOverSubscriber;
            _mixerRouter = mixerRouter;
            _sfxService = sfxService;
        }

        public void Start()
        {
            _pausedSubscriber.Subscribe(_ => _mixerRouter.SetChannelDucked(SfxChannel.Gameplay, true))
                .AddTo(_subscriptions);
            _resumedSubscriber.Subscribe(_ => _mixerRouter.SetChannelDucked(SfxChannel.Gameplay, false))
                .AddTo(_subscriptions);
            _gameOverSubscriber.Subscribe(_ => _sfxService.StopChannel(SfxChannel.Gameplay))
                .AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}
