using BalloonParty.Nudge;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Grid;
using MessagePipe;
using VContainer;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     The run-lifetime collaborators every <see cref="BalloonController" /> shares (message buses,
    ///     registry, grid, pool, disturbance field). Bundled into one injected object so the per-balloon
    ///     constructor takes only its instance state instead of a dozen wiring arguments.
    /// </summary>
    internal class BalloonControllerContext
    {
        public ISubscriber<ItemActivatedMessage> ItemActivatedSubscriber { get; }
        public BalloonControllerRegistry Registry { get; }
        public IPublisher<TransformCapturedMessage> TransformCapturedPublisher { get; }
        public IPublisher<BalloonDeflectedMessage> DeflectedPublisher { get; }
        public IPublisher<NudgeMessage> NudgePublisher { get; }
        public SlotGrid Grid { get; }
        public PoolManager PoolManager { get; }
        public DisturbanceFieldService DisturbanceField { get; }

        [Inject]
        public BalloonControllerContext(
            ISubscriber<ItemActivatedMessage> itemActivatedSubscriber,
            BalloonControllerRegistry registry,
            IPublisher<TransformCapturedMessage> transformCapturedPublisher,
            IPublisher<BalloonDeflectedMessage> deflectedPublisher,
            IPublisher<NudgeMessage> nudgePublisher,
            SlotGrid grid,
            PoolManager poolManager,
            DisturbanceFieldService disturbanceField)
        {
            ItemActivatedSubscriber = itemActivatedSubscriber;
            Registry = registry;
            TransformCapturedPublisher = transformCapturedPublisher;
            DeflectedPublisher = deflectedPublisher;
            NudgePublisher = nudgePublisher;
            Grid = grid;
            PoolManager = poolManager;
            DisturbanceField = disturbanceField;
        }
    }
}
