using BalloonParty.Configuration.Palette;
using BalloonParty.Nudge;
using BalloonParty.Scenario;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Grid;
using MessagePipe;
using VContainer;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>Shared collaborators bundled so the per-balloon constructor avoids a dozen wiring arguments.</summary>
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
        public PaintingFieldService PaintingField { get; }
        public IGamePalette Palette { get; }

        [Inject]
        public BalloonControllerContext(
            ISubscriber<ItemActivatedMessage> itemActivatedSubscriber,
            BalloonControllerRegistry registry,
            IPublisher<TransformCapturedMessage> transformCapturedPublisher,
            IPublisher<BalloonDeflectedMessage> deflectedPublisher,
            IPublisher<NudgeMessage> nudgePublisher,
            SlotGrid grid,
            PoolManager poolManager,
            DisturbanceFieldService disturbanceField,
            PaintingFieldService paintingField,
            IGamePalette palette)
        {
            ItemActivatedSubscriber = itemActivatedSubscriber;
            Registry = registry;
            TransformCapturedPublisher = transformCapturedPublisher;
            DeflectedPublisher = deflectedPublisher;
            NudgePublisher = nudgePublisher;
            Grid = grid;
            PoolManager = poolManager;
            DisturbanceField = disturbanceField;
            PaintingField = paintingField;
            Palette = palette;
        }
    }
}
