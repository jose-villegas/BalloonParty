using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots;
using BalloonParty.UI.Score;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.UI.Shields
{
    public class ShieldTrailController : MonoBehaviour
    {
        private const string TrailPoolKey = "ShieldTrail";

        [SerializeField] private ScorePointTrail _trailPrefab;

        [Inject] private IGameConfiguration _config;
        [Inject] private PoolManager _poolManager;
        [Inject] private ISubscriber<ShieldGainedMessage> _shieldGainedSubscriber;
        [Inject] private SlotGrid _slotGrid;

        private readonly CompositeDisposable _disposable = new();

        private void OnDestroy()
        {
            _disposable.Dispose();
        }

        [Inject]
        private void Initialize()
        {
            _shieldGainedSubscriber.Subscribe(OnShieldGained).AddTo(_disposable);
        }

        private void OnShieldGained(ShieldGainedMessage msg)
        {
            var fromWorldPosition = _slotGrid.IndexToWorldPosition(msg.SlotIndex);
            SpawnTrail(fromWorldPosition);
        }

        private void SpawnTrail(Vector3 fromWorldPosition)
        {
            var trail = _poolManager.GetOrRegister(TrailPoolKey,
                () => new ShieldTrailPoolChannel(_trailPrefab));

            trail.transform.position = fromWorldPosition;
            trail.transform.localScale = Vector3.one;

            trail.Setup(transform.position,
                _config.ShieldTrailDuration,
                () => _poolManager.Return(TrailPoolKey, trail));
        }
    }
}
