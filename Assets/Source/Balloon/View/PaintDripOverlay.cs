using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     Trigger-and-forget paint-drip overlay: a child sprite over the balloon body that plays a wavy,
    ///     wet dissolve of the incoming paint colour, then hides itself. Accept vs reject is not this
    ///     component's concern — the balloon body underneath has (accept) or hasn't (reject) already
    ///     committed the colour, so the same drip reads as paint settling or sliding off. Runs entirely
    ///     off a MaterialPropertyBlock, so many balloons dripping at once batch on one shared material.
    /// </summary>
    internal class PaintDripOverlay : MonoBehaviour
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int PaintColorId = Shader.PropertyToID("_PaintColor");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("Seconds for one drip: paint floods on, waves, then runs off to reveal the body.")]
        [SerializeField] private float _duration = 0.28f;

        [Tooltip("Maps normalized time to drip progress (0 = clean body, 1 = fully run off).")]
        [SerializeField] private AnimationCurve _progress = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private MaterialPropertyBlock _block;
        private float _elapsed;
        private bool _playing;

        private void Awake()
        {
            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }

        private void Update()
        {
            if (!_playing)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            var t = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;

            Apply(_progress.Evaluate(t));

            if (t >= 1f)
            {
                Stop();
            }
        }

        internal void Play(Color paintColor)
        {
            if (_renderer == null)
            {
                return;
            }

            _elapsed = 0f;
            _playing = true;
            _renderer.enabled = true;

            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(PaintColorId, paintColor);
            _block.SetFloat(SeedId, Random.Range(0f, 100f));
            _renderer.SetPropertyBlock(_block);

            Apply(0f);
        }

        // Called by BalloonView so the drip sorts above the body, the hosted item and the above-item
        // layer — the whole balloon's stack — without being wired into any renderer array.
        internal void ApplySortingOrder(int order)
        {
            if (_renderer != null)
            {
                _renderer.sortingOrder = order;
            }
        }

        internal void Stop()
        {
            _playing = false;
            _elapsed = 0f;

            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }

        private void Apply(float progress)
        {
            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(ProgressId, progress);
            _renderer.SetPropertyBlock(_block);
        }

#if UNITY_EDITOR
        internal float EditorDuration => _duration;
        internal AnimationCurve EditorProgressCurve => _progress;

        // Pushes an arbitrary drip frame in edit mode (no play loop) so PaintDripOverlayEditor can scrub
        // and play the animation while tuning the material — see PaintDripOverlayEditor.
        internal void EditorPreview(float progress, Color paintColor, float seed)
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.enabled = true;
            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(PaintColorId, paintColor);
            _block.SetFloat(SeedId, seed);
            _block.SetFloat(ProgressId, progress);
            _renderer.SetPropertyBlock(_block);
        }
#endif
    }
}
