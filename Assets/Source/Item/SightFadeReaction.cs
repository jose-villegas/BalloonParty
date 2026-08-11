using UnityEngine;

namespace BalloonParty.Item
{
    /// <summary>
    ///     A <see cref="SightRampReaction"/> that fades a <see cref="SpriteRenderer"/> in and out with the
    ///     aim's centrality on this item — a placeholder telegraph for "the aim is passing through here",
    ///     tuned later in Unity once the final look is chosen. Deliberately targets a CHILD sprite, never
    ///     the reaction's own GameObject: deactivating that would disable this component along with it,
    ///     and it would never reactivate. <see cref="_maxAlpha"/>, not the sprite's own authored colour, is
    ///     the fade's ceiling — the rest-state sprite is naturally authored at alpha 0 (it starts hidden),
    ///     so caching THAT as a ceiling (as <see cref="BalloonParty.Prediction.TraceHitMarker" /> does with
    ///     its always-opaque marker) would make every target compute to zero and the sprite would never
    ///     show. The sprite's GameObject is only active while any of that alpha would show, so an off-aim
    ///     frame draws nothing.
    /// </summary>
    internal sealed class SightFadeReaction : SightRampReaction
    {
        [Tooltip("Sprite faded in/out. Must be a CHILD GameObject, never this component's own.")]
        [SerializeField] private SpriteRenderer _sprite;

        [Tooltip("Alpha at full centrality (dead-centre aim). The sprite's OWN authored alpha is ignored — author it at 0 (rest/hidden) and tune the peak here.")]
        [Range(0f, 1f)]
        [SerializeField] private float _maxAlpha = 1f;

        [Tooltip("Seconds to ease the alpha toward its target (SmoothDamp).")]
        [SerializeField] private float _smoothTime = 0.15f;

        [Tooltip("Alpha below which the sprite's GameObject is deactivated, so a fully-faded frame draws nothing.")]
        [SerializeField] private float _hideThreshold = 0.01f;

        private float _currentAlpha;
        private float _alphaVelocity;

        protected override void OnSightTick(float centrality)
        {
            if (_sprite == null)
            {
                return;
            }

            var target = _maxAlpha * centrality;
            _currentAlpha = Mathf.SmoothDamp(_currentAlpha, target, ref _alphaVelocity, _smoothTime);

            var visible = _currentAlpha > _hideThreshold;
            if (visible != _sprite.gameObject.activeSelf)
            {
                _sprite.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            var color = _sprite.color;
            color.a = _currentAlpha;
            _sprite.color = color;
        }

        protected override void ResetReaction()
        {
            _currentAlpha = 0f;
            _alphaVelocity = 0f;

            if (_sprite == null)
            {
                return;
            }

            var color = _sprite.color;
            color.a = 0f;
            _sprite.color = color;
            _sprite.gameObject.SetActive(false);
        }
    }
}
