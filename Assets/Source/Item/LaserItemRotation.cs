using UnityEngine;

namespace BalloonParty.Item
{
    public class LaserItemRotation : MonoBehaviour, ITransformCapture
    {
        [SerializeField] private float _rotationSpeed;

        private float _angle;
        private bool _stopped;

        private void Update()
        {
            if (_stopped)
            {
                return;
            }

            _angle += _rotationSpeed * Time.deltaTime;
            transform.localRotation = Quaternion.AngleAxis(_angle, Vector3.forward);
        }

        private void OnEnable()
        {
            _angle = Random.Range(0f, 360f);
            _stopped = false;
            transform.localRotation = Quaternion.AngleAxis(_angle, Vector3.forward);
        }

        public TransformSnapshot CaptureSnapshot()
        {
            _stopped = true;
            return new TransformSnapshot(transform);
        }
    }
}
