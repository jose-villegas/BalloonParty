using System;
using BalloonParty.Configuration.CardReveal;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BalloonParty.UI.CardReveal
{
    /// <summary>
    ///     One piece of ribbon with a line of the card written on it, flown off the popped balloon by
    ///     <see cref="CardRevealView" />. Before the pop it rests rolled up inside the balloon; once released it
    ///     bursts out to its spot, drifts and sways there while its wave runs and the text rides the wave, and at
    ///     the end flies off and fades. The ribbon sizes itself to its text. A line can be struck through
    ///     (<see cref="RibbonStrike" />), and the whole piece can crumble away (<see cref="Dissolve" />): the
    ///     ribbon in patches, each letter fading as it drops out of its patch.
    /// </summary>
    public class RibbonPiece : MonoBehaviour
    {
        [SerializeField] private RibbonGraphic _ribbon;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private CanvasGroup _group;

        [Tooltip("The stroke through a struck line. None, and a struck line falls back to TMP's flat strikethrough.")]
        [SerializeField] private RibbonStrike _strike;

        [Tooltip("Room left and right of the text, inside the notches, in canvas units.")]
        [SerializeField] [Min(0f)] private float _padding = 7f;

        [Header("Inside the balloon")]
        [SerializeField] private Vector2 _restPosition;
        [SerializeField] private float _restTilt;
        [SerializeField] [Range(0.1f, 1f)] private float _restScale = 0.7f;

        [Header("Flying")]
        [Tooltip("Where the burst carries it, from the balloon's centre, in canvas units.")]
        [SerializeField] private Vector2 _burstTo;

        [SerializeField] private float _tilt;

        [Tooltip("How fast it drifts while it can be read, in canvas units a second.")]
        [SerializeField] private Vector2 _drift = new(0f, 6f);

        [Tooltip("Its side-to-side sway: how far, in canvas units, and how far it rocks, in degrees.")]
        [SerializeField] private Vector2 _sway = new(2.5f, 3f);

        [Tooltip("Offsets this piece's sway from the others', so they don't rock together.")]
        [SerializeField] private float _swayPhase;

        [Tooltip("Where it flies off to over its exit, from its spot, in canvas units.")]
        [SerializeField] private Vector2 _exitBy = new(0f, 120f);

        [SerializeField] private float _exitSpin = 12f;

        [Tooltip("The widest it may swell to after the burst, in canvas units, so a long line stays on screen.")]
        [SerializeField] [Min(1f)] private float _widest = 205f;

        [Tooltip("How far a letter drops as it crumbles away in a dissolve, in canvas units.")]
        [SerializeField] [Min(0f)] private float _crumbleDrop = 8f;

        private Vector3[][] _sourceVertices = Array.Empty<Vector3[]>();
        private Color32[][] _sourceColors = Array.Empty<Color32[]>();
        private float _dissolve;

        /// <summary>
        ///     Writes <paramref name="line" /> on the ribbon, <paramref name="struck" /> through or not, and sizes the
        ///     ribbon to it. The ribbon comes back whole.
        /// </summary>
        internal void Show(string line, bool struck = false)
        {
            _text.SetText(struck && _strike == null ? $"<s>{line}</s>" : line);
            _text.ForceMeshUpdate();
            var rect = (RectTransform)transform;
            var height = rect.sizeDelta.y;
            rect.sizeDelta = new Vector2(_text.preferredWidth + 2f * _padding + height, height);
            _text.ForceMeshUpdate();
            CaptureVertices();
            if (_strike != null)
            {
                _strike.gameObject.SetActive(struck);
                if (struck)
                {
                    StrikeThroughText();
                }
            }

            Dissolve(0f);
        }

        /// <summary>
        ///     Crumbles the piece <paramref name="progress" /> (0 to 1) of the way away; the next pose shows it.
        /// </summary>
        internal void Dissolve(float progress)
        {
            _dissolve = progress;
            _ribbon.Dissolve = progress;
            if (_strike != null)
            {
                _strike.Dissolve = progress;
            }
        }

        /// <summary>Rolled up inside the balloon, before the pop.</summary>
        internal void Rest()
        {
            Pose(_restPosition, _restTilt, _restScale, 1f, 0f);
        }

        /// <summary>
        ///     Its pose <paramref name="sinceRelease" /> seconds after the pop let it go: the burst out, then the
        ///     swell, drift and sway, and <paramref name="exit" /> (0 to 1) of the way through its flight off.
        /// </summary>
        internal void Fly(float sinceRelease, float exit, ICardRevealSettings settings)
        {
            var burstSeconds = settings.RibbonBurstSeconds;
            var burst = burstSeconds > 0f ? Mathf.Clamp01(sinceRelease / burstSeconds) : 1f;
            var burstOut = DOVirtual.EasedValue(0f, 1f, burst, Ease.OutCubic);
            var growSeconds = settings.RibbonGrowSeconds;
            var grow = growSeconds > 0f ? Mathf.Clamp01((sinceRelease - burstSeconds) / growSeconds) : 1f;
            var grown = GrownScale(settings.RibbonGrowth, ((RectTransform)transform).sizeDelta.x, _widest);
            var sway = Mathf.Sin(sinceRelease * 1.4f + _swayPhase);
            var away = exit * exit;

            var position = Vector2.LerpUnclamped(_restPosition, _burstTo, burstOut) + _drift * sinceRelease +
                           new Vector2(_sway.x * sway, 0f) + _exitBy * away;
            var tilt = Mathf.LerpUnclamped(_restTilt, _tilt, burstOut) + _sway.y * sway + _exitSpin * away;
            var scale = Mathf.Lerp(_restScale, 1f, burstOut) *
                        Mathf.Lerp(1f, grown, DOVirtual.EasedValue(0f, 1f, grow, Ease.InOutSine));
            Pose(position, tilt, scale, 1f - away, sinceRelease);
        }

        /// <summary>
        ///     The scale a ribbon <paramref name="width" /> wide swells to: <paramref name="growth" />, unless that
        ///     would take it past <paramref name="widest" />, and never below its own size.
        /// </summary>
        internal static float GrownScale(float growth, float width, float widest)
        {
            return width > 0f ? Mathf.Clamp(widest / width, 1f, growth) : growth;
        }

        private void Pose(Vector2 position, float tilt, float scale, float alpha, float waveTime)
        {
            var rect = (RectTransform)transform;
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0f, 0f, tilt);
            rect.localScale = new Vector3(scale, scale, 1f);
            _group.alpha = alpha;
            _ribbon.WaveTime = waveTime;
            BendText();
            if (_strike != null && _strike.gameObject.activeSelf)
            {
                _strike.Refresh();
            }
        }

        // Across the written letters, at the height of the letters themselves (an icon may stand taller).
        private void StrikeThroughText()
        {
            var info = _text.textInfo;
            var from = float.MaxValue;
            var to = float.MinValue;
            var baseline = 0f;
            var top = 0f;
            var letters = 0;
            for (var i = 0; i < info.characterCount; i++)
            {
                ref var character = ref info.characterInfo[i];
                if (!character.isVisible)
                {
                    continue;
                }

                from = Mathf.Min(from, character.bottomLeft.x);
                to = Mathf.Max(to, character.topRight.x);
                if (character.elementType == TMP_TextElementType.Character)
                {
                    baseline += character.baseLine;
                    top += character.ascender;
                    letters++;
                }
            }

            if (letters == 0)
            {
                _strike.gameObject.SetActive(false);
                return;
            }

            _strike.color = _text.color;
            _strike.Strike(from, to, baseline / letters, top / letters);
        }

        private void CaptureVertices()
        {
            var meshes = _text.textInfo.meshInfo;
            if (_sourceVertices.Length < meshes.Length)
            {
                Array.Resize(ref _sourceVertices, meshes.Length);
                Array.Resize(ref _sourceColors, meshes.Length);
            }

            for (var m = 0; m < meshes.Length; m++)
            {
                var vertices = meshes[m].vertices;
                var length = vertices?.Length ?? 0;
                if (_sourceVertices[m] == null || _sourceVertices[m].Length < length)
                {
                    _sourceVertices[m] = new Vector3[length];
                }

                if (length > 0)
                {
                    Array.Copy(vertices, _sourceVertices[m], length);
                }

                var colors = meshes[m].colors32;
                var colorCount = colors?.Length ?? 0;
                if (_sourceColors[m] == null || _sourceColors[m].Length < colorCount)
                {
                    _sourceColors[m] = new Color32[colorCount];
                }

                if (colorCount > 0)
                {
                    Array.Copy(colors, _sourceColors[m], colorCount);
                }
            }
        }

        // Each glyph corner lifted by the ribbon's wave where it sits, so the writing rides the folds. In a
        // dissolve each glyph fades and drops as the ribbon under its middle crumbles.
        private void BendText()
        {
            var info = _text.textInfo;
            var halfWidth = ((RectTransform)transform).rect.width * 0.5f;
            for (var i = 0; i < info.characterCount; i++)
            {
                ref var character = ref info.characterInfo[i];
                if (!character.isVisible)
                {
                    continue;
                }

                var mesh = character.materialReferenceIndex;
                if (mesh >= _sourceVertices.Length || character.vertexIndex + 3 >= _sourceVertices[mesh].Length)
                {
                    continue;
                }

                var vertices = info.meshInfo[mesh].vertices;
                var colors = info.meshInfo[mesh].colors32;
                var first = character.vertexIndex;
                var middle = (_sourceVertices[mesh][first].x + _sourceVertices[mesh][first + 2].x) * 0.5f;
                var crumbled = RibbonGraphic.DissolvedAt(middle, halfWidth, _dissolve);
                for (var corner = 0; corner < 4; corner++)
                {
                    var index = first + corner;
                    var source = _sourceVertices[mesh][index];
                    source.y += _ribbon.WaveAt(source.x) - crumbled * _crumbleDrop;
                    vertices[index] = source;
                    if (index < _sourceColors[mesh].Length && index < colors.Length)
                    {
                        var tint = _sourceColors[mesh][index];
                        tint.a = (byte)(tint.a * (1f - crumbled));
                        colors[index] = tint;
                    }
                }
            }

            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
