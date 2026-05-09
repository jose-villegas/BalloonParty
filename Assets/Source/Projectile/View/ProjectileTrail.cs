using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BalloonParty.Projectile.View
{
    [RequireComponent(typeof(TrailRenderer))]
    public class ProjectileTrail : MonoBehaviour
    {
        private TrailRenderer _trail;

        private void Awake()
        {
            _trail = GetComponent<TrailRenderer>();
            Disable();
        }

        public void Enable()
        {
            EnableNextFrameAsync().Forget();
        }

        public void Disable()
        {
            _trail.emitting = false;
            _trail.Clear();
        }

        private async UniTaskVoid EnableNextFrameAsync()
        {
            await UniTask.Yield(destroyCancellationToken);
            _trail.Clear();
            _trail.emitting = true;
        }
    }
}