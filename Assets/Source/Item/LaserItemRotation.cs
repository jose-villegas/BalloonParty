using System;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.SceneLight;
using UnityEngine;
using Light = BalloonParty.Shared.SceneLight.Light;
using Random = UnityEngine.Random;

namespace BalloonParty.Item
{
    public class LaserItemRotation : MonoBehaviour, ITransformCapture, ISpinningItemVisual
    {
        // Keeps the transition confined to the tail of each step, so the icon reads as genuinely
        // still for the rest of it, without needing a second authored knob alongside _stepSeconds.
        private const float TransitionFraction = 0.25f;

        [SerializeField] [Min(0.01f)] private float _stepSeconds = 1.5f;

        // The item icon is pooled through a non-injecting channel, so the host (ItemDisplayService)
        // hands these in via Configure rather than DI.
        private SceneLightFieldService _lightField;
        private IGamePalette _palette;
        private LaserSettings _laserSettings;

        // Hex-aligned angles to dwell on, cycled in order. Seeded with the default HexCoordinates
        // diagonal (~60 degrees) so a laser shown before Configure() runs still steps sensibly.
        private float[] _angles = { 0f, 60f, 120f, 180f, 240f, 300f };
        private int _currentIndex;
        private int _previousIndex;
        private float _elapsed;
        private bool _stopped;
        private Light _horizontal;
        private Light _vertical;
        private IDisposable _horizontalRegistration;
        private IDisposable _verticalRegistration;

        // Non-destructive spin read for the shot solver's gather (@ref plan_shot_solver_accuracy
        // Phase C §4) — CaptureSnapshot stops the spin, so gather must use this instead.
        float ISpinningItemVisual.AngleDegrees => transform.eulerAngles.z;

        // A stepped rotation has no meaningful constant rate — the icon sits still for most of each
        // step and is only briefly in motion. Reporting 0 makes the solver use AngleDegrees as-read
        // instead of extrapolating along a rate that no longer exists, which is correct for the
        // overwhelming majority of the time (the dwell).
        float ISpinningItemVisual.SpinDegreesPerSecond => 0f;

        // Reports the exact same condition DrawnAngle branches on, via IsDwelling — the two cannot
        // disagree because there is only one comparison, not a second one written to match it.
        bool ISpinningItemVisual.IsSettled => IsDwelling(_elapsed, _stepSeconds);

        // The exact span IsSettled/IsDwelling reads true for within one step — same DwellDuration
        // both of those already key off, so this cannot drift out of step with either.
        float ISpinningItemVisual.DwellSeconds => DwellDuration(_stepSeconds);

        private void OnEnable()
        {
            // Random start step (not a random angle) so multiple lasers on the board don't march in
            // lockstep, while the drawn angle always stays one of the hex-aligned values.
            _currentIndex = Random.Range(0, _angles.Length);
            _previousIndex = _currentIndex;
            _elapsed = 0f;
            _stopped = false;
            transform.localRotation = Quaternion.AngleAxis(_angles[_currentIndex], Vector3.forward);
        }

        private void Update()
        {
            if (_stopped)
            {
                return;
            }

            AdvanceStep();
            transform.localRotation = Quaternion.AngleAxis(DrawnAngle(), Vector3.forward);
            SyncTelegraphToSettings();
            UpdateTelegraph();
        }

        private void OnDisable()
        {
            DisposeTelegraph();
        }

        public TransformSnapshot CaptureSnapshot()
        {
            _stopped = true;
            // The shot is firing — its own beam lights take over (LaserItemHandler), so drop the preview.
            DisposeTelegraph();
            return new TransformSnapshot(transform);
        }

        // Called by the host each time the icon is shown (the pool channel doesn't DI-inject). Derives
        // the hex-aligned angle set from the live slot separation, then registers the telegraph now
        // that the service is available — OnEnable ran too early to have it.
        internal void Configure(SceneLightFieldService lightField, IGamePalette palette, LaserSettings settings, Vector2 slotSeparation)
        {
            _lightField = lightField;
            _palette = palette;
            _laserSettings = settings;

            // LaserCross fires a four-arm cross along ±right/±up, so its look repeats every 90 degrees:
            // 180 reads identical to 0, 240 to 60, 300 to 120 — only three of the six hex bearings are
            // visually distinct. But we still step through all six: Mathf.LerpAngle always takes the
            // shortest path, and with only three entries the wrap from the last back to the first would
            // run backwards (~-120 degrees), snapping the cross rather than continuing its turn. All six
            // hex bearings in order make every transition, including the wrap, a forward ~+60 degrees —
            // the repeated look is harmless since the motion is what sells the rotation.
            var diagonal = Mathf.Atan2(slotSeparation.y, slotSeparation.x) * Mathf.Rad2Deg;
            _angles = new[] { 0f, diagonal, 180f - diagonal, 180f, 180f + diagonal, 360f - diagonal };

            DisposeTelegraph();
            RegisterTelegraph();
        }

