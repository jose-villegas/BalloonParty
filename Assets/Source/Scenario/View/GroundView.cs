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
    ///     on the Ascent the bushes ride in planted on their ground rather than sliding over a still one. A reveal
    ///     also plays the drop between the layers (<see cref="GroundRevealDepth" />): it scales the meadow, the
    ///     cloud backdrop and the content root about the camera, so going down the ground grows into place and
    ///     going up it falls away. On water levels the same floor quad draws the water surface
    ///     (<c>BalloonParty/Scenario/Water</c>) instead of the meadow, with no blades: every impact the game reports
    ///     moves a simulated surface (<see cref="WaterRippleBuffer" />), and its waves bend the caustic net
    ///     under it (<see cref="WaterCausticBuffer" />), and
    ///     the reveal, the depth and the coverage play the same way. Sky always sits between two surfaces in the
    ///     schedule, so a reveal never swaps the meadow for the water; a cut between them just swaps the floor's
    ///     material. Bonus levels play on paper (<c>BalloonParty/Scenario/Paper</c>), drawn on a quad of its own over
    ///     the floor. From the sky it comes and goes through the same mist reveal; from the meadow or the water,
    ///     which has no sky between, it opens straight over the surface instead (a paper swap): the floor stays at
    ///     rest, the paper quad shows inside the hole (or outside it, on the way out), and nothing else changes,
    ///     since both layers cover the screen.
    /// </summary>
    // Early, so its LateUpdate sets this frame's reveal depth on the content root before the bushes draw from it
    // in theirs.
    [DefaultExecutionOrder(-100)]
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
        private static readonly int MistOpacityId = Shader.PropertyToID("_MistOpacity");
        private static readonly int PaperRevealId = Shader.PropertyToID("_PaperReveal");
        private static readonly int PaperEdgeDeckleId = Shader.PropertyToID("_EdgeDeckle");

        private readonly CompositeDisposable _subscriptions = new();

        private IGroundSettings _settings;
        private IGameDisplayConfiguration _display;
        private IActiveScenario _scenario;
        private ISkyBackdrop _sky;
        private ScenarioContentRoot _scenarioRoot;
        private WaterImpactCollector _impacts;
        private BackdropMix _mix;
        // Scenario, like the sky it replaces, so SceneCaptureService captures the meadow for the GI. Its shaders
        // write colour only, never the capture's coverage alpha, so the full-screen ground doesn't read as one
        // big occluder. NameToLayer cannot be called from a static field initializer on a MonoBehaviour.
        private static int GroundLayer = -1;

        private GameObject _quad;
        private Renderer _quadRenderer;
        private Material _floorMaterial;
        private Material _waterMaterial;
        private WaterCausticBuffer _caustics;
        private WaterRippleBuffer _ripples;
        // The surface the floor quad draws: the meadow, or water. Stays on the last one while the sky shows, so
        // a reveal up to the sky closes over the surface that was there.
        private ScenarioKind _surface = ScenarioKind.Ground;
        // The surface layer the backdrop is on, or that a mist reveal opens or closes: the floor's surface, or the
        // paper. Stays on the last one while the sky shows, like _surface.
        private ScenarioKind _layer = ScenarioKind.Ground;
        private GameObject _paperQuad;
        private Material _paperMaterial;
        // This reveal opens the paper straight over the meadow or the water, or that surface through it.
        private bool _swap;
        // How far the paper's edge frays past the hole either way (half its material's fray).
        private float _paperFray;
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
        // The meadow's scale this frame: 1 at rest, below 1 while a reveal shows it deeper than the sky.
        private float _groundScale = 1f;
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
            _quadRenderer = _quad.GetComponent<Renderer>();

            var overscan = Mathf.Max(1f, _settings.OverscanMultiplier);
            _quad.transform.localScale = OverscanScale(overscan);
            _quad.SetActive(false);

            // Floor and blades read the same patch noise, so the patch and dirt settings are globals: the drier tips sit
            // over the floor's lighter patches, and the blades thin out exactly where the floor shows dirt.
            Shader.SetGlobalFloat(PatchSizeId, Mathf.Max(0.01f, _settings.PatchSize));
            Shader.SetGlobalVector(DirtId, new Vector4(
                Mathf.Max(0.01f, _settings.DirtPatchSize), _settings.DirtThreshold, _settings.DirtSoftness, 0f));
            BuildBlades();
            BuildWater();
            BuildPaper(overscan);
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

            var floorShown = _quad != null && _quad.activeSelf;
            if (floorShown && _surface == ScenarioKind.Water)
            {
                _caustics?.Tick();
                _ripples?.Tick(Time.deltaTime, _impacts.Impacts);
            }
            else if (floorShown)
            {
                _wake?.Tick(Time.deltaTime, _settings.BladeRecoverySeconds);
            }

            _impacts.Clear();
            if (!floorShown && (_paperQuad == null || !_paperQuad.activeSelf))
            {
                return;
            }

            // Heading for the sky, the meadow on screen is the outgoing one: it leaves with the old bushes. Into a
            // meadow, or within one, it is the incoming one and rides in with the new bushes. Scaled like them, about
            // the camera, so they stay planted on it while it grows or shrinks.
            var travel = _scenarioRoot.Travel;
            if (_revealing && _revealTarget == ScenarioKind.Sky)
            {
                travel -= _outgoingDrop;
            }

            var scroll = new Vector2(0f, travel * _groundScale);

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
            Shader.SetGlobalVector(PaperRevealId, Vector4.zero);
            _scenarioRoot.SetDepth(1f, 1f);
            _wake?.Dispose();
            _caustics?.Dispose();
            _ripples?.Dispose();

            // The blade mesh and the floor's material instances are ours: the floor's quad is MeshHelper's shared
            // cached quad, which the App-owned sky and the GI overlay draw with too.
            if (_bladeMesh != null)
            {
                Destroy(_bladeMesh);
            }

            Destroy(_floorMaterial);
            if (_waterMaterial != null)
            {
                Destroy(_waterMaterial);
            }

            if (_paperMaterial != null)
            {
                Destroy(_paperMaterial);
            }
        }

        [Inject]
        internal void Construct(
            IGroundSettings settings, IGameDisplayConfiguration display, IActiveScenario scenario,
            ScenarioContentRoot scenarioRoot, WaterImpactCollector impacts, BackdropMix mix,
            IObjectResolver resolver)
        {
            _settings = settings;
            _display = display;
            _scenario = scenario;
            _scenarioRoot = scenarioRoot;
            _impacts = impacts;
            _mix = mix;

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
            _mist.transform.localScale = OverscanScale(overscan);

            var reach = Mathf.Max(0.01f, _settings.MistReach);
            var block = new MaterialPropertyBlock();
            block.SetFloat(MistReachId, reach);
            block.SetFloat(MistOpacityId, Mathf.Clamp01(_settings.MistOpacity));
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

        // The water's own material instance (a reveal switches its blend like the meadow's) and the caustic net under
        // its surface. Without a water material, water levels show the sky (see OnScenario).
        private void BuildWater()
        {
            if (_settings.WaterMaterial == null)
            {
                Log.Warn("Ground", "no water: assign a water material on the GroundSettings asset.", this);
                return;
            }

            _waterMaterial = new Material(_settings.WaterMaterial);
            _caustics = new WaterCausticBuffer(_settings.WaterCausticMaterial, _settings.WaterCausticResolution);
            _ripples = new WaterRippleBuffer(_settings.WaterRippleMaterial, _settings.WaterRippleTexelsPerUnit);
        }

        // Paper draws on a quad of its own over the floor and the blades, so it can open straight over either surface.
        // Its own instance, like the floor's: a reveal switches its blend state. Without a paper material bonus levels
        // show the sky (see OnScenario).
        private void BuildPaper(float overscan)
        {
            if (_settings.PaperMaterial == null)
            {
                Log.Warn("Ground", "no paper: assign a paper material on the GroundSettings asset.", this);
                return;
            }

            _paperMaterial = new Material(_settings.PaperMaterial);
            _paperFray = _paperMaterial.HasFloat(PaperEdgeDeckleId)
                ? 0.5f * Mathf.Abs(_paperMaterial.GetFloat(PaperEdgeDeckleId))
                : 0f;
            _paperQuad = QuadRendererBuilder.Build(
                "Paper", _paperMaterial, GroundLayer, _settings.SortingLayerName, _settings.PaperSortingOrder);
            _paperQuad.transform.SetParent(transform, worldPositionStays: false);
            _paperQuad.transform.localScale = OverscanScale(overscan);
            _paperQuad.SetActive(false);
        }

        private void OnScenario(ScenarioShift shift, NavigationState navigation)
        {
            if ((shift.Kind == ScenarioKind.Water && _waterMaterial == null)
                || (shift.Kind == ScenarioKind.Paper && _paperMaterial == null))
            {
                shift = new ScenarioShift(ScenarioKind.Sky, shift.Seconds);
            }

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

            var from = _shown;
            _revealing = true;
            _revealTarget = target;
            _revealElapsed = 0f;
            _revealSeconds = seconds;
            _swap = GroundRevealPlanner.IsPaperSwap(from, target);

            if (_swap)
            {
                BeginPaperSwap(from, target);
                ApplyReveal();
                return;
            }

            // Down to a surface, that one opens; up to the sky, the surface showing closes.
            if (target != ScenarioKind.Sky)
            {
                ShowLayer(target);
            }

            SetLayerDrawn(true);
            RestartIncoming(target);

            _mist.SetActive(true);
            _sky?.SetVisible(true);
            SetBlend(PremultipliedDstBlend);
            Shader.SetGlobalVector(PaperRevealId, Vector4.zero);
            ApplyReveal();
        }

        // The floor shows the meadow or the water at rest under the paper, the surface leaving or the one arriving, and
        // only the paper blends. Both layers cover the screen, so the sky stays hidden and every coverage reader stays
        // at rest.
        private void BeginPaperSwap(ScenarioKind from, ScenarioKind target)
        {
            var paperIncoming = target == ScenarioKind.Paper;
            ShowFloor(paperIncoming ? from : target);
            _layer = ScenarioKind.Paper;
            Shader.SetGlobalColor(
                BounceReferenceId, paperIncoming ? _settings.PaperBounceReference : FloorBounceReference());

            _quad.SetActive(true);
            if (_blades != null)
            {
                _blades.SetActive(_surface == ScenarioKind.Ground);
            }

            _paperQuad.SetActive(true);
            RestartIncoming(target);
            SetBlend(OpaqueDstBlend);
            SetBlend(_paperMaterial, PremultipliedDstBlend);
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
            if (_swap)
            {
                ApplyPaperSwap(progress);
                return;
            }

            var opening = GroundRevealCurve.Opening(_revealTarget, progress);
            var frame = GroundRevealCurve.Evaluate(opening, _settings.MistInShare, _closedRadius, _openRadius);
            var depth = GroundRevealCurve.Depth(_revealTarget, opening, _settings.RevealDepthZoom);
            _groundScale = depth.Ground;
            _scenarioRoot.SetDepth(depth.Incoming, depth.Outgoing);
            _mix.Set(_layer, opening);

            // While the hole is closed the ground covers nothing: skip its full-screen blended pass and the blade
            // mesh's vertices rather than draw them at zero coverage.
            SetLayerDrawn(frame.Radius > _closedRadius);

            Shader.SetGlobalVector(GroundRevealId, new Vector4(
                1f, frame.Radius, Mathf.Max(0.01f, _settings.RevealEdgeSoftness), frame.MistDensity));
            Shader.SetGlobalVector(GroundRevealShapeId, new Vector4(
                Mathf.Max(0f, _settings.RevealEdgeWander), _revealElapsed, depth.Ground, depth.Sky));
        }

        // The hole grows the whole beat, with no mist: the paper inside it on the way in, outside it on the way out.
        // _GroundReveal's flag stays 0, so every other coverage reader stays at rest; only the paper reads the hole.
        private void ApplyPaperSwap(float progress)
        {
            var open = GroundRevealCurve.SwapOpening(progress);
            var paperIncoming = _revealTarget == ScenarioKind.Paper;
            _mix.Set(ScenarioKind.Paper, _surface, paperIncoming ? open : 1f - open);

            Shader.SetGlobalVector(GroundRevealId, new Vector4(
                0f, Mathf.Lerp(_closedRadius - _paperFray, _openRadius + _paperFray, open),
                Mathf.Max(0.01f, _settings.RevealEdgeSoftness), 0f));
            Shader.SetGlobalVector(GroundRevealShapeId, new Vector4(
                Mathf.Max(0f, _settings.RevealEdgeWander), _revealElapsed, 1f, 1f));
            Shader.SetGlobalVector(PaperRevealId, new Vector4(1f, paperIncoming ? 1f : 0f, 0f, 0f));
        }

        private void Cut(ScenarioKind kind)
        {
            _revealing = false;
            _swap = false;
            _shown = kind;
            var surface = kind != ScenarioKind.Sky;

            if (_mist != null)
            {
                _mist.SetActive(false);
            }

            Shader.SetGlobalVector(GroundRevealId, Vector4.zero);
            Shader.SetGlobalVector(PaperRevealId, Vector4.zero);
            _groundScale = 1f;
            _scenarioRoot.SetDepth(1f, 1f);
            SetBlend(OpaqueDstBlend);

            var grassWasShowing = _quad.activeSelf && _surface == ScenarioKind.Ground;
            var waterWasShowing = _quad.activeSelf && _surface == ScenarioKind.Water;
            if (surface)
            {
                ShowLayer(kind);
            }

            SetLayerDrawn(surface);
            if (kind == ScenarioKind.Ground && !grassWasShowing)
            {
                _wake?.Restart();
            }
            else if (kind == ScenarioKind.Water && !waterWasShowing)
            {
                _ripples?.Restart();
            }

            _sky?.SetVisible(!surface);
            Shader.SetGlobalFloat(GroundCoverageId, surface ? 1f : 0f);
            _mix.Set(_layer, surface ? 1f : 0f);
        }

        private void RestartIncoming(ScenarioKind target)
        {
            if (target == ScenarioKind.Ground)
            {
                _wake?.Restart();
            }
            else if (target == ScenarioKind.Water)
            {
                _ripples?.Restart();
            }
        }

        // Puts the backdrop's surface layer on the paper, or the floor quad on the meadow or the water, with the GI's
        // bounce reference to match.
        private void ShowLayer(ScenarioKind kind)
        {
            _layer = kind;
            if (kind == ScenarioKind.Paper)
            {
                Shader.SetGlobalColor(BounceReferenceId, _settings.PaperBounceReference);
                return;
            }

            ShowFloor(kind);
        }

        // Puts the floor quad on the meadow or the water, with the GI's bounce reference to match.
        private void ShowFloor(ScenarioKind kind)
        {
            var water = kind == ScenarioKind.Water;
            _surface = water ? ScenarioKind.Water : ScenarioKind.Ground;
            _quadRenderer.sharedMaterial = water ? _waterMaterial : _floorMaterial;
            Shader.SetGlobalColor(BounceReferenceId, FloorBounceReference());
            if (!water)
            {
                _caustics?.Rest();
                _ripples?.Restart();
            }
        }

        private Color FloorBounceReference()
        {
            return _surface == ScenarioKind.Water ? _settings.WaterBounceReference : _settings.BounceReference;
        }

        // The layer's quads: the paper alone, or the floor with the blades over it on the meadow.
        private void SetLayerDrawn(bool drawn)
        {
            var paper = _layer == ScenarioKind.Paper;
            _quad.SetActive(drawn && !paper);
            if (_blades != null)
            {
                _blades.SetActive(drawn && !paper && _surface == ScenarioKind.Ground);
            }

            if (_paperQuad != null)
            {
                _paperQuad.SetActive(drawn && paper);
            }
        }

        // Every surface, so a cut between them never draws one in the other's blend state.
        private void SetBlend(float dstBlend)
        {
            SetBlend(_floorMaterial, dstBlend);
            SetBlend(_waterMaterial, dstBlend);
            SetBlend(_paperMaterial, dstBlend);
        }

        // Every backdrop quad is overscanned the same way, so a camera shake or cinematic pan never reveals an edge.
        private Vector3 OverscanScale(float overscan)
        {
            return new Vector3(_display.ReferenceWorldWidth * overscan, _display.ReferenceWorldHeight * overscan, 1f);
        }

        private static void SetBlend(Material material, float dstBlend)
        {
            if (material == null)
            {
                return;
            }

            material.SetFloat(SrcBlendId, OpaqueSrcBlend);
            material.SetFloat(DstBlendId, dstBlend);
        }
    }
}
