using BalloonParty.Configuration.HeadsUp;
using BalloonParty.Game.HeadsUp;
using BalloonParty.Shared.Pool;
using TMPro;
using UnityEngine;

namespace BalloonParty.UI.HeadsUp
{
    /// <summary>
    ///     One short handwritten line in world space (a <c>TextMeshPro</c>, not on a canvas), so it sits where
    ///     it happened through the camera rig's zoom and shake: written on in pencil, held, then faded out, or
    ///     held until released. Pooled by <see cref="HeadsUpLayer" />, which moves it on with two clocks: the
    ///     write, the hold and its own fade on live time, which stops under a pause; a release fades on unscaled
    ///     time, so a line pushed out during a pause doesn't stay frozen beside the one that replaced it.
    /// </summary>
    internal sealed class HandwrittenHeadsUp : MonoBehaviour, IPoolable
    {
        [SerializeField] private WriteOnText _writeOn;
        [SerializeField] [Range(0f, 1f)] private float _visibleAlpha = 0.85f;
        [SerializeField] [Min(0.01f)] private float _fadeOutDuration = 0.25f;

        [Tooltip("Fade length when the line is released early: evicted, cleared, or what it announced happened.")]
        [SerializeField] [Min(0.01f)] private float _clearDuration = 0.1f;

        [Tooltip("A soft outline behind the ink, so dark lines read over the day sky and the night sky alike: " +
                 "pencil on a scrap of paper. Width 0 = none.")]
        [SerializeField] private Color _haloColor = new(1f, 0.973f, 0.906f);
        [SerializeField] [Range(0f, 1f)] private float _haloWidth = 0.25f;

        private TMP_Text _label;
        private Material _material;
        private Color _faceColor = Color.white;
        private float _baseFontSize;
        private LineState _state;
        private float _alpha;
        private float _timer;
        private float _fadeFrom;
        private float _holdSeconds;
        private bool _held;
        private string _text;
        private string _pendingText;
        private HeadsUpStyleSettings _pendingStyle;
        private int _pendingArg;

        private enum LineState
        {
            Hidden,
            Writing,
            Holding,
            Held,
            Fading,
            Releasing
        }

        internal HeadsUpHandle Handle { get; private set; } = HeadsUpHandle.None;

        /// <summary>Released, evicted or cleared: it fades out without reporting itself finished.</summary>
        internal bool Released { get; private set; }

        /// <summary>The HUD endpoint it follows, or null for a line that stays where it was written.</summary>
        internal string EndpointKey { get; private set; }

        internal Vector2 Offset { get; private set; }

        /// <summary>The box the last written ink covers, in world space.</summary>
        internal Rect InkRect { get; private set; }

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            _baseFontSize = _label.fontSize;
            _writeOn.ExternallyTicked = true;
            ApplyHalo();
            // This line's own instance (the halo already made one): the fades write to it.
            _material = _label.fontMaterial;
            _faceColor = _material.GetColor(ShaderUtilities.ID_FaceColor);
            SetAlpha(0f);
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _writeOn.Stop();
            SetAlpha(0f);
            _state = LineState.Hidden;
            _pendingText = null;
            _text = null;
            Released = false;
            EndpointKey = null;
            Handle = HeadsUpHandle.None;
        }

        /// <summary>Writes <paramref name="text" /> in <paramref name="style" />, sized for <paramref name="arg" />.</summary>
        internal void Show(HeadsUpHandle handle, string text, Vector2 worldPosition, HeadsUpStyleSettings style,
            int arg, string endpointKey)
        {
            Handle = handle;
            Released = false;
            EndpointKey = endpointKey;
            Offset = style.WorldOffset;
            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            Write(text, style, arg);
        }

        /// <summary>
        ///     Writes <paramref name="text" /> over the line, where it is, at the next <see cref="Tick" />: several in
        ///     one frame cost one write. The same text again does nothing.
        /// </summary>
        internal void Rewrite(string text, HeadsUpStyleSettings style, int arg)
        {
            _pendingText = text;
            _pendingStyle = style;
            _pendingArg = arg;
        }

