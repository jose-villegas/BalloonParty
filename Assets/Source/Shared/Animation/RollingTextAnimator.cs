using BalloonParty.Shared.Extensions;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BalloonParty.Shared.Animation
{
    internal enum RollDirection
    {
        /// <summary>New characters enter from above and slide downward into place.</summary>
        Down,
        /// <summary>New characters enter from below and slide upward into place.</summary>
        Up
    }

    /// <summary>
    ///     Animates character changes on a <see cref="TMP_Text"/> with a rolling flipboard
    ///     effect. Each changed character slides in from above or below while fading in,
    ///     with a right-to-left stagger for a mechanical counter feel.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    internal class RollingTextAnimator : MonoBehaviour
    {
        private static readonly char[] ThousandsBuffer = new char[16];

        [SerializeField] private float _rollDuration = 0.25f;
        [SerializeField] private float _staggerDelay = 0.03f;
        [SerializeField] private RollDirection _direction = RollDirection.Down;
        [SerializeField] private Ease _ease = Ease.OutCubic;

        private TMP_Text _text;
        private Sequence _sequence;
        private char[] _prevChars;
        private int _prevLength;
        private float[] _charProgress;
        private bool[] _charAnimating;
        private TMP_MeshInfo[] _cachedMeshInfo;
        private bool _hasDisplayedText;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void OnDisable()
        {
            FinishImmediately();
        }

        /// <summary>
        ///     Sets thousands-formatted integer text and animates changed digits.
        ///     Zero-alloc hot path for score and counter labels.
        /// </summary>
        public void SetThousands(int value)
        {
            int length = TmpTextExtensions.FormatThousands(value, ThousandsBuffer);
            SetText(ThousandsBuffer, length);
        }

        /// <summary>Sets text from a char array (zero-alloc) and animates changed characters.</summary>
        public void SetText(char[] chars, int length)
        {
            EnsureText();

            if (!_hasDisplayedText)
            {
                _text.SetCharArray(chars, 0, length);
                _text.ForceMeshUpdate();
                SnapshotCurrent();
                _hasDisplayedText = true;
                return;
            }

            SnapshotCurrent();
            _text.SetCharArray(chars, 0, length);
            _text.ForceMeshUpdate();
            AnimateChanges();
        }

        /// <summary>Sets text from a string and animates changed characters.</summary>
        public void SetText(string newText)
        {
            EnsureText();

            if (!_hasDisplayedText)
            {
                _text.text = newText;
                _text.ForceMeshUpdate();
                SnapshotCurrent();
                _hasDisplayedText = true;
                return;
            }

            SnapshotCurrent();
            _text.text = newText;
            _text.ForceMeshUpdate();
            AnimateChanges();
        }

        private void EnsureText()
        {
            if (_text == null)
            {
                _text = GetComponent<TMP_Text>();
            }
        }

        private void SnapshotCurrent()
        {
            var textInfo = _text.textInfo;
            int count = textInfo.characterCount;

            if (_prevChars == null || _prevChars.Length < count)
            {
                _prevChars = new char[Mathf.Max(count, 16)];
            }

            for (int i = 0; i < count; i++)
            {
                _prevChars[i] = textInfo.characterInfo[i].character;
            }

            _prevLength = count;
        }

        private void AnimateChanges()
        {
            var textInfo = _text.textInfo;
            int charCount = textInfo.characterCount;

            _cachedMeshInfo = textInfo.CopyMeshInfoVertexData();

            if (_charProgress == null || _charProgress.Length < charCount)
            {
                _charProgress = new float[Mathf.Max(charCount, 16)];
                _charAnimating = new bool[Mathf.Max(charCount, 16)];
            }

            for (int i = 0; i < charCount; i++)
            {
                _charProgress[i] = 1f;
                _charAnimating[i] = false;
            }

            KillSequence();
            _sequence = DOTween.Sequence();
            int staggerIdx = 0;
            bool anyChanged = false;

            for (int i = charCount - 1; i >= 0; i--)
            {
                var charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible)
                {
                    continue;
                }

                bool changed = i >= _prevLength || _prevChars[i] != charInfo.character;
                if (!changed)
                {
                    continue;
                }

                _charProgress[i] = 0f;
                _charAnimating[i] = true;
                anyChanged = true;

                int captured = i;
                float delay = staggerIdx * _staggerDelay;
                _sequence.Insert(delay,
                    DOVirtual.Float(0f, 1f, _rollDuration, p => _charProgress[captured] = p)
                        .SetEase(_ease));
                staggerIdx++;
            }

            if (!anyChanged)
            {
                _sequence.Kill();
                _sequence = null;
                SnapshotCurrent();
                return;
            }

            ApplyVertexModifications();

            _sequence.OnUpdate(ApplyVertexModifications);
            _sequence.OnComplete(OnAnimationComplete);
            _sequence.SetLink(gameObject);
        }

        private void ApplyVertexModifications()
        {
            if (_cachedMeshInfo == null)
            {
                return;
            }

            var textInfo = _text.textInfo;
            float lineHeight = textInfo.lineCount > 0
                ? textInfo.lineInfo[0].lineHeight
                : _text.fontSize;
            float dirSign = _direction == RollDirection.Down ? 1f : -1f;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (!_charAnimating[i])
                {
                    continue;
                }

                var charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible)
                {
                    continue;
                }

                int matIdx = charInfo.materialReferenceIndex;
                int vertIdx = charInfo.vertexIndex;
                float progress = Mathf.Clamp01(_charProgress[i]);
                float yOffset = lineHeight * (1f - progress) * dirSign;
                byte alpha = (byte)(progress * 255f);

                var dstVerts = textInfo.meshInfo[matIdx].vertices;
                var srcVerts = _cachedMeshInfo[matIdx].vertices;
                var colors = textInfo.meshInfo[matIdx].colors32;

                for (int v = 0; v < 4; v++)
                {
                    dstVerts[vertIdx + v] = srcVerts[vertIdx + v] + new Vector3(0f, yOffset, 0f);
                    var c = colors[vertIdx + v];
                    c.a = alpha;
                    colors[vertIdx + v] = c;
                }
            }

            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        private void OnAnimationComplete()
        {
            _cachedMeshInfo = null;
            _sequence = null;

            if (_charAnimating != null)
            {
                for (int i = 0; i < _charAnimating.Length; i++)
                {
                    _charAnimating[i] = false;
                    _charProgress[i] = 1f;
                }
            }

            SnapshotCurrent();
        }

        private void FinishImmediately()
        {
            KillSequence();

            if (_text != null)
            {
                _text.ForceMeshUpdate();
            }

            _cachedMeshInfo = null;
        }

        private void KillSequence()
        {
            if (_sequence != null && _sequence.IsActive())
            {
                _sequence.Kill();
            }

            _sequence = null;
        }
    }
}