        // Registers or tears down the telegraph lights when the SO toggle changes at runtime,
        // and pushes live intensity/size edits into existing lights each frame.
        private void SyncTelegraphToSettings()
        {
            if (_laserSettings == null)
            {
                return;
            }

            var wantEnabled = _laserSettings.TelegraphEnabled;
            var isRegistered = _horizontal != null;

            if (wantEnabled && !isRegistered)
            {
                RegisterTelegraph();
            }
            else if (!wantEnabled && isRegistered)
            {
                DisposeTelegraph();
            }
            else if (isRegistered)
            {
                // Push live edits so the artist sees changes without re-entering play mode.
                _horizontal.Intensity.Value = _laserSettings.TelegraphIntensity;
                _horizontal.Radius.Value = _laserSettings.TelegraphHalfWidth;
                _vertical.Intensity.Value = _laserSettings.TelegraphIntensity;
                _vertical.Radius.Value = _laserSettings.TelegraphHalfWidth;
            }
        }

        private void RegisterTelegraph()
        {
            if (_laserSettings == null || !_laserSettings.TelegraphEnabled || _lightField == null)
            {
                return;
            }

            // Idle telegraph reads as the LaserAim colour; the fired beam takes the owner's colour.
            var colorIndex = _palette.PaletteIndexOf(GamePalette.UnbreakableColorId);
            var center = transform.position;
            _horizontal = Light.Segment(center, center, _laserSettings.TelegraphHalfWidth, _laserSettings.TelegraphIntensity, colorIndex);
            _vertical = Light.Segment(center, center, _laserSettings.TelegraphHalfWidth, _laserSettings.TelegraphIntensity, colorIndex);
            _horizontalRegistration = _lightField.RegisterLight(_horizontal);
            _verticalRegistration = _lightField.RegisterLight(_vertical);
            UpdateTelegraph();
        }

        // The cross follows the item's world position and (spinning) rotation each frame.
        private void UpdateTelegraph()
        {
            if (_horizontal == null || _laserSettings == null)
            {
                return;
            }

            var center = transform.position;
            var halfLen = _laserSettings.TelegraphHalfLength;
            var right = (Vector3)(Vector2)transform.right * halfLen;
            var up = (Vector3)(Vector2)transform.up * halfLen;

            _horizontal.Position.Value = center - right;
            _horizontal.EndPosition.Value = center + right;
            _vertical.Position.Value = center - up;
            _vertical.EndPosition.Value = center + up;
        }

        private void DisposeTelegraph()
        {
            LifecycleHelper.DisposeAndClear(ref _horizontalRegistration);
            LifecycleHelper.DisposeAndClear(ref _verticalRegistration);
            _horizontal = null;
            _vertical = null;
        }

        // Advances to the next step whenever elapsed time crosses _stepSeconds, wrapping the
        // remainder (not zeroing it) so a long frame doesn't drop time off the following step.
        private void AdvanceStep()
        {
            _elapsed += Time.deltaTime;
            while (_elapsed >= _stepSeconds)
            {
                _elapsed -= _stepSeconds;
                _previousIndex = _currentIndex;
                _currentIndex = (_currentIndex + 1) % _angles.Length;
            }
        }

        // Dwells on the previous angle for the first part of the step, then eases into the new
        // target across the last TransitionFraction of it.
        private float DrawnAngle()
        {
            if (IsDwelling(_elapsed, _stepSeconds))
            {
                return _angles[_previousIndex];
            }

            var dwellDuration = DwellDuration(_stepSeconds);
            var transitionDuration = _stepSeconds * TransitionFraction;
            var t = (_elapsed - dwellDuration) / transitionDuration;
            var smoothT = Mathf.SmoothStep(0f, 1f, t);
            return Mathf.LerpAngle(_angles[_previousIndex], _angles[_currentIndex], smoothT);
        }

        // Where the dwell ends and the transition tail begins — the one definition DrawnAngle,
        // IsDwelling and DwellSeconds all key off, so none of the three can drift out of step with the
        // others. Internal rather than private so it is edit-mode testable directly, mirroring IsDwelling.
        internal static float DwellDuration(float stepSeconds)
        {
            return stepSeconds * (1f - TransitionFraction);
        }

        // Pure and static — no MonoBehaviour/Time dependency — so ISpinningItemVisual.IsSettled and
        // DrawnAngle's own branch share this single comparison instead of each carrying a copy that could
        // disagree; internal rather than private so it is edit-mode testable directly.
        internal static bool IsDwelling(float elapsedSeconds, float stepSeconds)
        {
            return elapsedSeconds <= DwellDuration(stepSeconds);
        }
    }
}
