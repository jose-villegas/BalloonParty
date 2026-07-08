using BalloonParty.Item.Laser;
using UnityEngine;

namespace BalloonParty.Editor.EffectPreview
{
    /// <summary>
    ///     Preview module for <see cref="LaserView" />. Recolours the view's wired renderers with the
    ///     picked colour and samples the effect's clip frame-by-frame in edit mode (Animator.Update is
    ///     unreliable outside play mode, so we drive AnimationClip.SampleAnimation directly).
    /// </summary>
    internal sealed class LaserPreviewModule : IEffectPreviewModule
    {
        private readonly LaserView _view;

        private AnimationClip _clip;
        private Color _tint;
        private float _length;
        private float _elapsed;
        private bool _finished;

        public bool UsesColorPicker => true;

        internal LaserPreviewModule(LaserView view)
        {
            _view = view;
        }

        public void DrawGUI()
        {
        }

        public void Start(EffectPreviewContext context)
        {
            _tint = context.Tint;
            _clip = ResolveClip();
            _length = _clip != null ? _clip.length : 0f;
            _elapsed = 0f;
            _finished = _clip == null || _length <= 0f;

            SampleAndTint(0f);
        }

        public bool Tick(float delta)
        {
            if (_finished)
            {
                return false;
            }

            _elapsed += delta;
            SampleAndTint(Mathf.Min(_elapsed, _length));

            if (_elapsed >= _length)
            {
                _finished = true;
                return false;
            }

            return true;
        }

        public void CleanUp()
        {
        }

        public void DrawSceneGizmos()
        {
        }

        private AnimationClip ResolveClip()
        {
            var animator = _view.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return null;
            }

            var clips = animator.runtimeAnimatorController.animationClips;
            return clips.Length > 0 ? clips[0] : null;
        }

        // The clip may re-assert renderer state each sample, so the tint is re-applied after sampling.
        private void SampleAndTint(float time)
        {
            if (_clip != null)
            {
                _clip.SampleAnimation(_view.gameObject, time);
            }

            _view.ApplyColor(_tint);
        }
    }
}
