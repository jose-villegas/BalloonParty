using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BalloonParty.UI.CardReveal
{
    /// <summary>
    ///     One piece of ribbon with a line of the card written on it, flown off the popped balloon by
    ///     <see cref="CardRevealView" />. Before the pop it rests rolled up inside the balloon; once released it
    ///     bursts out to its spot, drifts and sways there while its wave runs and the text rides the wave, and at
    ///     the end flies off and fades. The ribbon sizes itself to its text.
    /// </summary>
    public class RibbonPiece : MonoBehaviour
    {
        [SerializeField] private RibbonGraphic _ribbon;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private CanvasGroup _group;

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

        private Vector3[][] _sourceVertices = Array.Empty<Vector3[]>();

        /// <summary>Writes <paramref name="line" /> on the ribbon and sizes the ribbon to it.</summary>
        internal void Show(string line)
        {
            _text.SetText(line);
            _text.ForceMeshUpdate();
            var rect = (RectTransform)transform;
            var height = rect.sizeDelta.y;
            rect.sizeDelta = new Vector2(_text.preferredWidth + 2f * _padding + height, height);
            _text.ForceMeshUpdate();
            CaptureVertices();
        }

        /// <summary>Rolled up inside the balloon, before the pop.</summary>
        internal void Rest()
        {
            Pose(_restPosition, _restTilt, _restScale, 1f, 0f);
        }

        /// <summary>
        ///     Its pose <paramref name="sinceRelease" /> seconds after the pop let it go: the burst out over
        ///     <paramref name="burstSeconds" />, then the drift and sway, and <paramref name="exit" /> (0 to 1) of
        ///     the way through its flight off.
        /// </summary>
        internal void Fly(float sinceRelease, float burstSeconds, float exit)
        {
            var burst = burstSeconds > 0f ? Mathf.Clamp01(sinceRelease / burstSeconds) : 1f;
            var burstOut = DOVirtual.EasedValue(0f, 1f, burst, Ease.OutCubic);
            var sway = Mathf.Sin(sinceRelease * 1.4f + _swayPhase);
            var away = exit * exit;

            var position = Vector2.LerpUnclamped(_restPosition, _burstTo, burstOut) + _drift * sinceRelease +
                           new Vector2(_sway.x * sway, 0f) + _exitBy * away;
            var tilt = Mathf.LerpUnclamped(_restTilt, _tilt, burstOut) + _sway.y * sway + _exitSpin * away;
            var scale = Mathf.Lerp(_restScale, 1f, burstOut);
            Pose(position, tilt, scale, 1f - away, sinceRelease);
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
        }

        private void CaptureVertices()
        {
            var meshes = _text.textInfo.meshInfo;
            if (_sourceVertices.Length < meshes.Length)
            {
                Array.Resize(ref _sourceVertices, meshes.Length);
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
            }
        }

        // Each glyph corner lifted by the ribbon's wave where it sits, so the writing rides the folds.
        private void BendText()
        {
            var info = _text.textInfo;
            for (var i = 0; i < info.characterCount; i++)
            {
                var character = info.characterInfo[i];
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
                for (var corner = 0; corner < 4; corner++)
                {
                    var index = character.vertexIndex + corner;
                    var source = _sourceVertices[mesh][index];
                    source.y += _ribbon.WaveAt(source.x);
                    vertices[index] = source;
                }
            }

            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }
    }
}
