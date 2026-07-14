using System;
using BalloonParty.Shared.SceneLight;
using UnityEngine;
using VContainer;
using Light = BalloonParty.Shared.SceneLight.Light;
using Random = UnityEngine.Random;

namespace BalloonParty.Item
{
    public class LaserItemRotation : MonoBehaviour, ITransformCapture
    {
        [SerializeField] private float _rotationSpeed;

        [Header("Telegraph Light (experimental)")]
        [Tooltip("While the laser sits idle on a balloon, cast a spinning cross of light matching the " +
                 "item's rotation — a preview of where it'll fire. Toggle to experiment.")]
        [SerializeField] private bool _castTelegraphLight;
        [SerializeField] [Min(0f)] private float _lightHalfLength = 2f;
        [SerializeField] [Min(0f)] private float _lightHalfWidth = 0.5f;
        [SerializeField] [Min(0f)] private float _lightIntensity = 1.5f;

        [Inject] private SceneLightFieldService _lightField;

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
            RegisterTelegraph();
        }

        private void Update()
        {
            if (_stopped)
            {
                return;
            }

            _angle += _rotationSpeed * Time.deltaTime;
            transform.localRotation = Quaternion.AngleAxis(_angle, Vector3.forward);
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

        private void RegisterTelegraph()
        {
            if (!_castTelegraphLight || _lightField == null)
            {
                return;
            }

            var center = transform.position;
            _horizontal = Light.Segment(center, center, _lightHalfWidth, _lightIntensity);
            _vertical = Light.Segment(center, center, _lightHalfWidth, _lightIntensity);
            _horizontalRegistration = _lightField.RegisterLight(_horizontal);
            _verticalRegistration = _lightField.RegisterLight(_vertical);
            UpdateTelegraph();
        }

        // The cross follows the item's world position and (spinning) rotation each frame.
        private void UpdateTelegraph()
        {
            if (_horizontal == null)
            {
                return;
            }

            var center = transform.position;
            var right = (Vector3)(Vector2)transform.right * _lightHalfLength;
            var up = (Vector3)(Vector2)transform.up * _lightHalfLength;

            _horizontal.Position.Value = center - right;
            _horizontal.EndPosition.Value = center + right;
            _vertical.Position.Value = center - up;
            _vertical.EndPosition.Value = center + up;
        }

        private void DisposeTelegraph()
        {
            _horizontalRegistration?.Dispose();
            _verticalRegistration?.Dispose();
            _horizontalRegistration = null;
            _verticalRegistration = null;
            _horizontal = null;
            _vertical = null;
        }
    }
}
