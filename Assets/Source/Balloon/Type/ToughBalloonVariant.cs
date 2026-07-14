using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using DG.Tweening;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Type
{
    public class ToughBalloonVariant : MonoBehaviour, IBalloonVariant, IBalloonViewBinding
    {
        private static readonly int DamageProgressId = Shader.PropertyToID("_DamageProgress");
        private static readonly int VoronoiSeedId = Shader.PropertyToID("_VoronoiSeed");

        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private float _crackAnimDuration = 0.5f;

        [Inject] private DisturbanceFieldService _disturbanceField;
        [Inject] private IGamePalette _palette;
        [Inject] private IPublisher<SpeckSpawnRequestMessage> _speckPublisher;

        private MaterialPropertyBlock _block;
        private Tween _damageTween;
        private float _currentDamageProgress;
        private bool _repelPulse;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        public void Bind(IBalloonModel model, CompositeDisposable disposables)
        {
            if (_renderer == null)
            {
                Debug.LogError(
                    $"ToughBalloonVariant.Bind: _renderer is not assigned on \"{gameObject.name}\" " +
                    "— crack damage visuals will be disabled. Fix the prefab.",
                    this);
                return;
            }

            if (model is not IHasDurability durable)
            {
                return;
            }

            // Kill any in-flight tween from a previous pool cycle
            _damageTween?.Kill();
            _currentDamageProgress = 0f;

            _renderer.SetFloatAndApply(_block, DamageProgressId, 0f);
            _renderer.SetVectorAndApply(_block, VoronoiSeedId,
                new Vector4(UnityEngine.Random.Range(-999f, 999f), UnityEngine.Random.Range(-999f, 999f)));

            var maxHits = durable.HitsRemaining.Value;

            var warningPulse = new SerialDisposable();
            disposables.Add(warningPulse);

            durable.HitsRemaining
                .Subscribe(hits => OnHitsChanged(hits, maxHits, warningPulse))
                .AddTo(disposables);

            // The crack tween targets a virtual float, so BalloonView's transform.DOKill won't catch it — kill
            // it when this bind's disposables clear (despawn/rebind) so it can't tick on a pooled instance.
            disposables.Add(Disposable.Create(() =>
            {
                LifecycleHelper.KillAndClear(ref _damageTween);
            }));
        }

        public void Initialize(IWriteableBalloonModel model, int levelAllowedColorsMask) { }

        private void OnHitsChanged(int hits, int maxHits, SerialDisposable warningPulse)
        {
            ApplyDamageProgress(hits, maxHits);

            // On its last hit the tough "breathes" its reserved color into the field at the profile's
            // cadence — alternating repel/attract pulses — so the danger reads in the specks too.
            warningPulse.Disposable = hits == 1 ? StartWarningPulse() : Disposable.Empty;
        }

        private IDisposable StartWarningPulse()
        {
            _repelPulse = false;
            return _disturbanceField.StartPulse(StampSource.ToughWarning, EmitAlternatingPulse);
        }

        // Each pulse flips sign — the tough pushes the field out, then pulls it back in — while always
        // tagging the specks its reserved color. Magnitude/radius come from the ToughWarning profile.
        // The same beat also puffs specks at the balloon (its ToughWarning speck profile), so the danger
        // reads as a rising swarm around the tough while it breathes.
        private void EmitAlternatingPulse()
        {
            var profile = _disturbanceField.GetProfile(StampSource.ToughWarning);
            var strength = Mathf.Abs(profile.Strength) * (_repelPulse ? 1f : -1f);
            _repelPulse = !_repelPulse;

            _disturbanceField.Stamp(
                transform.position, profile.Radius, strength, Vector2.zero, profile.Duration,
                _palette.PaletteIndexOf(GamePalette.ToughColorId), reportImpact: false);

            _speckPublisher?.Publish(new SpeckSpawnRequestMessage(SpeckSource.ToughWarning, transform.position));
        }

        private void ApplyDamageProgress(int hits, int maxHits)
        {
            if (maxHits <= 1)
            {
                return;
            }

            var target = Mathf.Clamp01(1f - ((hits - 1f) / (maxHits - 1f)));

            _damageTween?.Kill();
            _damageTween = DOVirtual
                .Float(_currentDamageProgress,
                    target,
                    _crackAnimDuration,
                    progress =>
                    {
                        _currentDamageProgress = progress;
                        _renderer.SetFloatAndApply(_block, DamageProgressId, progress);
                    })
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject);
        }
    }
}
