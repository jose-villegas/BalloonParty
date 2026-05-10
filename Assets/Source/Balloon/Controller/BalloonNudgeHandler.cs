#region

using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using MessagePipe;
using VContainer;
using VContainer.Unity;

#endregion

namespace BalloonParty.Balloon.Controller
{
    public class BalloonNudgeHandler : IStartable
    {
        private readonly SlotGrid _grid;
        private readonly ISubscriber<BalloonHitMessage> _hitSubscriber;
        private readonly IPublisher<BalloonNudgeMessage> _nudgePublisher;

        [Inject]
        public BalloonNudgeHandler(
            SlotGrid grid,
            ISubscriber<BalloonHitMessage> hitSubscriber,
            IPublisher<BalloonNudgeMessage> nudgePublisher)
        {
            _grid = grid;
            _hitSubscriber = hitSubscriber;
            _nudgePublisher = nudgePublisher;
        }

        public void Start()
        {
            _hitSubscriber.Subscribe(OnBalloonHit);
        }

        private void OnBalloonHit(BalloonHitMessage msg)
        {
            var hitSlot = msg.Balloon.SlotIndex.Value;
            var hitSlotPos = _grid.IndexToWorldPosition(hitSlot);
            var neighbors = _grid.GetNeighbors(hitSlot.x, hitSlot.y);

            foreach (var neighbor in neighbors)
            {
                _nudgePublisher.Publish(new BalloonNudgeMessage(neighbor, hitSlotPos));
            }
        }
    }
}


