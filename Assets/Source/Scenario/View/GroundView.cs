using BalloonParty.Configuration;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Level;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Rendering;
using BalloonParty.Slots.Actor;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Scenario.View
{
    /// <summary>
    ///     The ground backdrop shown on ground levels, between the sky scatter and the cloud backdrop: a stationary,
    ///     overscanned floor quad (<c>BalloonParty/Scenario/Ground</c>) and over it one mesh of grass blades
    ///     (<see cref="GrassBladeMesh" />, <c>BalloonParty/Scenario/GrassBlades</c>) that bend away from anything
    ///     stamping the shared disturbance field. Follows <see cref="IActiveScenario" />: at rest on ground it shows
    ///     both, hides the sky they fully cover, and raises the global <c>_GroundCoverage</c> the cloud backdrop reads
    ///     to fade its bodies (their shadows stay on the grass). A switch that isn't a cut plays the mist reveal
    ///     (<see cref="GroundRevealCurve" />, <c>BalloonParty/Scenario/GroundMist</c>): sky and ground both draw, the
    ///     ground inside a hole the mist opens or closes, and every <c>GroundCoverageAt</c> reader follows the hole.
    ///     The Launch begin-screen always shows sky. The meadow scrolls with <see cref="ScenarioContentRoot" />, so
    ///     on the Ascent the bushes ride in planted on their ground rather than sliding over a still one.
    /// </summary>
    internal sealed class GroundView : MonoBehaviour, ITransitionOutgoingContent
    {
        private const float OpaqueSrcBlend = 1f;
        private const float OpaqueDstBlend = 0f;
        private const float PremultipliedDstBlend = 10f;

        private static readonly int GroundCoverageId = Shader.PropertyToID("_GroundCoverage");
        private static readonly int GroundScrollId = Shader.PropertyToID("_GroundScroll");
        private static readonly int GroundRevealId = Shader.PropertyToID("_GroundReveal");
        private static readonly int GroundRevealShapeId = Shader.PropertyToID("_GroundRevealShape");
        private static readonly int BladeTileId = Shader.PropertyToID("_BladeTile");
        private static readonly int PatchSizeId = Shader.PropertyToID("_GroundPatchSize");
        private static readonly int DirtId = Shader.PropertyToID("_GroundDirt");
        private static readonly int BounceReferenceId = Shader.PropertyToID("_GroundBounceReference");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int MistReachId = Shader.PropertyToID("_MistReach");

        private readonly CompositeDisposable _subscriptions = new();

        private IGroundSettings _settings;
        private IGameDisplayConfiguration _display;
        private IActiveScenario _scenario;
        private ISkyBackdrop _sky;
        private ScenarioContentRoot _scenarioRoot;
        // Scenario, like the sky it replaces, so SceneCaptureService captures the meadow for the GI. Its shaders
        // write colour only, never the capture's coverage alpha, so the full-screen ground doesn't read as one
        // big occluder. NameToLayer cannot be called from a static field initializer on a MonoBehaviour.
        private static int GroundLayer = -1;

        private GameObject _quad;
        private Material _floorMaterial;
        private GameObject _blades;
        private Mesh _bladeMesh;
        private GrassWakeBuffer _wake;
        private GameObject _mist;
        private Vector2 _pushedScroll = new(float.NaN, float.NaN);
        private NavigationState? _navigation;
        private ScenarioKind _shown = ScenarioKind.Sky;
        private bool _revealing;
        private ScenarioKind _revealTarget;
        private float _revealElapsed;
        private float _revealSeconds;
        private float _closedRadius;
        private float _openRadius;
        // The transition's hold on the outgoing content (ITransitionOutgoingContent): the root lifts by this on
        // the beat's first frame while the old bushes keep their place, so an outgoing meadow subtracts it too.
        private float _outgoingDrop;

        private void Start()
        {
            if (_settings.Material == null)
            {
                Log.Warn("Ground", "disabled: assign a material on the GroundSettings asset.", this);
                enabled = false;
                return;
            }

            if (GroundLayer == -1)
            {
                GroundLayer = LayerMask.NameToLayer("Scenario");
            }

            // Our own instance: a reveal switches its blend state, which would otherwise dirty the material asset.
            _floorMaterial = new Material(_settings.Material);
            _quad = QuadRendererBuilder.Build(
                "Ground", _floorMaterial, GroundLayer, _settings.SortingLayerName, _settings.SortingOrder);
            _quad.transform.SetParent(transform, worldPositionStays: false);

            var overscan = Mathf.Max(1f, _settings.OverscanMultiplier);
            _quad.transform.localScale = new Vector3(
                _display.ReferenceWorldWidth * overscan,
                _display.ReferenceWorldHeight * overscan,
                1f);
            _quad.SetActive(false);

            // Floor and blades read the same patch noise, so the patch and dirt settings are globals: the drier tips sit
            // over the floor's lighter patches, and the blades thin out exactly where the floor shows dirt.
            Shader.SetGlobalFloat(PatchSizeId, Mathf.Max(0.01f, _settings.PatchSize));
            Shader.SetGlobalVector(DirtId, new Vector4(
                Mathf.Max(0.01f, _settings.DirtPatchSize), _settings.DirtThreshold, _settings.DirtSoftness, 0f));
            Shader.SetGlobalColor(BounceReferenceId, _settings.BounceReference);
            BuildBlades();
            BuildMist(overscan);

            _scenario.Current
                .CombineLatest(Navigation.Current, (shift, navigation) => (shift, navigation))
                .Subscribe(state => OnScenario(state.shift, state.navigation))
                .AddTo(_subscriptions);
        }

        // Late, so the Ascent tween has already moved the root this frame and the meadow keeps pace with the bushes,
        // and the disturbance field has taken this frame's stamps before the wake reads them.
        private void LateUpdate()
        {
            if (_revealing)
            {
                StepReveal();
            }

            if (_quad == null || !_quad.activeSelf)
            {
                return;
            }

            _wake?.Tick(Time.deltaTime, _settings.BladeRecoverySeconds);

            // Heading for the sky, the meadow on screen is the outgoing one: it leaves with the old bushes. Into a
            // meadow, or within one, it is the incoming one and rides in with the new bushes.
            var scroll = (Vector2)_scenarioRoot.Transform.position;
            if (_revealing && _revealTarget == ScenarioKind.Sky)
            {
                scroll.y -= _outgoingDrop;
            }

            if (scroll == _pushedScroll)
            {
                return;
            }

            _pushedScroll = scroll;
            Shader.SetGlobalVector(GroundScrollId, scroll);
        }

        private void OnDestroy()
        {
            _subscriptions.Dispose();

            if (_quad == null)
            {
                return;
            }

            // The sky and the clouds outlive this scope (App-owned), so hand them back their sky look.
            _sky?.SetVisible(true);
            Shader.SetGlobalFloat(GroundCoverageId, 0f);
            Shader.SetGlobalVector(GroundRevealId, Vector4.zero);
            _wake?.Dispose();

            // The blade mesh and the floor's material instance are ours: the floor's quad is MeshHelper's shared
            // cached quad, which the App-owned sky and the GI overlay draw with too.
            if (_bladeMesh != null)
            {
                Destroy(_bladeMesh);
            }

            Destroy(_floorMaterial);
        }

        [Inject]
        internal void Construct(
            IGroundSettings settings, IGameDisplayConfiguration display, IActiveScenario scenario,
            ScenarioContentRoot scenarioRoot, IObjectResolver resolver)
        {
            _settings = settings;
            _display = display;
            _scenario = scenario;
            _scenarioRoot = scenarioRoot;

            // AppLifetimeScope registers the sky only when its settings asset is wired; without it there is
            // nothing to hide, so this is an optional lookup rather than a constructor dependency.
            resolver.TryResolve(out _sky);
        }

        public void HoldOutgoing(Transform outgoingRoot, float exitDrop)
        {
            _outgoingDrop = exitDrop;
        }

        public void ReleaseOutgoing()
        {
            _outgoingDrop = 0f;
        }

        // One mesh of every blade over the camera's view plus a margin; the shader wraps the roots across the tile as
        // the meadow scrolls, so the mesh is built once and never touched again. Sized from the real view (the same
        // rect the disturbance field covers), not the reference size: a wide screen shows more than the reference,
        // and every blade off screen still runs the vertex stage. The shader
        // places blades in world space around the origin, which holds while this view sits at the origin (it is
        // parented under the Game scope, whose transform is identity).
        private void BuildBlades()
        {
            if (_settings.BladeMaterial == null)
            {
                Log.Warn("Ground", "no blades: assign a blade material on the GroundSettings asset.", this);
                return;
            }

            var view = new DisturbanceFieldCoordinates(_display, 1f).Bounds.size;
            var tile = view + Vector2.one * (2f * Mathf.Max(0f, _settings.BladeTileMargin));
            _bladeMesh = GrassBladeMesh.Build(
                new GrassBladeLayout(tile, _settings.BladeSpacing, _settings.BladeSeed));

            var renderer = MeshRendererBuilder.Build(
                "GrassBlades", _bladeMesh, _settings.BladeMaterial, GroundLayer, _settings.SortingLayerName,
                _settings.BladeSortingOrder);
            _blades = renderer.gameObject;
            _blades.transform.SetParent(transform, worldPositionStays: false);

            // Per renderer, not on the shared material, so play mode never dirties the material asset.
            var block = new MaterialPropertyBlock();
            block.SetVector(BladeTileId, new Vector4(-tile.x * 0.5f, -tile.y * 0.5f, tile.x, tile.y));
            renderer.SetPropertyBlock(block);
            _blades.SetActive(false);

            _wake = new GrassWakeBuffer(_settings.WakeMaterial);
        }

        // Default layer, which SceneCaptureService leaves out: the GI measures the sky and the ground under the mist,
        // not a white sheet that would read as a bright bounce. Drawn only while a reveal runs.
        private void BuildMist(float overscan)
        {
            if (_settings.MistMaterial == null)
            {
                Log.Warn("Ground", "no mist reveal: assign a mist material on the GroundSettings asset.", this);
                return;
            }

            _mist = QuadRendererBuilder.Build(
                "GroundMist", _settings.MistMaterial, 0, _settings.SortingLayerName, _settings.MistSortingOrder);
            _mist.transform.SetParent(transform, worldPositionStays: false);
            _mist.transform.localScale = new Vector3(
                _display.ReferenceWorldWidth * overscan,
                _display.ReferenceWorldHeight * overscan,
                1f);

            var reach = Mathf.Max(0.01f, _settings.MistReach);
            var block = new MaterialPropertyBlock();
            block.SetFloat(MistReachId, reach);
            _mist.GetComponent<Renderer>().SetPropertyBlock(block);
            _mist.SetActive(false);

            // Closed shows no ground even where the edge wanders inward. Open puts the edge, its soft band and the
            // mist reaching inside it (its full fray included, the shader's 1.5 x reach) past every corner of the
            // overscanned view, measured on the larger of the real view and the reference, so a wide screen or a
            // camera pan sees no mist left when the reveal cuts.
            var real = new DisturbanceFieldCoordinates(_display, 1f).Bounds.size;
            var view = new Vector2(
                Mathf.Max(real.x, _display.ReferenceWorldWidth), Mathf.Max(real.y, _display.ReferenceWorldHeight));
            var wander = Mathf.Max(0f, _settings.RevealEdgeWander);
            var softness = Mathf.Max(0.01f, _settings.RevealEdgeSoftness);
            _closedRadius = -(wander + softness);
            _openRadius = 0.5f * overscan * view.magnitude + wander + softness + reach * 1.5f;
        }

        private void OnScenario(ScenarioShift shift, NavigationState navigation)
        {
            var decision = GroundRevealPlanner.Decide(
                shift, navigation, _navigation, _revealing ? _revealTarget : _shown, _mist != null);
            _navigation = navigation;

            switch (decision.Action)
            {
                case GroundRevealAction.Cut:
                    Cut(decision.Target);
                    break;
                case GroundRevealAction.Reveal:
                    BeginReveal(decision.Target, decision.Seconds);
                    break;
            }
        }

        private void BeginReveal(ScenarioKind target, float seconds)
        {
            if (_revealing)
            {
                Cut(_revealTarget);
            }

            _revealing = true;
            _revealTarget = target;
            _revealElapsed = 0f;
            _revealSeconds = seconds;

            _quad.SetActive(true);
            if (_blades != null)
            {
                _blades.SetActive(true);
            }

            if (target == ScenarioKind.Ground)
            {
                _wake?.Restart();
            }

            _mist.SetActive(true);
            _sky?.SetVisible(true);
            _floorMaterial.SetFloat(SrcBlendId, OpaqueSrcBlend);
            _floorMaterial.SetFloat(DstBlendId, PremultipliedDstBlend);
            ApplyReveal();
        }

        // Unscaled, like the Ascent and the restart rise it rides on: the level-up's pop slow-mo doesn't stretch it.
        private void StepReveal()
        {
            _revealElapsed += Time.unscaledDeltaTime;
            if (_revealElapsed >= _revealSeconds)
            {
                Cut(_revealTarget);
                return;
            }

            ApplyReveal();
        }

        private void ApplyReveal()
        {
            var progress = _revealSeconds > 0f ? _revealElapsed / _revealSeconds : 1f;

            var frame = GroundRevealCurve.Evaluate(
                GroundRevealCurve.Opening(_revealTarget, progress), _settings.MistInShare, _closedRadius, _openRadius);

            // While the hole is closed the ground covers nothing: skip its full-screen blended pass and the blade
            // mesh's vertices rather than draw them at zero coverage.
            var open = frame.Radius > _closedRadius;
            _quad.SetActive(open);
            if (_blades != null)
            {
                _blades.SetActive(open);
            }

            Shader.SetGlobalVector(GroundRevealId, new Vector4(
                1f, frame.Radius, Mathf.Max(0.01f, _settings.RevealEdgeSoftness), frame.MistDensity));
            Shader.SetGlobalVector(GroundRevealShapeId, new Vector4(
                Mathf.Max(0f, _settings.RevealEdgeWander), _revealElapsed, 0f, 0f));
        }

        private void Cut(ScenarioKind kind)
        {
            _revealing = false;
            _shown = kind;
            var ground = kind == ScenarioKind.Ground;

            if (_mist != null)
            {
                _mist.SetActive(false);
            }

            Shader.SetGlobalVector(GroundRevealId, Vector4.zero);
            _floorMaterial.SetFloat(SrcBlendId, OpaqueSrcBlend);
            _floorMaterial.SetFloat(DstBlendId, OpaqueDstBlend);

            var wasShowing = _quad.activeSelf;
            _quad.SetActive(ground);
            if (_blades != null)
            {
                _blades.SetActive(ground);
            }

            if (ground && !wasShowing)
            {
                _wake?.Restart();
            }

            _sky?.SetVisible(!ground);
            Shader.SetGlobalFloat(GroundCoverageId, ground ? 1f : 0f);
        }
    }
}
