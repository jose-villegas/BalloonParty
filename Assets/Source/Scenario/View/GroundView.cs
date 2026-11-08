using BalloonParty.Configuration;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Level;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Rendering;
using BalloonParty.Slots.Actor;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Scenario.View
{
    /// <summary>
    ///     The ground backdrop shown on ground levels: one stationary, overscanned quad between the sky scatter
    ///     and the cloud backdrop, drawn with <c>BalloonParty/Scenario/Ground</c> (a meadow whose blades part
    ///     around anything stamping the shared disturbance field). Follows <see cref="IActiveScenario" />: on
    ///     ground it shows the quad, hides the sky it fully covers, and raises the global
    ///     <c>_GroundCoverage</c> the cloud backdrop reads to fade its bodies (their shadows stay on the grass).
    ///     The Launch begin-screen always shows sky. The meadow scrolls with <see cref="ScenarioContentRoot" />, so
    ///     on the Ascent the bushes ride in planted on their ground rather than sliding over a still one.
    /// </summary>
    internal sealed class GroundView : MonoBehaviour
    {
        // Default, not Scenario: SceneCaptureService captures the Scenario layer for the GI, and an opaque
        // full-screen ground there would read as coverage everywhere.
        private const int GroundLayer = 0;

        private static readonly int GroundCoverageId = Shader.PropertyToID("_GroundCoverage");
        private static readonly int GroundScrollId = Shader.PropertyToID("_GroundScroll");

        private readonly CompositeDisposable _subscriptions = new();

        private IGroundSettings _settings;
        private IGameDisplayConfiguration _display;
        private IActiveScenario _scenario;
        private ISkyBackdrop _sky;
        private ScenarioContentRoot _scenarioRoot;
        private GameObject _quad;
        private Vector2 _pushedScroll = new(float.NaN, float.NaN);

        private void Start()
        {
            if (_settings.Material == null)
            {
                Log.Warn("Ground", "disabled: assign a material on the GroundSettings asset.", this);
                enabled = false;
                return;
            }

            _quad = QuadRendererBuilder.Build(
                "Ground", _settings.Material, GroundLayer, _settings.SortingLayerName, _settings.SortingOrder);
            _quad.transform.SetParent(transform, worldPositionStays: false);

            var overscan = Mathf.Max(1f, _settings.OverscanMultiplier);
            _quad.transform.localScale = new Vector3(
                _display.ReferenceWorldWidth * overscan,
                _display.ReferenceWorldHeight * overscan,
                1f);

            // The Launch begin-screen is always sky: its camera mask leaves out the Default layer this quad is on,
            // so showing ground there would hide the sky behind nothing. A ground start level lands on entering Game.
            _scenario.Kind
                .CombineLatest(Navigation.Current, ShowsGround)
                .DistinctUntilChanged()
                .Subscribe(Show)
                .AddTo(_subscriptions);
        }

        // Late, so the Ascent tween has already moved the root this frame and the meadow keeps pace with the bushes.
        private void LateUpdate()
        {
            if (_quad == null || !_quad.activeSelf)
            {
                return;
            }

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
            Destroy(_quad.GetComponent<MeshFilter>().sharedMesh);
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

        private void Show(bool ground)
        {
            _quad.SetActive(ground);
            _sky?.SetVisible(!ground);
            Shader.SetGlobalFloat(GroundCoverageId, ground ? 1f : 0f);
        }
    }
}
