using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using DG.Tweening;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Type
{
    public class ToughBalloonVariant : MonoBehaviour, IBalloonVariant, IBalloonViewBinding
    {
        private static readonly int DamageProgressId = Shader.PropertyToID("_DamageProgress");
        private static readonly int VoronoiSeedId = Shader.PropertyToID("_VoronoiSeed");

        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private float _crackAnimDuration = 0.5f;

        private MaterialPropertyBlock _block;
        private Tween _damageTween;
        private float _currentDamageProgress;


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
                    "— crack damage visuals will be disabled. Fix the prefab.", this);
                return;
            }

            // Kill any in-flight tween from a previous pool cycle
            _damageTween?.Kill();
            _currentDamageProgress = 0f;

            SetFloat(DamageProgressId, 0f);
            SetVector(VoronoiSeedId, new Vector4(Random.Range(-999f, 999f), Random.Range(-999f, 999f)));

            var maxHits = model.HitsRemaining.Value;

            model.HitsRemaining
                .Subscribe(hits =>
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
                            v =>
                            {
                                _currentDamageProgress = v;
                                SetFloat(DamageProgressId, v);
                            })
                        .SetEase(Ease.OutCubic)
                        .SetLink(gameObject);
                })
                .AddTo(disposables);
        }

        public void Initialize(IWriteableBalloonModel model) { }

        private void SetFloat(int id, float value)
        {
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(id, value);
            _renderer.SetPropertyBlock(_block);
        }

        private void SetVector(int id, Vector4 value)
        {
            _renderer.GetPropertyBlock(_block);
            _block.SetVector(id, value);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
