using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Palette;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Type
{
    /// <summary>
    ///     Turns an unbreakable's red lights blue while it holds a formation: the core glow sprites, the inner
    ///     rim, and the exhaust trail. A glitter swirl fades in behind it at the same time. The change starts
    ///     once the member has arrived in its slot, and eases back to red when the formation breaks.
    /// </summary>
    internal class UnbreakableFormationLook : MonoBehaviour, IBalloonViewBinding
    {
        // How far toward white the blue trail's tail gets, matching the red trail's pale tip.
        private const float TrailTailWhiten = 0.6f;

        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int HdrBoostId = Shader.PropertyToID("_HdrBoost");

        [Tooltip("The core glow sprites, tinted red at rest.")]
        [SerializeField] private SpriteRenderer[] _coreLights;

        [Tooltip("Renderers whose material rim is red at rest (UnbreakableMatIN); overridden per instance.")]
        [SerializeField] private SpriteRenderer[] _rimRenderers;

        [SerializeField] private TrailRenderer _trail;

        [Tooltip("The glitter swirl behind the sphere. Hidden, and costing no draw call, out of formation.")]
        [SerializeField] private SpriteRenderer _glitter;

        [Tooltip("Seconds to ease between red and blue.")]
        [SerializeField] [Min(0.01f)] private float _fadeDuration = 0.4f;

        [Tooltip("The glitter's alpha at full formation.")]
        [SerializeField] [Range(0f, 1f)] private float _glitterAlpha = 1f;

        // Blue carries far less light than the red at the same boost, so the core lights' _HdrBoost rises with
        // the blue to glow as brightly, while staying blue rather than whitening (PLAN-HDR).
        [Tooltip("The core lights' material _HdrBoost at full formation; at rest they keep their material's.")]
        [SerializeField] [Min(1f)] private float _coreFormationBoost = 4f;

        [Inject] private IGamePalette _palette;

        private MaterialPropertyBlock _block;
        private Color[] _coreRest;
        private float[] _coreRestBoost;
        private Color[] _rimRest;
        private Gradient _trailRest;
        private Gradient _trailBlue;
        private Color _blue;
        private IBalloonModel _model;
        private float _blend;
        private bool _trailIsBlue;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            CaptureRestLook();
        }

        private void Update()
        {
            if (_model == null)
            {
                return;
            }

            var target = ShowsFormation(_model) ? 1f : 0f;
            if (Mathf.Approximately(_blend, target))
            {
                // BalloonView.OnSpawned re-enables every sorted renderer, the glitter included.
                if (_blend <= 0f && _glitter != null && _glitter.enabled)
                {
                    _glitter.enabled = false;
                }

                return;
            }

            // Only a fade toward formation turns the glitter on: a fade back to red must not re-show it on a
            // balloon BalloonView.Hide has just hidden for its pop.
            if (target > _blend && _glitter != null && !_glitter.enabled)
            {
                _glitter.enabled = true;
            }

            _blend = Mathf.MoveTowards(_blend, target, Time.deltaTime / _fadeDuration);
            Apply(_blend);
        }

        // The glitter hangs off the Shell, which sways and turns with its balloon. Pinning its world rotation
        // here, after those have run, points the swirl's drift (the sprite's local up) along the formation
        // instead, the same way for every member.
        private void LateUpdate()
        {
            if (_glitter == null || !_glitter.enabled || _model is not IFormationMember member)
            {
                return;
            }

            var heading = member.FormationHeading;
            var angle = Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg - 90f;
            _glitter.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        // Binding a pooled instance to a new balloon starts it red, whatever its last balloon ended on.
        public void Bind(IBalloonModel model, CompositeDisposable disposables)
        {
            _model = model;
            _blue = _palette != null ? _palette.GetColor(GamePalette.UnbreakableFormationColorId) : Color.blue;
            _trailBlue ??= BuildBlueTrail();
            _blend = 0f;
            Apply(0f);
        }

        /// <summary>Blue only once the member has arrived: while it still glides into its slot it stays red.</summary>
        internal static bool ShowsFormation(IBalloonModel model)
        {
            return model is IFormationMember { InFormation: { Value: true } } && model.IsStable.Value;
        }

        private void CaptureRestLook()
        {
            _coreRest = new Color[_coreLights?.Length ?? 0];
            _coreRestBoost = new float[_coreRest.Length];
            for (var i = 0; i < _coreRest.Length; i++)
            {
                _coreRest[i] = _coreLights[i] != null ? _coreLights[i].color : Color.clear;
                var material = _coreLights[i] != null ? _coreLights[i].sharedMaterial : null;
                _coreRestBoost[i] = material != null && material.HasProperty(HdrBoostId) ? material.GetFloat(HdrBoostId) : 1f;
            }

            _rimRest = new Color[_rimRenderers?.Length ?? 0];
            for (var i = 0; i < _rimRest.Length; i++)
            {
                var material = _rimRenderers[i] != null ? _rimRenderers[i].sharedMaterial : null;
                _rimRest[i] = material != null && material.HasProperty(RimColorId)
                    ? material.GetColor(RimColorId)
                    : Color.clear;
            }

            _trailRest = _trail != null ? _trail.colorGradient : null;
        }

        // The rest gradient's shape and alpha, recoloured from the formation blue at the head toward white
        // at the tail, the way the red one runs from crimson to a pale tip.
        private Gradient BuildBlueTrail()
        {
            if (_trailRest == null)
            {
                return null;
            }

            var colorKeys = _trailRest.colorKeys;
            for (var i = 0; i < colorKeys.Length; i++)
            {
                colorKeys[i].color = Color.Lerp(_blue, Color.white, colorKeys[i].time * TrailTailWhiten);
            }

            var gradient = new Gradient { mode = _trailRest.mode };
            gradient.SetKeys(colorKeys, _trailRest.alphaKeys);
            return gradient;
        }

        private void Apply(float blend)
        {
            for (var i = 0; i < _coreRest.Length; i++)
            {
                var core = _coreLights[i];
                if (core != null)
                {
                    var rest = _coreRest[i];
                    core.color = Color.Lerp(rest, new Color(_blue.r, _blue.g, _blue.b, rest.a), blend);

                    // Same block the rim loop below reads back, so the two overrides sit side by side.
                    core.GetPropertyBlock(_block);
                    _block.SetFloat(HdrBoostId, Mathf.Lerp(_coreRestBoost[i], _coreFormationBoost, blend));
                    core.SetPropertyBlock(_block);
                }
            }

            for (var i = 0; i < _rimRest.Length; i++)
            {
                var r = _rimRenderers[i];
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(_block);
                _block.SetColor(RimColorId, Color.Lerp(_rimRest[i], _blue, blend));
                r.SetPropertyBlock(_block);
            }

            // A gradient can't be lerped without allocating, so the trail swaps at the fade's midpoint.
            var trailBlue = blend >= 0.5f;
            if (_trail != null && _trailBlue != null && trailBlue != _trailIsBlue)
            {
                _trail.colorGradient = trailBlue ? _trailBlue : _trailRest;
                _trailIsBlue = trailBlue;
            }

            if (_glitter != null)
            {
                if (blend <= 0f)
                {
                    _glitter.enabled = false;
                }

                _glitter.color = new Color(_blue.r, _blue.g, _blue.b, blend * _glitterAlpha);
            }
        }
    }
}
