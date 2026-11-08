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
    ///     stamping the shared disturbance field. Follows <see cref="IActiveScenario" />: on ground it shows both,
    ///     hides the sky they fully cover, and raises the global
    ///     <c>_GroundCoverage</c> the cloud backdrop reads to fade its bodies (their shadows stay on the grass).
    ///     The Launch begin-screen always shows sky. The meadow scrolls with <see cref="ScenarioContentRoot" />, so
    ///     on the Ascent the bushes ride in planted on their ground rather than sliding over a still one.
    /// </summary>
    internal sealed class GroundView : MonoBehaviour
    {
        private static readonly int GroundCoverageId = Shader.PropertyToID("_GroundCoverage");
        private static readonly int GroundScrollId = Shader.PropertyToID("_GroundScroll");
        private static readonly int BladeTileId = Shader.PropertyToID("_BladeTile");
        private static readonly int PatchSizeId = Shader.PropertyToID("_GroundPatchSize");
        private static readonly int DirtId = Shader.PropertyToID("_GroundDirt");

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
        private GameObject _blades;
        private Mesh _bladeMesh;
        private GrassWakeBuffer _wake;
        private Vector2 _pushedScroll = new(float.NaN, float.NaN);

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

            _quad = QuadRendererBuilder.Build(
                "Ground", _settings.Material, GroundLayer, _settings.SortingLayerName, _settings.SortingOrder);
            _quad.transform.SetParent(transform, worldPositionStays: false);

            var overscan = Mathf.Max(1f, _settings.OverscanMultiplier);
            _quad.transform.localScale = new Vector3(
                _display.ReferenceWorldWidth * overscan,
                _display.ReferenceWorldHeight * overscan,
                1f);

            // Floor and blades read the same patch noise, so the patch and dirt settings are globals: the drier tips sit
            // over the floor's lighter patches, and the blades thin out exactly where the floor shows dirt.
            Shader.SetGlobalFloat(PatchSizeId, Mathf.Max(0.01f, _settings.PatchSize));
            Shader.SetGlobalVector(DirtId, new Vector4(
                Mathf.Max(0.01f, _settings.DirtPatchSize), _settings.DirtThreshold, _settings.DirtSoftness, 0f));
            BuildBlades();

            // The Launch begin-screen is always sky; a ground start level lands on entering Game.
            _scenario.Kind
                .CombineLatest(Navigation.Current, ShowsGround)
                .DistinctUntilChanged()
                .Subscribe(Show)
                .AddTo(_subscriptions);
        }

        // Late, so the Ascent tween has already moved the root this frame and the meadow keeps pace with the bushes,
        // and the disturbance field has taken this frame's stamps before the wake reads them.
        private void LateUpdate()
        {
            if (_quad == null || !_quad.activeSelf)
            {
                return;
            }

            _wake?.Tick(Time.deltaTime, _settings.BladeRecoverySeconds);

            var scroll = (Vector2)_scenarioRoot.Transform.position;
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
            _wake?.Dispose();

            // Only the blade mesh is ours: the floor's quad is MeshHelper's shared cached quad, which the App-owned
            // sky and the GI overlay draw with too.
            if (_bladeMesh != null)
            {
                Destroy(_bladeMesh);
            }
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

        private static bool ShowsGround(ScenarioKind kind, NavigationState navigation)
        {
            return kind == ScenarioKind.Ground && navigation != NavigationState.Launch;
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

        private void Show(bool ground)
        {
            _quad.SetActive(ground);
            if (_blades != null)
            {
                _blades.SetActive(ground);
            }

            if (ground)
            {
                _wake?.Restart();
            }

            _sky?.SetVisible(!ground);
            Shader.SetGlobalFloat(GroundCoverageId, ground ? 1f : 0f);
        }
    }
}