        /// <summary>Fades the line out on unscaled time, the pencil lifted mid-write if it was still writing.</summary>
        internal void Release()
        {
            Released = true;
            _pendingText = null;
            _writeOn.Stop();
            if (_state == LineState.Hidden)
            {
                return;
            }

            _state = LineState.Releasing;
            _fadeFrom = _alpha;
            _timer = 0f;
        }

        /// <summary>Moves the written line, ink and all, to <paramref name="worldPosition" />.</summary>
        internal void MoveTo(Vector2 worldPosition)
        {
            var shift = worldPosition - (Vector2)transform.position;
            transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
            InkRect = new Rect(InkRect.position + shift, InkRect.size);
        }

        /// <summary>Moves the line on; true once it has faded out and can go back to the pool.</summary>
        internal bool Tick(float liveDelta, float unscaledDelta)
        {
            ApplyPendingRewrite();
            switch (_state)
            {
                case LineState.Writing:
                    _writeOn.Tick(liveDelta);
                    if (!_writeOn.IsWriting)
                    {
                        _state = _held ? LineState.Held : LineState.Holding;
                        _timer = 0f;
                    }

                    return false;
                case LineState.Holding:
                    _timer += liveDelta;
                    if (_timer >= _holdSeconds)
                    {
                        _state = LineState.Fading;
                        _timer = 0f;
                    }

                    return false;
                case LineState.Fading:
                    return Fade(_visibleAlpha, _fadeOutDuration, liveDelta);
                case LineState.Releasing:
                    return Fade(_fadeFrom, _clearDuration, unscaledDelta);
                case LineState.Held:
                    return false;
                default:
                    return true;
            }
        }

        private void ApplyPendingRewrite()
        {
            if (_pendingText == null)
            {
                return;
            }

            var text = _pendingText;
            _pendingText = null;
            if (text == _text && _state != LineState.Fading)
            {
                return;
            }

            Write(text, _pendingStyle, _pendingArg);
        }

        private void Write(string text, HeadsUpStyleSettings style, int arg)
        {
            _text = text;
            _holdSeconds = style.HoldSeconds;
            _held = style.Held;
            _label.color = style.Look.Color;
            _label.fontSize = _baseFontSize * style.SizeAt(arg);
            _label.fontStyle = style.Look.FontStyle;
            SetAlpha(_visibleAlpha);
            _writeOn.Write(text);
            _state = LineState.Writing;

            // The write has built the mesh, so the bounds hold every character, faded-in or not.
            var bounds = _label.textBounds;
            var scale = (Vector2)transform.lossyScale;
            var position = (Vector2)transform.position;
            InkRect = new Rect(position + Vector2.Scale(bounds.min, scale), Vector2.Scale(bounds.size, scale));
        }

        private bool Fade(float from, float duration, float delta)
        {
            _timer += delta;
            var t = Mathf.Clamp01(_timer / duration);
            SetAlpha(from * (1f - t));
            if (t < 1f)
            {
                return false;
            }

            _state = LineState.Hidden;
            return true;
        }

        // Outline width and colour live on the font material: setting them gives this line its own instance,
        // a handful of draw calls for the few heads-up lines there are.
        private void ApplyHalo()
        {
            if (_haloWidth <= 0f)
            {
                return;
            }

            _label.outlineColor = _haloColor;
            _label.outlineWidth = _haloWidth;
        }

        // Through the material's face and outline colours, not the text's colour: setting that rebuilds the
        // mesh, which would undo the write-on's per-glyph reveal mid-stroke. The shader multiplies both by the
        // vertex colour the write-on drives.
        private void SetAlpha(float alpha)
        {
            _alpha = alpha;
            if (_material == null)
            {
                return;
            }

            var face = _faceColor;
            face.a *= alpha;
            _material.SetColor(ShaderUtilities.ID_FaceColor, face);
            var outline = _haloWidth > 0f ? _haloColor : Color.clear;
            outline.a *= alpha;
            _material.SetColor(ShaderUtilities.ID_OutlineColor, outline);
        }
    }
}
