#if UNITY_EDITOR
using UnityEditor;
#endif
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Capabilities;
using DG.Tweening;
using DG.Tweening.Core;
using UniRx;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Balloon.Type
{
    /// <summary>Drives the SoapBubbleCluster shader's bubble count and float/spin clock; the shader animates itself from <c>_Time.y</c> after Bind.
    /// During a merge it shows the count a bubble at a time and wobbles (<see cref="IBubbleMergeReactive" />). A ripe
    /// cluster (<see cref="BubbleClusterModel.Ripe" />) swells its renderer to <see cref="IRipeBubbleSettings.SwellScale" />,
    /// floats <see cref="IRipeBubbleSettings.FloatSpeedup" /> times faster, throbs as its fuse
    /// (<see cref="BubbleClusterModel.FuseLeft" />) crosses each second (<see cref="RipeBubbleRule.Throbs" />), is
    /// tinted toward <see cref="IRipeBubbleSettings.WarnTint" /> as the fuse runs out, and spins with the spiral arm it
    /// sheds: at the arm's own turn rate and the same way round (<see cref="RipeBubbleRule.ClusterSpin" />,
    /// <see cref="IRipeSpin" />), stepping up with the arm at the first fast throb. The fuse only moves while the
    /// level clock runs, so a pause or the level-up holds the throb and the tint with it. A cluster popped while ripe
    /// folds before its pop shows (<see cref="IBalloonPopPrelude" />): over <see cref="IRipeBubbleSettings.BurstSeconds" />
    /// it steps down to one big bubble (<see cref="RipeBubbleRule.FoldCount" />, <see cref="RipeBubbleRule.FoldScale" />),
    /// each step pumping its scale out (<see cref="RipeBubbleRule.FoldPulse" />), keeping the flush it had at the hit.
    /// All of it is undone on every bind and when the binding is dropped.</summary>
    [ExecuteAlways]
    public class SoapBubbleClusterVariant : MonoBehaviour, IBalloonVariant, IBalloonViewBinding, IBubbleMergeReactive,
        IBalloonPopPrelude, IRipeSpin
    {
        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("Most bubbles the cluster shows (the shader draws up to 6). At least the balloons " +
                 "configuration's Bubble Merge Max, which is what a merge fills a cluster to.")]
        [SerializeField] private int _maxBubbles = 6;

        [Tooltip("Animation speed. Owned here so edit-mode animation " +
                 "works without relying on the frozen _Time.y shader global.")]
        [SerializeField] private float _floatSpeed = SoapBubbleShader.DefaultFloatSpeed;

        [Header("Editor Preview")]
        [Tooltip("Drag to preview each cluster state without entering Play mode.")]
        [SerializeField] private int _previewBubbleCount = 5;

        [Inject] private IBalloonsConfiguration _balloons;

        private MaterialPropertyBlock _block;
        private IHasDurability _durable;
        private BubbleClusterModel _cluster;
        private Vector3 _rendererRestScale = Vector3.one;
        private Tween _swellTween;
        private Tween _punchTween;
        private Tween _foldTween;
        private TweenCallback _settleScale;
        private TweenCallback _stepFold;
        private TweenCallback _endFold;
        private DOGetter<float> _foldGetter;
        private DOSetter<float> _foldSetter;
        private System.Action _foldDone;
        private float _swell = 1f;
        private float _fold = 1f;
        private float _foldElapsed;
        private float _foldSeconds;
        private float _foldRadius;
        private float _foldGrowth;
        private float _foldPulse;
        private int _foldFrom;
        private bool _folding;
        private bool _spinFast;
        private float _lastFuseLeft;
        private float _timeOffset;
        private float _currentFloatSpeed;
        private float _currentRotation;
        private float _currentSpin;
        private int _shownCount;
        private int _countHolds;
        private int _binding;
        private float _instancePhase;
        private float _rotationAngle;
        private float _rotationSpeedRad;
        private float _lastTime = -1f;

        SpriteRenderer IBubbleMergeReactive.BubbleRenderer => _renderer;
        int IBubbleMergeReactive.Binding => _binding;

        Vector3 IBubbleMergeReactive.BubbleWorldScale
        {
            get
            {
                var parent = _renderer != null ? _renderer.transform.parent : null;
                return parent != null ? Vector3.Scale(parent.lossyScale, _rendererRestScale) : _rendererRestScale;
            }
        }

        float IRipeSpin.TurnDirection => TurnDirection;

        // The way round it drifts at rest, kept when it ripens: the shader's angle runs against what is drawn.
        private float TurnDirection => _rotationSpeedRad > 0f ? -1f : 1f;

        // The renderer's scale before any punch: its rest scale, swollen while ripe, grown while folding.
        private Vector3 BaseScale => _rendererRestScale * (_swell * _fold);

        private void Awake()
        {
            EnsureBlock();
            // Cached, so a throb's punch and a fold allocate no callbacks.
            _settleScale = SettleScale;
            _stepFold = StepFold;
            _endFold = EndFold;
            _foldGetter = () => _foldElapsed;
            _foldSetter = elapsed => _foldElapsed = elapsed;
            _instancePhase = Random.value * 100f;

            // The quad must stay axis-aligned — the shader rotates the content. Nothing rotates
            // this child at runtime, so a one-time reset here (not a per-frame one) is enough.
            if (_renderer != null)
            {
                _renderer.transform.localRotation = Quaternion.identity;
                _rendererRestScale = _renderer.transform.localScale;
            }

            // Instances that never Bind (scene-placed, previews) still get a live clock.
            if (Application.isPlaying)
            {
                PushRuntimeClock();
            }
        }

        private void Update()
        {
            EnsureBlock();

#if UNITY_EDITOR
            // _Time is frozen in edit mode, so integrate editor time here instead.
            if (!Application.isPlaying)
            {
                SceneView.RepaintAll();
                var editorTime = (float)EditorApplication.timeSinceStartup;
                if (_lastTime >= 0f)
                {
                    var delta = editorTime - _lastTime;
                    if (delta > 0f && delta < 0.5f)
                    {
                        _rotationAngle += _rotationSpeedRad * delta;
                    }
                }

                _lastTime = editorTime;
                PushEditPreview(editorTime);
            }
#endif
        }

        private void OnValidate()
        {
            EnsureBlock();
            PushBubbleCount(Mathf.Clamp(_previewBubbleCount, 1, _maxBubbles));
        }

        void IBubbleMergeReactive.HoldCount()
        {
            _countHolds++;
        }

        void IBubbleMergeReactive.StepCount(int delta)
        {
            if (_countHolds > 0)
            {
                PushBubbleCount(Mathf.Clamp(_shownCount + delta, 1, _maxBubbles));
            }
        }

        void IBubbleMergeReactive.ReleaseCount()
        {
            if (_countHolds == 0)
            {
                return;
            }

            _countHolds--;
            if (_countHolds == 0 && _durable != null && !_folding)
            {
                PushBubbleCount(Mathf.Clamp(_durable.HitsRemaining.Value, 1, _maxBubbles));
            }
        }

        // The renderer child, not the root: the root's position and scale belong to the balance and absorb tweens.
        void IBubbleMergeReactive.Wobble(float strength, float seconds)
        {
            Punch(strength, seconds);
        }

        // Only a cluster that was ripe when it popped folds; the model, out of play, keeps its ripe state and its fuse
        // as they stood at the hit, so the flush holds where it was.
        bool IBalloonPopPrelude.TryPlayPopPrelude(System.Action onDone)
        {
            if (_cluster == null || !_cluster.Ripe.Value || _renderer == null)
            {
                return false;
            }

            var settings = _balloons.RipeBubble;
            var seconds = settings.BurstSeconds;
            if (seconds <= 0f)
            {
                return false;
            }

            _foldFrom = Mathf.Clamp(_balloons.BubbleMergeMax, 1, _maxBubbles);
            _foldSeconds = seconds;
            _foldElapsed = 0f;
            _foldRadius = SoapBubbleShader.BubbleRadiusOf(_renderer.sharedMaterial);
            _foldGrowth = settings.FoldGrowth;
            _foldPulse = settings.FoldPulse;
            _foldDone = onDone;
            _folding = true;
            _punchTween?.Kill();
            _punchTween = null;
            _foldTween?.Kill();
            // The hit already took the model's count to nothing; the fold starts from the full cluster it was, with
            // the first pump.
            StepFold();
            _foldTween = DOTween.To(_foldGetter, _foldSetter, seconds, seconds)
                .SetEase(Ease.Linear)
                .SetTarget(this)
                .SetLink(gameObject)
                .OnUpdate(_stepFold)
                .OnComplete(_endFold);
            return true;
        }

        public void Initialize(IWriteableBalloonModel model, int levelAllowedColorsMask) { }

        public void Bind(IBalloonModel model, CompositeDisposable disposables)
        {
            if (_renderer == null)
            {
                Log.Error("SoapBubbleCluster",
                    $"Bind: _renderer is not assigned on \"{gameObject.name}\" " +
                    "— bubble count visuals will be disabled. Fix the prefab.",
                    this);
                return;
            }

            if (model is not IHasDurability durable)
            {
                return;
            }

            _durable = durable;
            ResetCues();
            // Cleared on despawn and on a level transition's freeze, so no hold, wobble or ripe look outlives the
            // binding.
            disposables.Add(Disposable.Create(ResetCues));

            _rotationAngle = SoapBubbleShader.RandomRotation();
            _rotationSpeedRad = SoapBubbleShader.RandomSpinRadians();
            _lastTime = -1f;

            PushRuntimeClock();
            PushBubbleCount(Mathf.Clamp(durable.HitsRemaining.Value, 1, _maxBubbles));

            durable.HitsRemaining
                .Subscribe(OnHitsChanged)
                .AddTo(disposables);

            if (model is BubbleClusterModel cluster && _balloons != null)
            {
                _cluster = cluster;
                _lastFuseLeft = cluster.FuseLeft.Value;
                cluster.Ripe
                    .Subscribe(OnRipeChanged)
                    .AddTo(disposables);
                cluster.FuseLeft
                    .Subscribe(OnFuseLeftChanged)
                    .AddTo(disposables);
            }
        }

        private void EnsureBlock()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }
        }

        // A merge holds the shown count and steps it itself until its bubbles have landed.
        private void OnHitsChanged(int hits)
        {
            if (_countHolds == 0 && !_folding)
            {
                PushBubbleCount(Mathf.Clamp(hits, 1, _maxBubbles));
            }
        }

        // The arm starts slow as the cluster ripens, and so does the spin.
        private void OnRipeChanged(bool ripe)
        {
            var settings = _balloons.RipeBubble;
            _spinFast = false;
            SwellTo(ripe ? settings.SwellScale : 1f, settings.SwellSeconds);
            PushFloatSpeed(ripe ? _floatSpeed * settings.FloatSpeedup : _floatSpeed);
            ApplyTint(_cluster.FuseLeft.Value);
        }

        // The spin changes pace on the throb, as the arm does on the beat the service sends for the same throb
        // (RipeFuseBeatMessage.Fast), so the two step up in the same frame.
        private void OnFuseLeftChanged(float left)
        {
            var settings = _balloons.RipeBubble;
            if (_cluster.Ripe.Value && RipeBubbleRule.Throbs(_lastFuseLeft, left, settings.ThrobFastSeconds))
            {
                _spinFast = RipeBubbleRule.IsFastThrob(left, settings.ThrobFastSeconds);
                Punch(settings.ThrobStrength, settings.ThrobSeconds);
            }

            _lastFuseLeft = left;
            ApplyTint(left);
        }

        private void ApplyTint(float fuseLeft)
        {
            var settings = _balloons.RipeBubble;
            var ripe = _cluster.Ripe.Value;
            var amount = ripe ? RipeBubbleRule.WarnAmount(fuseLeft, settings.FuseSeconds, settings.WarnStart) : 0f;
            var spin = ripe
                ? RipeBubbleRule.ClusterSpin(TurnDirection, settings.SpiralTurnsPerSecond, _spinFast)
                : _rotationSpeedRad;
            PushWarning(settings.WarnTint, amount, spin);
        }

        // The warning as the fuse runs out: the cluster fills with the tint, and turns with its spiral arm. The fill
        // is the shader's flush, not the renderer's colour (the film is nearly clear, so tinting it shows nothing).
        // The angle is rebased as the float's offset is, so a new spin carries on from where the cluster has turned to.
        private void PushWarning(Color tint, float amount, float spin)
        {
            if (_renderer == null || _block == null)
            {
                return;
            }

            tint.a = Mathf.Clamp01(amount);
            _currentRotation = SoapBubbleShader.RebaseTimeOffset(
                Time.timeSinceLevelLoad, _currentRotation, _currentSpin, spin);
            _currentSpin = spin;
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(SoapBubbleShader.FlushId, tint);
            _block.SetFloat(SoapBubbleShader.RotationId, _currentRotation);
            _block.SetFloat(SoapBubbleShader.RotationSpeedId, _currentSpin);
            _renderer.SetPropertyBlock(_block);
        }

        // Reset to the base and punched from it, so a wobble or a throb never drops the swell.
        private void Punch(float strength, float seconds)
        {
            if (_renderer == null || strength <= 0f)
            {
                return;
            }

            var body = _renderer.transform;
            _punchTween?.Kill();
            var baseScale = BaseScale;
            body.localScale = baseScale;
            _punchTween = body.DOPunchScale(baseScale * strength, seconds, vibrato: 5, elasticity: 0.6f)
                .OnComplete(_settleScale);
        }

        // The swell tweens a factor, not the transform, so a punch in flight keeps the transform and lands on the new
        // base when it settles.
        private void SwellTo(float target, float seconds)
        {
            _swellTween?.Kill();
            if (seconds <= 0f || Mathf.Approximately(_swell, target))
            {
                _swell = target;
                SettleScale();
                return;
            }

            _swellTween = DOTween.To(() => _swell, value => _swell = value, target, seconds)
                .SetEase(Ease.OutBack)
                .SetTarget(this)
                .OnUpdate(_settleScale);
        }

        private void SettleScale()
        {
            // IsPlaying, not IsActive: a punch that has just finished is still active inside its own
            // OnComplete, and that call is the one that must put the swollen base back.
            if (_renderer != null && (_punchTween == null || !_punchTween.IsActive() || !_punchTween.IsPlaying()))
            {
                _renderer.transform.localScale = BaseScale;
            }
        }

        // The layout and the scale change together, so the outline holds while the bubbles grow into one; the pump
        // on top moves every frame, so the scale is set every frame.
        private void StepFold()
        {
            var shown = RipeBubbleRule.FoldCount(_foldElapsed, _foldSeconds, _foldFrom);
            if (shown != _shownCount)
            {
                PushBubbleCount(shown);
            }

            _fold = RipeBubbleRule.FoldScale(shown, _foldFrom, _foldRadius, _foldGrowth)
                    * RipeBubbleRule.FoldPulse(_foldElapsed, _foldSeconds, _foldFrom, _foldPulse);
            SettleScale();
        }

        // Stays folded: the pop that follows shrinks the one big bubble away.
        private void EndFold()
        {
            var done = _foldDone;
            _foldDone = null;
            _foldTween = null;
            done?.Invoke();
        }

        // A freeze mid-merge would otherwise leave the cluster showing a count between the two, and a pooled view
        // would come back swollen, tinted or floating fast.
        private void ResetCues()
        {
            if (_countHolds > 0 && _durable != null)
            {
                PushBubbleCount(Mathf.Clamp(_durable.HitsRemaining.Value, 1, _maxBubbles));
            }

            _countHolds = 0;
            _binding++;
            _swellTween?.Kill();
            _swellTween = null;
            _punchTween?.Kill();
            _punchTween = null;
            // A fold dropped here never calls back: the view has been taken back, or bound to another balloon.
            _foldTween?.Kill();
            _foldTween = null;
            _foldDone = null;
            _folding = false;
            _spinFast = false;
            _swell = 1f;
            _fold = 1f;
            _cluster = null;
            if (_renderer != null)
            {
                _renderer.transform.DOKill();
                _renderer.transform.localScale = _rendererRestScale;
            }

            PushWarning(Color.clear, 0f, _rotationSpeedRad);
            PushFloatSpeed(_floatSpeed);
        }

        private void PushBubbleCount(int count)
        {
            _shownCount = count;
            if (_renderer == null || _block == null)
            {
                return;
            }

            _renderer.SetFloatAndApply(_block, SoapBubbleShader.BubbleCountId, count);
        }

        // Rebased so the bubbles carry on from where they are rather than jumping to the new speed's phase.
        private void PushFloatSpeed(float speed)
        {
            if (_renderer == null || _block == null || Mathf.Approximately(speed, _currentFloatSpeed))
            {
                return;
            }

            _timeOffset = SoapBubbleShader.RebaseTimeOffset(
                Time.timeSinceLevelLoad, _timeOffset, _currentFloatSpeed, speed);
            _currentFloatSpeed = speed;
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(SoapBubbleShader.TimeOffsetId, _timeOffset);
            _block.SetFloat(SoapBubbleShader.FloatSpeedId, speed);
            _renderer.SetPropertyBlock(_block);
        }

        // Pushed once per Bind — no per-frame property-block churn at runtime.
        private void PushRuntimeClock()
        {
            if (_renderer == null || _block == null)
            {
                return;
            }

            _timeOffset = _instancePhase;
            _currentFloatSpeed = _floatSpeed;
            _currentRotation = _rotationAngle;
            _currentSpin = _rotationSpeedRad;
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(SoapBubbleShader.TimeOffsetId, _timeOffset);
            _block.SetFloat(SoapBubbleShader.FloatSpeedId, _currentFloatSpeed);
            _block.SetFloat(SoapBubbleShader.RotationId, _rotationAngle);
            _block.SetFloat(SoapBubbleShader.RotationSpeedId, _rotationSpeedRad);
            _renderer.SetPropertyBlock(_block);
        }

#if UNITY_EDITOR
        private void PushEditPreview(float editorTime)
        {
            if (_renderer == null || _block == null)
            {
                return;
            }

            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(SoapBubbleShader.TimeOffsetId, editorTime * _floatSpeed + _instancePhase);
            _block.SetFloat(SoapBubbleShader.FloatSpeedId, 0f);
            _block.SetFloat(SoapBubbleShader.RotationId, _rotationAngle);
            _block.SetFloat(SoapBubbleShader.RotationSpeedId, 0f);
            _renderer.SetPropertyBlock(_block);
        }
#endif
    }
}
