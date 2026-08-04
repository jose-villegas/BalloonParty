using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Game.Score;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Rendering;
using BalloonParty.UI.Score;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MessagePipe;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.UI.LevelUp
{
    public class LevelUpPopUp : MonoBehaviour
    {
        private const int FillTrailSortingOrder = 3200;

        private static readonly int AppearTrigger = Animator.StringToHash("Appear");
        private static readonly int HideTrigger = Animator.StringToHash("Hide");
        private static readonly int AppearState = Animator.StringToHash("LeveUpAppear");

        [Header("References")] [SerializeField]
        private Animator _animator;

        [SerializeField] private TMP_Text _levelLabel;

        [Tooltip("The level just completed, beside _levelLabel's new one. Both are set once when the " +
            "popup opens and neither changes while it is up. Optional — leave it unassigned to show " +
            "only the new level.")]
        [SerializeField] private TMP_Text _previousLevelLabel;

        [Tooltip("Scaled from 0 to 1 as the fill trails land. Must NOT be the trail target area — " +
            "it is zero-sized for most of the ceremony.")]
        [SerializeField] private RectTransform _levelFill;

        [Tooltip("Fixed-size container the trail arrival disc is measured from. Separate from " +
            "_levelFill on purpose: that one scales to zero, which would collapse the disc to a point.")]
        [SerializeField] private RectTransform _fillTargetArea;

        [Header("Fill Trails")]
        [FormerlySerializedAs("_glowTrailsPerBar")]
        [SerializeField] private int _fillTrailsPerBar = 8;

        [FormerlySerializedAs("_glowTrailStaggerDelay")]
        [SerializeField] private float _fillTrailStaggerDelay = 0.08f;

        [FormerlySerializedAs("_glowTrailDuration")]
        [SerializeField] private float _fillTrailDuration = 0.8f;

        [Tooltip("How long the fill takes to catch up to one trail arrival. Longer than the gap " +
            "between arrivals on purpose — the tween restarts from wherever it is, so overlapping " +
            "steps blend into one continuous ramp instead of a staircase.")]
        [SerializeField] private float _fillStepDuration = 0.35f;

        [Header("Fill Target (fractions of the target area's half-width; angles CCW from +x)")]
        [FormerlySerializedAs("_glowTargetRadiusMultiplier")]
        [SerializeField] [Range(0f, 1f)] private float _fillTargetRadiusMultiplier = 0.8f;

        [FormerlySerializedAs("_glowTargetInnerRadiusMultiplier")]
        [SerializeField] [Range(0f, 1f)] private float _fillTargetInnerRadiusMultiplier;

        [FormerlySerializedAs("_glowTargetMinAngle")]
        [SerializeField] private float _fillTargetMinAngle;

        [FormerlySerializedAs("_glowTargetMaxAngle")]
        [SerializeField] private float _fillTargetMaxAngle = 360f;

        [Inject] private ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        [Inject] private IPublisher<LevelUpDismissedMessage> _dismissedPublisher;
        [Inject] private IPublisher<LevelUpFillTrailsMessage> _fillTrailsPublisher;
        [Inject] private CinematicEndGate _gate;
        [Inject] private PauseService _pauseService;
        [Inject] private TimeScaleService _timeScaleService;
        [Inject] private IGamePalette _palette;
        [Inject] private PoolManager _poolManager;
        [Inject] private ScoreTrailService _scoreTrailService;

        private readonly CompositeDisposable _disposable = new();
        private readonly Dictionary<string, TrailSpawner> _trailSpawners = new();

        private Tween _fillTween;

        private int _fillTrailArrivedCount;
        private int _fillTrailTotalCount;
        private bool _isShowing;

        private void Start()
        {
            _animator.updateMode = AnimatorUpdateMode.UnscaledTime;

            _levelUpSubscriber
                .Subscribe(msg => ShowAfterGateAsync(msg).Forget())
                .AddTo(_disposable);
        }

        private void OnDestroy()
        {
            _fillTween?.Kill();
            _disposable.Dispose();
        }

        public void OnContinue()
        {
            // The hidden popup's full-screen button still receives raycasts, so every gameplay tap
            // lands here — without this gate each one published a dismissal and vanished the live shot.
            if (!_isShowing)
            {
                return;
            }

            _isShowing = false;
            _animator.ResetTrigger(AppearTrigger);
            _animator.SetTrigger(HideTrigger);
            Resume();
        }

        private async UniTaskVoid ShowAfterGateAsync(ScoreLevelUpMessage msg)
        {
            _pauseService.Pause(PauseSource.LevelUp);

            await _gate.WaitAsync(destroyCancellationToken);

            _timeScaleService.Claim(TimeScaleSource.LevelUpPopup, 0f);

            // NewLevel is the level being entered, so NewLevel - 1 names the one just completed — the
            // same offset GameplayMetricsService applies for its ceremony snapshot's level index.
            _levelLabel.text = msg.NewLevel.ToString("N0");
            if (_previousLevelLabel != null)
            {
                _previousLevelLabel.text = (msg.NewLevel - 1).ToString("N0");
            }

            SnapFillFraction(0f);
            _animator.ResetTrigger(HideTrigger);
            _animator.SetTrigger(AppearTrigger);
            _isShowing = true;

            await WaitForAnimatorStateAsync(AppearState);

            _fillTrailArrivedCount = 0;
            _fillTrailTotalCount = msg.CompletedColors.Count * _fillTrailsPerBar;

            _fillTrailsPublisher.Publish(
                new LevelUpFillTrailsMessage(_fillTrailsPerBar, _fillTrailStaggerDelay));

            SpawnFillTrailsAsync(msg.CompletedColors).Forget();
        }

        // Takes completed colors as a param — live AllowedColors may already reflect the new level.
        private async UniTaskVoid SpawnFillTrailsAsync(IReadOnlyList<string> completedColors)
        {
            if (!TryGetFillTargetGeometry(out var fillCenter, out var innerRadius, out var outerRadius))
            {
                return;
            }

            var minAngleRad = _fillTargetMinAngle * Mathf.Deg2Rad;
            var maxAngleRad = _fillTargetMaxAngle * Mathf.Deg2Rad;
            var staggerMs = Mathf.RoundToInt(_fillTrailStaggerDelay * 1000f);

            for (var i = 0; i < _fillTrailsPerBar; i++)
            {
                foreach (var colorName in completedColors)
                {
                    var entry = _palette.GetEntry(colorName);
                    var target = _scoreTrailService.GetTarget(entry.Name);
                    var spawner = GetOrCreateSpawner(entry.Name);

                    var offset = VectorMathExtensions.RandomPointInAnnulusSector(
                        innerRadius, outerRadius, minAngleRad, maxAngleRad);
                    var destination = fillCenter + new Vector3(offset.x, offset.y, 0f);

                    spawner.Spawn(target.RandomPosition(), destination,
                        _fillTrailDuration, entry.Color, OnFillTrailArrived, useUnscaledTime: true);
                }

                if (i < _fillTrailsPerBar - 1)
                {
                    await UniTask.Delay(staggerMs, true,
                        cancellationToken: destroyCancellationToken);
                }
            }
        }

        private void OnFillTrailArrived()
        {
            // A trail in flight outlives the popup when the ceremony tears down (or the scene
            // unloads) before it lands — the arrival callback must not touch destroyed UI.
            if (this == null || _levelFill == null)
            {
                return;
            }

            _fillTrailArrivedCount++;
            TweenFillFraction((float)_fillTrailArrivedCount / _fillTrailTotalCount);
        }

        // Uniform, because the arrival disc is measured off a half-width — a non-uniform fill would
        // make the trails land off the visible edge on one axis.
        //
        // Unscaled: the popup holds Time.timeScale at 0, and the trails it follows fly on the same
        // clock. Restarted rather than queued, so a burst of arrivals reads as one accelerating ramp.
        private void TweenFillFraction(float fraction)
        {
            if (_levelFill == null)
            {
                return;
            }

            _fillTween?.Kill();
            _fillTween = _levelFill
                .DOScale(Vector3.one * Mathf.Clamp01(fraction), _fillStepDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void SnapFillFraction(float fraction)
        {
            _fillTween?.Kill();
            if (_levelFill != null)
            {
                _levelFill.localScale = Vector3.one * Mathf.Clamp01(fraction);
            }
        }

        // World-space arrival disc: center plus inner/outer radii scaled off the target area's
        // half-width. Measured from _fillTargetArea, never from _levelFill — the fill scales from zero,
        // so deriving the disc from it would send every trail to a single point at the start and drift
        // the destination outward as it grew. Shared by trail spawning and the editor gizmo so both
        // agree.
        private bool TryGetFillTargetGeometry(out Vector3 center, out float innerRadius, out float outerRadius)
        {
            center = default;
            innerRadius = 0f;
            outerRadius = 0f;

            if (_fillTargetArea == null)
            {
                return false;
            }

            var area = _fillTargetArea;
            center = area.TransformPoint(area.rect.center);
            var edge = area.TransformPoint(
                new Vector3(area.rect.xMax, area.rect.center.y, 0f));
            var halfWidth = Vector3.Distance(center, edge);
            outerRadius = halfWidth * _fillTargetRadiusMultiplier;
            innerRadius = halfWidth * _fillTargetInnerRadiusMultiplier;
            return true;
        }

        private TrailSpawner GetOrCreateSpawner(string colorName)
        {
            if (_trailSpawners.TryGetValue(colorName, out var spawner))
            {
                return spawner;
            }

            spawner = new TrailSpawner(
                _poolManager, $"FillTrail_{colorName}", _scoreTrailService.TrailPrefab, FillTrailSortingOrder);
            _trailSpawners[colorName] = spawner;
            return spawner;
        }

        private void Resume()
        {
            // Publish before releasing the freeze so the hand-back never flashes full speed.
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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!TryGetFillTargetGeometry(out var center, out var innerRadius, out var outerRadius))
            {
                return;
            }

            GizmoDrawingHelper.DrawWorldRingSegment(
                center, innerRadius, outerRadius,
                _fillTargetMinAngle, _fillTargetMaxAngle, Color.cyan);
        }
#endif
    }
}
