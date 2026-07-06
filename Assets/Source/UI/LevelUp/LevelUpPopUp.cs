using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Game.Score;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.UI.Score;
using Cysharp.Threading.Tasks;
using MessagePipe;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.UI.LevelUp
{
    public class LevelUpPopUp : MonoBehaviour
    {
        private const int GlowTrailSortingOrder = 3200;

        private static readonly int AppearTrigger = Animator.StringToHash("Appear");
        private static readonly int HideTrigger = Animator.StringToHash("Hide");
        private static readonly int AppearState = Animator.StringToHash("LeveUpAppear");

        [Header("References")] [SerializeField]
        private Animator _animator;

        [SerializeField] private TMP_Text _levelLabel;
        [SerializeField] private Image _levelGlowFill;

        [Header("Glow Trails")] [SerializeField] private int _glowTrailsPerBar = 8;
        [SerializeField] private float _glowTrailStaggerDelay = 0.08f;
        [SerializeField] private float _glowTrailDuration = 0.8f;
        [SerializeField] [Range(0f, 1f)] private float _glowTargetRadiusMultiplier = 0.8f;

        [Header("Timing")] [SerializeField] private float _continueUnpauseDelay;

        [Inject] private ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        [Inject] private IPublisher<LevelUpDismissedMessage> _dismissedPublisher;
        [Inject] private IPublisher<LevelUpGlowTrailsMessage> _glowTrailsPublisher;
        [Inject] private IReadyGate _gate;
        [Inject] private PauseService _pauseService;
        [Inject] private TimeScaleService _timeScaleService;
        [Inject] private IGamePalette _palette;
        [Inject] private PoolManager _poolManager;
        [Inject] private ScoreTrailService _scoreTrailService;

        private readonly CompositeDisposable _disposable = new();
        private readonly Dictionary<string, TrailSpawner> _trailSpawners = new();

        private int _glowTrailArrivedCount;
        private int _glowTrailTotalCount;
        private int _pendingNewLevel;

        private void Start()
        {
            _animator.updateMode = AnimatorUpdateMode.UnscaledTime;

            _levelUpSubscriber
                .Subscribe(msg => ShowAfterGateAsync(msg).Forget())
                .AddTo(_disposable);
        }

        private void OnDestroy()
        {
            _disposable.Dispose();
        }

        public void OnContinue()
        {
            _animator.ResetTrigger(AppearTrigger);
            _animator.SetTrigger(HideTrigger);
            ResumeAfterDelayAsync().Forget();
        }

        private async UniTaskVoid ShowAfterGateAsync(ScoreLevelUpMessage msg)
        {
            _pauseService.Pause(PauseSource.LevelUp);

            await _gate.WaitAsync(destroyCancellationToken);

            _timeScaleService.Claim(TimeScaleSource.LevelUpPopup, 0f);

            _levelLabel.text = (msg.NewLevel - 1).ToString("N0");
            _levelGlowFill.fillAmount = 0f;
            _animator.ResetTrigger(HideTrigger);
            _animator.SetTrigger(AppearTrigger);

            await WaitForAnimatorStateAsync(AppearState);

            _pendingNewLevel = msg.NewLevel;
            _glowTrailArrivedCount = 0;
            _glowTrailTotalCount = msg.CompletedColors.Count * _glowTrailsPerBar;

            _glowTrailsPublisher.Publish(
                new LevelUpGlowTrailsMessage(_glowTrailsPerBar, _glowTrailStaggerDelay));

            SpawnGlowTrailsAsync(msg.CompletedColors).Forget();
        }

        // Takes the completed colors as a param rather than reading live AllowedColors, which may
        // already reflect the new level by now (unenforced subscriber order on ScoreLevelUpMessage).
        private async UniTaskVoid SpawnGlowTrailsAsync(IReadOnlyList<string> completedColors)
        {
            var glowRect = _levelGlowFill.rectTransform;
            var glowCenter = glowRect.TransformPoint(glowRect.rect.center);
            var glowEdge = glowRect.TransformPoint(
                new Vector3(glowRect.rect.xMax, glowRect.rect.center.y, 0f));
            var glowRadius = Vector3.Distance(glowCenter, glowEdge) * _glowTargetRadiusMultiplier;
            var staggerMs = Mathf.RoundToInt(_glowTrailStaggerDelay * 1000f);

            for (var i = 0; i < _glowTrailsPerBar; i++)
            {
                foreach (var colorName in completedColors)
                {
                    var entry = _palette.GetEntry(colorName);
                    var target = _scoreTrailService.GetTarget(entry.Name);
                    var spawner = GetOrCreateSpawner(entry.Name);

                    var offset = Random.insideUnitCircle * glowRadius;
                    var destination = glowCenter + new Vector3(offset.x, offset.y, 0f);

                    spawner.Spawn(target.RandomPosition(), destination,
                        _glowTrailDuration, entry.Color, OnGlowTrailArrived, useUnscaledTime: true);
                }

                if (i < _glowTrailsPerBar - 1)
                {
                    await UniTask.Delay(staggerMs, true,
                        cancellationToken: destroyCancellationToken);
                }
            }
        }

        private void OnGlowTrailArrived()
        {
            _glowTrailArrivedCount++;
            _levelGlowFill.fillAmount = Mathf.Clamp01(
                (float)_glowTrailArrivedCount / _glowTrailTotalCount);

            if (_glowTrailArrivedCount >= _glowTrailTotalCount)
            {
                _levelLabel.text = _pendingNewLevel.ToString("N0");
            }
        }

        private TrailSpawner GetOrCreateSpawner(string colorName)
        {
            if (_trailSpawners.TryGetValue(colorName, out var spawner))
            {
                return spawner;
            }

            spawner = new TrailSpawner(
                _poolManager, $"GlowTrail_{colorName}", _scoreTrailService.TrailPrefab, GlowTrailSortingOrder);
            _trailSpawners[colorName] = spawner;
            return spawner;
        }

        private async UniTaskVoid ResumeAfterDelayAsync()
        {
            await UniTask.Delay(
                (int)(_continueUnpauseDelay * 1000),
                true,
                cancellationToken: destroyCancellationToken);

            // Publish first: the restore cinematic claims its ramp (starting at the frozen 0) before
            // the popup's freeze is released, so the hand-back never flashes full speed.
            _dismissedPublisher.Publish(new LevelUpDismissedMessage());
            _timeScaleService.Release(TimeScaleSource.LevelUpPopup);
            _pauseService.Resume(PauseSource.LevelUp);
        }

        private async UniTask WaitForAnimatorStateAsync(int stateHash)
        {
            await UniTask.WaitUntil(
                () => _animator.GetCurrentAnimatorStateInfo(0).shortNameHash == stateHash,
                cancellationToken: destroyCancellationToken);

            await UniTask.WaitUntil(
                () => _animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f,
                cancellationToken: destroyCancellationToken);
        }
    }
}
