using DG.Tweening;
using TMPro;
using UniRx;
using UnityEngine;

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

        private TMP_Text _label;
        private Quaternion _baseRotation;
        private Vector3 _baseScale;
        private Sequence _flipSequence;
        private int _lastLevel = int.MinValue;
        private bool _pivotCentered;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            if (_flipContainer == null)
            {
                _flipContainer = transform;
            }

            _baseRotation = _flipContainer.localRotation;
            _baseScale = _flipContainer.localScale;
        }

        public void Bind(IReadOnlyReactiveProperty<int> level)
        {
            level.Subscribe(OnLevelChanged).AddTo(this);
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
            _flipSequence?.Kill();
            _flipContainer.localRotation = _baseRotation;
            _flipContainer.localScale = _baseScale;
            _label.text = text;
        }

        // Vertical card-flip reveal: a quick tip to the 90° edge (where the label is a line and
        // invisible) swaps in the new text, then it spins around X through several full flips, easing
        // out to rest. Full turns land back at identity, so no un-mirror is needed.
        private void PlayFlip(string newText)
        {
            EnsurePivotCentered();

            _flipSequence?.Kill();
            _flipContainer.localRotation = _baseRotation;

            var totalAngle = 360f * Mathf.Max(1, _flipCount);
            var edgeDuration = _flipDuration * EdgeFraction;

            _flipSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _flipSequence.Append(_flipContainer.DOLocalRotate(new Vector3(90f, 0f, 0f), edgeDuration)
                .SetEase(Ease.InSine));
            _flipSequence.AppendCallback(() => _label.text = newText);
            _flipSequence.Append(_flipContainer
                .DOLocalRotate(new Vector3(totalAngle, 0f, 0f), _flipDuration - edgeDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.OutCubic));
            _flipSequence.OnComplete(() => _flipContainer.localRotation = _baseRotation);
        }
    }
}
