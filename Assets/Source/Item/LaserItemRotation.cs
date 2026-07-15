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
    public class LaserItemRotation : MonoBehaviour, ITransformCapture
    {
        [SerializeField] private float _rotationSpeed;

        // The item icon is pooled through a non-injecting channel, so the host (ItemDisplayService)
        // hands these in via ConfigureLightField rather than DI.
        private SceneLightFieldService _lightField;
        private IGamePalette _palette;
        private LaserSettings _laserSettings;
        private float _angle;
        private bool _stopped;
        private Light _horizontal;
        private Light _vertical;
        private IDisposable _horizontalRegistration;
        private IDisposable _verticalRegistration;

        private void OnEnable()
        {
            _angle = Random.Range(0f, 360f);
            _stopped = false;
            transform.localRotation = Quaternion.AngleAxis(_angle, Vector3.forward);
        }

        private void Update()
        {
            if (_stopped)
            {
                return;
            }

            _angle += _rotationSpeed * Time.deltaTime;
            transform.localRotation = Quaternion.AngleAxis(_angle, Vector3.forward);
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

        // Called by the host each time the icon is shown (the pool channel doesn't DI-inject). Registers
        // the telegraph now that the service is available — OnEnable ran too early to have it.
        internal void ConfigureLightField(SceneLightFieldService lightField, IGamePalette palette, LaserSettings settings)
        {
            _lightField = lightField;
            _palette = palette;
            _laserSettings = settings;
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
            var colorIndex = _palette.PaletteIndexOf(GamePalette.LaserAimColorId);
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
    }
}
