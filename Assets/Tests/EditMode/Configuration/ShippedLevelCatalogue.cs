using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Ranges;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;

namespace BalloonParty.Tests.Configuration
{
    /// <summary>
    ///     A realistic generation catalogue that roughly mirrors the shipped asset's tuning. Tests that need a
    ///     full catalogue use it as a fixture; the real asset is pinned by <c>ShippedEarlyLevelsTests</c>.
    /// </summary>
    internal static class ShippedLevelCatalogue
    {
        internal static LevelGenerationSettings Build(ExtraColorLevelSettings extraColor = null)
        {
            var toughCount = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(2f, 0f), new Keyframe(5f, 1f));

            return new LevelGenerationSettings(
                threatBudget: new BudgetFormula(2f, 0.75f, 30f),
                aidBudget: new BudgetFormula(3f, 0.25f, 5f),
                colors: new[] { new IntLevelStep(1, 1), new IntLevelStep(2, 2), new IntLevelStep(5, 3) },
                boardLines: new[] { new IntLevelStep(1, 8), new IntLevelStep(2, 5), new IntLevelStep(5, 4) },
                itemCounts: new[]
                {
                    new ItemCountStep(1, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(3f, 1f)),
                        new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0.45f))),
                    new ItemCountStep(5, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f)),
                        new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0.45f))),
                },
                balloons: new[]
                {
                    new BalloonContentEntry(BalloonType.Simple, ContentRole.Threat, 1, 0, ContentWeightRamp.Flat(1f, 1f)),
                    new BalloonContentEntry(BalloonType.Tough, ContentRole.Threat, 1, 1, ContentWeightRamp.Flat(0.7f, 2f), toughCount),
                    new BalloonContentEntry(BalloonType.Rainbow, ContentRole.Aid, 2, 0, ContentWeightRamp.Flat(0.15f, 0.35f)),
                    new BalloonContentEntry(BalloonType.SimpleSilver, ContentRole.Aid, 4, 1, new ContentWeightRamp(0.1f, 1f, 0.1f, 1f, 0, 1f)),
                    new BalloonContentEntry(BalloonType.Unbreakable, ContentRole.Threat, 5, 2, ContentWeightRamp.Flat(1f, 2f)),
                    new BalloonContentEntry(BalloonType.Tougher, ContentRole.Threat, 6, 2, ContentWeightRamp.Flat(1f, 1f)),
                    new BalloonContentEntry(BalloonType.SimpleGold, ContentRole.Aid, 7, 1, new ContentWeightRamp(0.05f, 0.4f, 0.05f, 0.4f, 0, 0.7f)),
                    new BalloonContentEntry(BalloonType.BubbleCluster, ContentRole.Threat, 16, 2, ContentWeightRamp.Flat(0.1f, 0.1f)),
                },
                items: new[]
                {
                    new ItemContentEntry(ItemType.Shield, 1, 0, ContentWeightRamp.Flat(2f, 3f), 8, 12, 2),
                    new ItemContentEntry(ItemType.Paint, 2, 1, new ContentWeightRamp(0.1f, 0.6f, 0.1f, 0.6f, 0, 2f), 2, 1),
                    new ItemContentEntry(ItemType.Bomb, 3, 1, new ContentWeightRamp(0.4f, 1f, 0.4f, 1f, 0, 2f), 2, 1),
                    new ItemContentEntry(ItemType.Lightning, 6, 1, ContentWeightRamp.Flat(0.1f, 0.7f), 2),
                    new ItemContentEntry(ItemType.Laser, 7, 1, ContentWeightRamp.Flat(0.8f, 1.25f), 1, 1),
                    new ItemContentEntry(ItemType.Snipe, 8, 1, ContentWeightRamp.Flat(0.75f, 2.25f), 2, 1),
                },
                gridActors: new[]
                {
                    new GridActorContentEntry(GridActorType.Puff, 3, 1, new RangedInt(7, 9), new RangedInt(10, 13), 6, 5),
                    new GridActorContentEntry(GridActorType.Bush, 6, 2, new RangedInt(3, 9), new RangedInt(3, 9), 0, 3,
                        requiresGround: true),
                },
                extraColor: extraColor);
        }
    }
}
