#region

using UnityEngine;

#endregion

namespace BalloonParty.Item
{
    public class LaserItemRotation : MonoBehaviour
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
            _angle = 0f;
            _stopped = false;
            transform.localRotation = Quaternion.identity;
        }

        public void Stop()
        {
            _stopped = true;
        }
    }
}
