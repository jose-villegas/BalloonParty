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
    ///     so the grade follows the mist reveal frame by frame. It sits on top of the base look in the default
    ///     volume profile (Neutral tonemapping and the Color Adjustments that keep the frame as bright as it was in
    ///     LDR), which the HDR pipeline applies everywhere, the launcher included. The HUD canvases render through the main
    ///     camera, so they take the grade too.
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
        private ColorAdjustments _baseAdjustments;
        private ColorAdjustments _neutralAdjustments;

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
            _volume.enabled = IsOn();
            if (!_volume.enabled)
            {
                return;
            }

            var grade = ScenarioGrade.Lerp(
                _settings.For(ScenarioKind.Sky), _settings.For(_mix.Surface), _mix.Coverage);
            var baseLook = BaseAdjustments();
            _adjustments.postExposure.value = baseLook.postExposure.value + grade.Exposure;
            _adjustments.contrast.value = baseLook.contrast.value + grade.Contrast;
            _adjustments.saturation.value = baseLook.saturation.value + grade.Saturation;
            _adjustments.colorFilter.value = baseLook.colorFilter.value * grade.Filter;
            _whiteBalance.temperature.value = grade.Temperature;
            _whiteBalance.tint.value = grade.Tint;
            _tones.shadows.value = Trackball(grade.Shadows, grade.ShadowLift);
            _tones.highlights.value = Trackball(grade.Highlights, 0f);
        }

        private void OnDestroy()
        {
            if (_profile != null)
            {
                Destroy(_profile);
            }

            if (_neutralAdjustments != null)
            {
                Destroy(_neutralAdjustments);
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

        // This volume overrides Color Adjustments outright, so the grade's exposure, contrast, saturation and
        // filter are applied on top of the base's to stay relative to it. The values are read every frame, so the
        // base can be tuned live; a missing base reads as neutral.
        private ColorAdjustments BaseAdjustments()
        {
            if (_baseAdjustments != null)
            {
                return _baseAdjustments;
            }

            var profile = VolumeManager.instance.globalDefaultProfile;
            if (profile != null && profile.TryGet(out _baseAdjustments))
            {
                return _baseAdjustments;
            }

            if (_neutralAdjustments == null)
            {
                _neutralAdjustments = ScriptableObject.CreateInstance<ColorAdjustments>();
            }

            return _neutralAdjustments;
        }
    }
}
