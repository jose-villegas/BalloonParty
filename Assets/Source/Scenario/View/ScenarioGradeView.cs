using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Level;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer;

namespace BalloonParty.Scenario.View
{
    /// <summary>
    ///     Grades the whole frame per scenario (<see cref="IScenarioGradeSettings" />), so the sky, the meadow and
    ///     the water read in the same light. One global Volume holds a runtime profile whose values blend from the
    ///     sky's grade to the surface's by how much of the screen the surface covers (<see cref="BackdropMix" />),
    ///     so the grade follows the mist reveal frame by frame. The grade needs the main camera's post-processing,
    ///     which this turns on while the grade is enabled and gives back otherwise. The HUD canvases render
    ///     through that camera, so they take the grade too.
    /// </summary>
    internal sealed class ScenarioGradeView : MonoBehaviour
    {
        // Above any volume an artist might add to the scene: this one is the scenario's grade.
        private const float VolumePriority = 100f;

        private IScenarioGradeSettings _settings;
        private BackdropMix _mix;
        private VolumeProfile _profile;
        private Volume _volume;
        private ColorAdjustments _adjustments;
        private WhiteBalance _whiteBalance;
        private ShadowsMidtonesHighlights _tones;
        private UniversalAdditionalCameraData _cameraData;
        private bool _cameraHadPostProcessing;
        private bool _applied;

        private void Start()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _adjustments = _profile.Add<ColorAdjustments>(overrides: true);
            _whiteBalance = _profile.Add<WhiteBalance>(overrides: true);
            _tones = _profile.Add<ShadowsMidtonesHighlights>();
            _tones.shadows.overrideState = true;
            _tones.highlights.overrideState = true;

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = VolumePriority;
            _volume.sharedProfile = _profile;
        }

        // Late, after GroundView (early order) has written this frame's mix; the volume stack updates at render.
        private void LateUpdate()
        {
            var on = IsOn();
            Apply(on);
            if (!on)
            {
                return;
            }

            var grade = ScenarioGrade.Lerp(
                _settings.For(ScenarioKind.Sky), _settings.For(_mix.Surface), _mix.Coverage);
            _adjustments.postExposure.value = grade.Exposure;
            _adjustments.contrast.value = grade.Contrast;
            _adjustments.saturation.value = grade.Saturation;
            _adjustments.colorFilter.value = grade.Filter;
            _whiteBalance.temperature.value = grade.Temperature;
            _whiteBalance.tint.value = grade.Tint;
            _tones.shadows.value = Trackball(grade.Shadows, grade.ShadowLift);
            _tones.highlights.value = Trackball(grade.Highlights, 0f);
        }

        private void OnDestroy()
        {
            Apply(false);
            if (_profile != null)
            {
                Destroy(_profile);
            }
        }

        [Inject]
        internal void Construct(IScenarioGradeSettings settings, BackdropMix mix)
        {
            _settings = settings;
            _mix = mix;
        }

        // URP's trackball value: the tint in xyz, the brightness offset in w.
        private static Vector4 Trackball(Color color, float offset)
        {
            return new Vector4(color.r, color.g, color.b, offset);
        }

        private bool IsOn()
        {
#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE
            if (BalloonParty.Cheats.CheatState.ScenarioGradeOff)
            {
                return false;
            }
#endif
            return _settings.Enabled;
        }

        // Turns the camera's post-processing on with the grade, and back to what it was without it. The main
        // camera can come up after this view, so it is looked up until found.
        private void Apply(bool on)
        {
            if (_volume != null)
            {
                _volume.enabled = on;
            }

            if (on == _applied)
            {
                return;
            }

            if (_cameraData == null)
            {
                var camera = Camera.main;
                if (camera == null)
                {
                    return;
                }

                _cameraData = camera.GetUniversalAdditionalCameraData();
                _cameraHadPostProcessing = _cameraData.renderPostProcessing;
            }

            _cameraData.renderPostProcessing = on || _cameraHadPostProcessing;
            _applied = on;
        }
    }
}
