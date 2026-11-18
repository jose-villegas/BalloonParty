using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.SceneLight;
using UniRx;
using UnityEngine;
using VContainer;
using Light = BalloonParty.Shared.SceneLight.Light;

namespace BalloonParty.Balloon.Type
{
    /// <summary>
    ///     Tints a reward balloon's god rays from its palette entry (the golden balloon's <c>GoldenRays</c>, the
    ///     heart's <c>HeartRays</c>). The rays turn and breathe in their shader (<c>BalloonParty/Sprite/GodRays</c>),
    ///     whose material holds their shape, and no tween outlives the balloon: they hang off the balloon's root,
    ///     so they scale in with its spawn and go with it when the pool takes it back. The rays also light the
    ///     scene: a light in their palette colour, reaching nearly as far as they do, follows the balloon through
    ///     the scene-light field while it is on the board.
    /// </summary>
    internal class GodRaysLook : MonoBehaviour, IBalloonViewBinding
    {
        // A resting balloon still drifts by hairs; moves under this don't re-render the light field.
        private const float MoveStep = 0.02f;
        private const float MinLightRadius = 0.05f;

        private static readonly int RayReachId = Shader.PropertyToID("_RayReach");

        [Tooltip("The god-ray quad. Sorted below every balloon and kept out of BalloonView's slot sorting, so " +
                 "the rays show between the balloons around it and never over any of them.")]
        [SerializeField] private SpriteRenderer _rays;

        [Tooltip("The palette entry tinting the rays; its alpha is their strength.")]
        [PaletteColorName] [SerializeField] private string _colorId = GamePalette.GoldenRaysColorId;

        [Tooltip("How strongly the rays light the scene round the balloon, times the palette entry's alpha (the " +
                 "rays' own strength). 0 casts no light.")]
        [Min(0f)]
        [SerializeField] private float _lightIntensity = 1.5f;

        [Tooltip("The light's reach as a share of the rays' own (the material's Reach at the quad's scale).")]
        [Range(0.1f, 1.5f)]
        [SerializeField] private float _lightReachShare = 0.9f;

        [Tooltip("How fast the light falls off toward its reach: 1 is a straight ramp, higher keeps it closer in.")]
        [Min(0.1f)]
        [SerializeField] private float _lightFalloffPower = 1.5f;

        [Inject] private IGamePalette _palette;
        [Inject] private SceneLightFieldService _lightField;

        private Light _light;
        private float _rayReach;

        // After the balloon has moved this frame, so the light keeps to it as it glides and grows in with its spawn.
        private void LateUpdate()
        {
            if (_light == null)
            {
                return;
            }

            var raysTransform = _rays.transform;
            var position = raysTransform.position;
            if ((position - _light.Position.Value).sqrMagnitude > MoveStep * MoveStep)
            {
                _light.Position.Value = position;
                _light.EndPosition.Value = position;
            }

            var radius = Mathf.Max(MinLightRadius, _rayReach * raysTransform.lossyScale.x * _lightReachShare);
            if (Mathf.Abs(radius - _light.Radius.Value) > MoveStep)
            {
                _light.Radius.Value = radius;
                _light.EndRadius.Value = radius;
            }
        }

        public void Bind(IBalloonModel model, CompositeDisposable disposables)
        {
            if (_rays == null || _palette == null)
            {
                return;
            }

            if (_lightField != null && _lightIntensity > 0f)
            {
                _rayReach = _rays.sharedMaterial != null && _rays.sharedMaterial.HasProperty(RayReachId)
                    ? _rays.sharedMaterial.GetFloat(RayReachId)
                    : 0f;
                _light = new Light(_rays.transform.position, MinLightRadius, 0f, -1, _lightFalloffPower);
                _lightField.RegisterLight(_light).AddTo(disposables);
                disposables.Add(Disposable.Create(() => _light = null));
            }

            SetColor(_colorId);
        }

        /// <summary>Turns the rays, and the light they cast, to another palette entry (the nuke's, once armed).</summary>
        internal void SetColor(string colorId)
        {
            if (_rays == null || _palette == null)
            {
                return;
            }

            var color = _palette.GetColor(colorId);
            _rays.color = color;
            if (_light == null)
            {
                return;
            }

            // The light field tags a light with one of its palette slots; an entry past them lights in the key
            // light's colour.
            var index = _palette.PaletteIndexOf(colorId);
            _light.PaletteIndex.Value = index < PaletteChannelEncoding.Slots ? index : -1;
            _light.Intensity.Value = _lightIntensity * color.a;
        }
    }
}
