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
    ///     effect. <see cref="SetThousands"/> gives an odometer roll where each digit spins
    ///     independently (ones fastest, higher digits slower). <see cref="SetText(string)"/>
    ///     and <see cref="SetText(char[],int)"/> apply a single-step per-character roll.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    internal class RollingTextAnimator : MonoBehaviour
    {
        private static readonly char[] FormattingBuffer = new char[16];
        private static readonly int[] Pow10 =
            { 1, 10, 100, 1_000, 10_000, 100_000, 1_000_000, 10_000_000, 100_000_000, 1_000_000_000 };

        [SerializeField] private float _rollDuration = 0.15f;
        [SerializeField] private float _staggerDelay = 0.03f;
        [SerializeField] private RollDirection _direction = RollDirection.Down;
        [SerializeField] private Ease _ease = Ease.OutCubic;
        [Tooltip("SmoothDamp time — how quickly the display chases the target value.")]
        [SerializeField] private float _smoothTime = 0.4f;
        [Tooltip("Per-digit roll speed multiplier per digit position (ones=1x, tens=2x, hundreds=4x...).")]
        [SerializeField] private float _digitRollScale = 2f;
        [Tooltip("How far characters travel as a fraction of line height (1 = full line height).")]
        [SerializeField, Range(0.1f, 2f)] private float _rollHeight = 1f;
        [Tooltip("Insert commas as thousands separators (off for arcade style).")]
        [SerializeField] private bool _useThousandsSeparator;

        private TMP_Text _text;
        private Sequence _sequence;
        private char[] _prevChars;
        private int _prevLength;
        private float[] _charProgress;
        private bool[] _charAnimating;
        private TMP_MeshInfo[] _cachedMeshInfo;
        private float[] _digitRollProgress;
        private float _displayedFloat;
        private float _smoothVelocity;
        private int _displayedValue;
        private int _previousDisplayedInt;
        private int _odometerTarget;
        private int _odometerDigitCount;
        private bool _odometerActive;
        private bool _hasDisplayedText;
        private bool _hasValue;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void Update()
        {
            if (!_odometerActive)
            {
                return;
            }

            _displayedFloat = Mathf.SmoothDamp(
                _displayedFloat, _odometerTarget, ref _smoothVelocity, _smoothTime);

            int currentInt = Mathf.RoundToInt(_displayedFloat);

            if (Mathf.Abs(_displayedFloat - _odometerTarget) < 0.01f)
            {
                currentInt = _odometerTarget;
                _displayedFloat = _odometerTarget;
            }

            AdvanceDigitRolls();

            if (currentInt != _previousDisplayedInt)
            {
                DetectDigitChanges(currentInt, _previousDisplayedInt);
                _previousDisplayedInt = currentInt;
                _displayedValue = currentInt;

                int len = TmpTextExtensions.FormatThousands(currentInt, FormattingBuffer, _useThousandsSeparator);
                _text.SetCharArray(FormattingBuffer, 0, len);
                _text.ForceMeshUpdate();
                _cachedMeshInfo = _text.textInfo.CopyMeshInfoVertexData();
            }

            ApplyOdometerOffsets();

            if (currentInt == _odometerTarget)
            {
                OnOdometerComplete();
            }
        }

        private void OnDisable()
        {
            FinishImmediately();
        }

        /// <summary>
        ///     Sets thousands-formatted integer text. The display smoothly chases the target
        ///     via SmoothDamp; each digit rolls independently with heavier digits settling slower.
        ///     Multiple rapid calls simply update the target — no animation is lost.
        /// </summary>
        public void SetThousands(int value)
        {
            EnsureText();

            if (!_hasValue)
            {
                int len = TmpTextExtensions.FormatThousands(value, FormattingBuffer, _useThousandsSeparator);
                ApplyTextDirect(FormattingBuffer, len);
                _displayedValue = value;
                _displayedFloat = value;
                _odometerTarget = value;
                _previousDisplayedInt = value;
                _hasValue = true;
                return;
            }

            if (value == _odometerTarget)
            {
                return;
            }

            _odometerTarget = value;

            int maxVal = Mathf.Max(Mathf.Abs(_displayedValue), Mathf.Abs(value));
            int digitCount = maxVal > 0 ? Mathf.FloorToInt(Mathf.Log10(maxVal)) + 1 : 1;

            if (!_odometerActive)
            {
                _odometerDigitCount = digitCount;
                _odometerActive = true;
                _displayedFloat = _displayedValue;
                _previousDisplayedInt = _displayedValue;
                EnsureDigitCapacity(digitCount);
                KillSequence();
            }
            else if (digitCount > _odometerDigitCount)
            {
                EnsureDigitCapacity(digitCount);
                _odometerDigitCount = digitCount;
            }
        }

        /// <summary>Sets text from a char array (zero-alloc) and animates changed characters.</summary>
        public void SetText(char[] chars, int length)
        {
            EnsureText();
            ApplyTextAnimated(chars, length);
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

        private void AdvanceDigitRolls()
        {
            float dt = Time.deltaTime;
            for (int d = 0; d < _odometerDigitCount; d++)
            {
                if (_digitRollProgress[d] < 1f)
                {
                    float digitDuration = _rollDuration * Mathf.Pow(_digitRollScale, d);
                    _digitRollProgress[d] = Mathf.Min(_digitRollProgress[d] + dt / digitDuration, 1f);
                }
            }
        }

        private void DetectDigitChanges(int current, int previous)
        {
            for (int d = 0; d < _odometerDigitCount; d++)
            {
                int newDigit = d < Pow10.Length ? (current / Pow10[d]) % 10 : 0;
                int oldDigit = d < Pow10.Length ? (previous / Pow10[d]) % 10 : 0;

                if (newDigit != oldDigit)
                {
                    _digitRollProgress[d] = 0f;
                }
            }
        }

        private void EnsureDigitCapacity(int digitCount)
        {
            if (_digitRollProgress == null || _digitRollProgress.Length < digitCount)
            {
                var expanded = new float[Mathf.Max(digitCount, 10)];
                if (_digitRollProgress != null)
                {
                    for (int d = 0; d < _odometerDigitCount; d++)
                    {
                        expanded[d] = _digitRollProgress[d];
                    }
                }

                _digitRollProgress = expanded;
            }

            for (int d = _odometerDigitCount; d < digitCount; d++)
            {
                _digitRollProgress[d] = 1f;
            }
        }

        private void ApplyOdometerOffsets()
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

            int digitPos = 0;
            for (int i = textInfo.characterCount - 1; i >= 0; i--)
            {
                var charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible)
                {
                    continue;
                }

                if (charInfo.character < '0' || charInfo.character > '9')
                {
                    continue;
                }

                if (digitPos >= _odometerDigitCount)
                {
                    break;
                }

                float progress = Mathf.Clamp01(_digitRollProgress[digitPos]);

                if (progress < 1f)
                {
                    int matIdx = charInfo.materialReferenceIndex;
                    int vertIdx = charInfo.vertexIndex;
                    float yOffset = lineHeight * _rollHeight * (1f - progress) * dirSign;
                    byte alpha = (byte)(progress * 255f);

                    var dst = textInfo.meshInfo[matIdx].vertices;
                    var src = _cachedMeshInfo[matIdx].vertices;
                    var colors = textInfo.meshInfo[matIdx].colors32;

                    for (int v = 0; v < 4; v++)
                    {
                        dst[vertIdx + v] = src[vertIdx + v] + new Vector3(0f, yOffset, 0f);
                        var c = colors[vertIdx + v];
                        c.a = alpha;
                        colors[vertIdx + v] = c;
                    }
                }

                digitPos++;
            }

            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        private void ApplyTextDirect(char[] chars, int length)
        {
            _text.SetCharArray(chars, 0, length);
            _text.ForceMeshUpdate();
            SnapshotCurrent();
            _hasDisplayedText = true;
        }

        private void ApplyTextAnimated(char[] chars, int length)
        {
            if (!_hasDisplayedText)
            {
                ApplyTextDirect(chars, length);
                return;
            }

            SnapshotCurrent();
            _text.SetCharArray(chars, 0, length);
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
                float yOffset = lineHeight * _rollHeight * (1f - progress) * dirSign;
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

            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
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

        private void OnOdometerComplete()
        {
            _odometerActive = false;
            _smoothVelocity = 0f;
            _displayedValue = _odometerTarget;
            _displayedFloat = _odometerTarget;

            int len = TmpTextExtensions.FormatThousands(_odometerTarget, FormattingBuffer, _useThousandsSeparator);
            _text.SetCharArray(FormattingBuffer, 0, len);
            _text.ForceMeshUpdate();
            _cachedMeshInfo = null;

            SnapshotCurrent();
        }

        private void FinishImmediately()
        {
            KillSequence();
            _odometerActive = false;
            _smoothVelocity = 0f;

            if (_hasValue)
            {
                _displayedValue = _odometerTarget;
                _displayedFloat = _odometerTarget;

                if (_text != null)
                {
                    int len = TmpTextExtensions.FormatThousands(_odometerTarget, FormattingBuffer, _useThousandsSeparator);
                    _text.SetCharArray(FormattingBuffer, 0, len);
                    _text.ForceMeshUpdate();
                }
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
