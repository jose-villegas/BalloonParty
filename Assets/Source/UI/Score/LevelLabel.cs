using DG.Tweening;
using BalloonParty.Game.Level;
using BalloonParty.Shared.Extensions;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace BalloonParty.UI.Score
{
    [RequireComponent(typeof(TMP_Text))]
    public class LevelLabel : MonoBehaviour
    {
        // Fraction of the duration spent tipping to the first edge-on point (where the text swaps); the
        // rest is the decelerating spin to rest.
        private const float EdgeFraction = 0.12f;

        [SerializeField] private bool _showNextLevel;

        [Tooltip("Transform flipped around X (vertically) to reveal the new level. Defaults to this " +
                 "object's transform.")]
        [SerializeField] private Transform _flipContainer;
        [SerializeField] private float _flipDuration = 0.9f;

        [Tooltip("Full vertical flips before it settles (each is a 360° turn around X).")]
        [SerializeField] private int _flipCount = 2;

        [Tooltip("Components disabled for the duration of the flip and re-enabled on completion.")]
        [SerializeField] private Behaviour[] _disableDuringFlip;

        [Tooltip("When enabled, Image components in the list fade their alpha instead of hard-disabling.")]
        [SerializeField] private bool _lerpImages = true;

        private TMP_Text _label;
        private Quaternion _baseRotation;
        private Vector3 _baseScale;
        private Sequence _flipSequence;
        private int _lastLevel = int.MinValue;
        private bool _pivotCentered;
        private bool _initialized;
        private float[] _imageBaseAlphas;

        private void Awake()
        {
            EnsureInitialized();
        }

        [Inject]
        private void Inject(LevelController levelController)
        {
            levelController.Level.Subscribe(OnLevelChanged).AddTo(this);
        }

        public void Bind(IReadOnlyReactiveProperty<int> level)
        {
            level.Subscribe(OnLevelChanged).AddTo(this);
        }

        // [Inject] can fire before Awake when VContainer builds during its own Awake, and UniRx's
        // Subscribe immediately emits the current value — so all state must be ready before first use.
        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _label = GetComponent<TMP_Text>();
            if (_flipContainer == null)
            {
                _flipContainer = transform;
            }

            _baseRotation = _flipContainer.localRotation;
            _baseScale = _flipContainer.localScale;
        }

        private void OnLevelChanged(int level)
        {
            var text = (level + (_showNextLevel ? 1 : 0)).ToString("N0");

            // Flip only on a level-up. The initial push and a run-reset back down just snap.
            if (level > _lastLevel && _lastLevel != int.MinValue)
            {
                _lastLevel = level;
                PlayFlip(text);
                return;
            }

            _lastLevel = level;
            SnapToText(text);
        }

        // A Y flip pivots on the RectTransform's pivot, so centre it (once, lazily — the rect size is
        // only valid after the first layout) and shift the position back so the label doesn't jump.
        private void EnsurePivotCentered()
        {
            if (_pivotCentered)
            {
                return;
            }

            _pivotCentered = true;

            var center = new Vector2(0.5f, 0.5f);
            if (_flipContainer is not RectTransform rect || rect.pivot == center)
            {
                return;
            }

            Vector3 delta = rect.pivot - center;
            delta.Scale(rect.rect.size);
            delta.Scale(rect.localScale);
            delta = rect.rotation * delta;

            rect.pivot = center;
            rect.localPosition -= delta;
        }

        private void SnapToText(string text)
        {
            EnsureInitialized();
            _flipSequence?.Kill();
            _flipContainer.localRotation = _baseRotation;
            _flipContainer.localScale = _baseScale;
            _label.text = text;
            SetFlipComponentsEnabled(true);
        }

        // Vertical card-flip reveal: a quick tip to the 90° edge (where the label is a line and
        // invisible) swaps in the new text, then it spins around X through several full flips, easing
        // out to rest. Full turns land back at identity, so no un-mirror is needed.
        private void PlayFlip(string newText)
        {
            EnsureInitialized();
            EnsurePivotCentered();

            _flipSequence?.Kill();
            _flipContainer.localRotation = _baseRotation;
            SetFlipComponentsEnabled(false);

            var totalAngle = 360f * Mathf.Max(1, _flipCount);
            var edgeDuration = _flipDuration * EdgeFraction;

            _flipSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

            if (_lerpImages)
            {
                CacheImageAlphas();
                AppendImageFades(_flipSequence, 0f, edgeDuration);
            }

            _flipSequence.Append(_flipContainer.DOLocalRotate(new Vector3(90f, 0f, 0f), edgeDuration)
                .SetEase(Ease.InSine));
            _flipSequence.AppendCallback(() => _label.text = newText);
            _flipSequence.Append(_flipContainer
                .DOLocalRotate(new Vector3(totalAngle, 0f, 0f), _flipDuration - edgeDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.OutCubic));

            if (_lerpImages)
            {
                AppendImageFades(_flipSequence, _imageBaseAlphas, edgeDuration);
            }

            _flipSequence.OnComplete(() =>
            {
                _flipContainer.localRotation = _baseRotation;
                SetFlipComponentsEnabled(true);
            });
        }

        private void CacheImageAlphas()
        {
            if (_disableDuringFlip == null)
            {
                return;
            }

            if (_imageBaseAlphas == null || _imageBaseAlphas.Length != _disableDuringFlip.Length)
            {
                _imageBaseAlphas = new float[_disableDuringFlip.Length];
            }

            for (var i = 0; i < _disableDuringFlip.Length; i++)
            {
                if (_disableDuringFlip[i] is Image image)
                {
                    _imageBaseAlphas[i] = image.GetAlphaAuto();
                }
            }
        }

        private void AppendImageFades(Sequence sequence, float targetAlpha, float duration)
        {
            var first = true;
            for (var i = 0; i < _disableDuringFlip.Length; i++)
            {
                if (_disableDuringFlip[i] is Image image)
                {
                    var tween = image.DOFadeAuto(targetAlpha, duration).SetEase(Ease.InSine);
                    if (first)
                    {
                        sequence.Append(tween);
                        first = false;
                    }
                    else
                    {
                        sequence.Join(tween);
                    }
                }
            }
        }

        private void AppendImageFades(Sequence sequence, float[] targetAlphas, float duration)
        {
            var first = true;
            for (var i = 0; i < _disableDuringFlip.Length; i++)
            {
                if (_disableDuringFlip[i] is Image image)
                {
                    var alpha = targetAlphas != null && i < targetAlphas.Length ? targetAlphas[i] : 1f;
                    var tween = image.DOFadeAuto(alpha, duration).SetEase(Ease.OutCubic);
                    if (first)
                    {
                        sequence.Append(tween);
                        first = false;
                    }
                    else
                    {
                        sequence.Join(tween);
                    }
                }
            }
        }

        private void SetFlipComponentsEnabled(bool enabled)
        {
            if (_disableDuringFlip == null)
            {
                return;
            }

            for (var i = 0; i < _disableDuringFlip.Length; i++)
            {
                if (_disableDuringFlip[i] == null)
                {
                    continue;
                }

                if (_lerpImages && _disableDuringFlip[i] is Image image)
                {
                    if (enabled && _imageBaseAlphas != null && i < _imageBaseAlphas.Length)
                    {
                        image.SetAlphaAuto(_imageBaseAlphas[i]);
                    }

                    continue;
                }

                _disableDuringFlip[i].enabled = enabled;
            }
        }
    }
}
