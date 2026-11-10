using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BalloonParty.UI.HeadsUp
{
    /// <summary>
    ///     Writes a <see cref="TMP_Text" /> on left to right as if by a pencil: a soft reveal edge, slanted
    ///     forward, sweeps across the glyphs while an optional pencil tip rides it. Works on any TMP text,
    ///     on a canvas or in the world. Ticks on unscaled time, so it keeps writing through slow-motion and
    ///     frozen screens, and only while writing: a written text costs nothing.
    /// </summary>
    public class WriteOnText : MonoBehaviour
    {
        // Declared by a shader that takes the reveal from uv0.z (BalloonParty/Text/Pencil, whose ragged edge
        // needs it apart from opacity), so vertex alpha stays the text's own fades and colours. TMP leaves
        // uv0.z at 0, which that shader reads as whole.
        private static readonly int RevealInUvProperty = Shader.PropertyToID("_PencilRevealInUv");

        [SerializeField] private TMP_Text _text;

        [Tooltip("Optional pencil tip that rides the reveal edge while writing, hidden otherwise.")]
        [SerializeField] private Transform _tip;

        [SerializeField] [Min(0.005f)] private float _secondsPerCharacter = 0.07f;

        [Tooltip("How many characters the soft reveal edge spans.")]
        [SerializeField] [Range(0.05f, 3f)] private float _edgeSoftness = 0.8f;

        [Tooltip("How far, in characters, the top of the edge leads its bottom: a forward-leaning, pencil-like edge.")]
        [SerializeField] [Range(0f, 1f)] private float _slant = 0.35f;

        [Tooltip("The tip's height on the line: 0 at the baseline, 1 at the ascender.")]
        [SerializeField] [Range(0f, 1f)] private float _tipHeight = 0.35f;

        [Tooltip("The tip's up-and-down wobble, as a fraction of the line height.")]
        [SerializeField] [Min(0f)] private float _tipWobble = 0.06f;

        [SerializeField] [Min(0f)] private float _tipWobbleSpeed = 40f;

        // The colours TMP generated, so the reveal scales each vertex's own alpha (rich-text colours,
        // gradients) instead of overwriting it. One per mesh (font and fallback), grown, never shrunk.
        private readonly List<Color32[]> _sourceColors = new();

        // Per mesh, as above: whether its material takes the reveal in uv0.z rather than in vertex alpha.
        private readonly List<bool> _revealInUv = new();

        private float _head;
        private float _startHead;
        private float _endHead;
        private bool _writing;

        /// <summary>Seconds the last <see cref="Write" /> takes to finish, its delay included.</summary>
        public float Duration => (_endHead - _startHead) * _secondsPerCharacter;

        private void Awake()
        {
            enabled = _writing;
            if (!_writing)
            {
                SetTipVisible(false);
            }
        }

        // Only while writing (the component disables itself once written): a mesh TMP rebuilds mid-write
        // (a canvas rebuild, a re-enable) comes back fully opaque, so it's re-captured and re-hidden at once.
        private void OnEnable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        }

        private void Update()
        {
            _head += Time.unscaledDeltaTime / _secondsPerCharacter;
            if (_head >= _endHead)
            {
                Complete();
                return;
            }

            ApplyReveal();
            MoveTip();
        }

        private void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        }

        /// <summary>
        ///     Sets the text, hidden, and writes it on after <paramref name="delaySeconds" />: a label that
        ///     fades in first waits for its fade, or a short line is written before it can be seen.
        /// </summary>
        public void Write(string text, float delaySeconds = 0f)
        {
            _text.SetText(text);

            // Laid out and hidden in this one call: a pooled or reused label must not flash fully
            // written for a frame before the first tick.
            _text.ForceMeshUpdate();
            CaptureSourceColors();
            _startHead = WriteOnMath.StartHead(delaySeconds, _secondsPerCharacter);
            _head = _startHead;
            _endHead = WriteOnMath.EndHead(_text.textInfo.characterCount, _edgeSoftness);
            _writing = true;
            enabled = true;
            ApplyReveal();
            SetTipVisible(true);
            MoveTip();
        }

        /// <summary>Finishes the current write at once.</summary>
        public void Complete()
        {
            if (!_writing)
            {
                return;
            }

            _head = _endHead;
            ApplyReveal();
            Stop();
        }

        /// <summary>Stops the current write where it is, the pencil lifted: for a line going away mid-write.</summary>
        public void Stop()
        {
            _writing = false;
            enabled = false;
            SetTipVisible(false);
        }

        private void CaptureSourceColors()
        {
            var meshes = _text.textInfo.meshInfo;
            for (var m = 0; m < meshes.Length; m++)
            {
                var material = meshes[m].material;
                var revealInUv = material != null && material.HasProperty(RevealInUvProperty);
                if (m == _revealInUv.Count)
                {
                    _revealInUv.Add(revealInUv);
                }
                else
                {
                    _revealInUv[m] = revealInUv;
                }

                var colors = meshes[m].colors32;
                var length = colors?.Length ?? 0;
                if (m == _sourceColors.Count)
                {
                    _sourceColors.Add(new Color32[length]);
                }
                else if (_sourceColors[m].Length < length)
                {
                    _sourceColors[m] = new Color32[length];
                }

                if (length > 0)
                {
                    Array.Copy(colors, _sourceColors[m], length);
                }
            }
        }

        private void ApplyReveal()
        {
            var info = _text.textInfo;
            var pushed = TMP_VertexDataUpdateFlags.None;
            for (var i = 0; i < info.characterCount; i++)
            {
                var character = info.characterInfo[i];
                if (!character.isVisible)
                {
                    continue;
                }

                var mesh = character.materialReferenceIndex;
                if (mesh >= _sourceColors.Count || character.vertexIndex + 3 >= _sourceColors[mesh].Length)
                {
                    continue;
                }

                var revealInUv = _revealInUv[mesh];
                pushed |= revealInUv ? TMP_VertexDataUpdateFlags.Uv0 : TMP_VertexDataUpdateFlags.Colors32;
                var meshInfo = info.meshInfo[mesh];

                // TMP's quad order: bottom-left, top-left, top-right, bottom-right.
                for (var corner = 0; corner < 4; corner++)
                {
                    var right = corner >= 2;
                    var top = corner == 1 || corner == 2;
                    var position = WriteOnMath.CornerPosition(i, right, top, _slant);
                    var alpha = WriteOnMath.RevealAlpha(_head, position, _edgeSoftness);

                    var vertex = character.vertexIndex + corner;
                    if (revealInUv)
                    {
                        var uv = meshInfo.uvs0[vertex];
                        uv.z = 1f - alpha;
                        meshInfo.uvs0[vertex] = uv;
                    }
                    else
                    {
                        var color = _sourceColors[mesh][vertex];
                        color.a = (byte)(color.a * alpha);
                        meshInfo.colors32[vertex] = color;
                    }
                }
            }

            if (pushed != TMP_VertexDataUpdateFlags.None)
            {
                _text.UpdateVertexData(pushed);
            }
        }

        // On the edge's midpoint, so the tip sits where the graphite is going down.
        private void MoveTip()
        {
            if (_tip == null)
            {
                return;
            }

            var info = _text.textInfo;
            if (info.characterCount == 0)
            {
                return;
            }

            var at = Mathf.Clamp(_head - _edgeSoftness * 0.5f, 0f, info.characterCount);
            var index = Mathf.Min((int)at, info.characterCount - 1);
            var character = info.characterInfo[index];
            var x = Mathf.Lerp(character.origin, character.xAdvance, Mathf.Clamp01(at - index));
            var lineHeight = character.ascender - character.baseLine;
            var wobble = Mathf.Sin(Time.unscaledTime * _tipWobbleSpeed) * _tipWobble;
            var y = character.baseLine + lineHeight * (_tipHeight + wobble);
            _tip.position = _text.transform.TransformPoint(new Vector3(x, y, 0f));
        }

        private void OnTextChanged(UnityEngine.Object changed)
        {
            if (!_writing || changed != _text)
            {
                return;
            }

            CaptureSourceColors();
            ApplyReveal();
        }

        private void SetTipVisible(bool visible)
        {
            if (_tip != null)
            {
                _tip.gameObject.SetActive(visible);
            }
        }
    }
}
