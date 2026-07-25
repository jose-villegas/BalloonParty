using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Slots.Capabilities;

namespace BalloonParty.Item.Effects
{
    /// <summary>Bomb-core scalars — mirrors <see cref="BombSettings" />'s kill-radius fields only (the
    /// nudge/light/implosion fields are live-presentation, never read by a config-free core).</summary>
    internal readonly struct BombEffectParams
    {
        public readonly float Radius;
        public readonly float RainbowEffectScale;
        public readonly float RainbowConversionRange;

        public BombEffectParams(float radius, float rainbowEffectScale, float rainbowConversionRange)
        {
            Radius = radius;
            RainbowEffectScale = rainbowEffectScale;
            RainbowConversionRange = rainbowConversionRange;
        }
    }

    /// <summary>Laser-core scalars — mirrors <see cref="LaserSettings" />'s geometry fields only.</summary>
    internal readonly struct LaserEffectParams
    {
        public readonly float RaycastDistance;
        public readonly float CircleCastRadius;
        public readonly float ColorCycles;

        public LaserEffectParams(float raycastDistance, float circleCastRadius, float colorCycles)
        {
            RaycastDistance = raycastDistance;
            CircleCastRadius = circleCastRadius;
            ColorCycles = colorCycles;
        }
    }

    /// <summary>Lightning-core scalars — mirrors <see cref="LightningSettings" />'s selection fields
    /// only (glow/visual fields excluded).</summary>
    internal readonly struct LightningEffectParams
    {
        public readonly float SegmentsMultiplier;
        public readonly float Randomness;
        public readonly float JumpTime;

        public LightningEffectParams(float segmentsMultiplier, float randomness, float jumpTime)
        {
            SegmentsMultiplier = segmentsMultiplier;
            Randomness = randomness;
            JumpTime = jumpTime;
        }
    }

    /// <summary>Paint-core scalars — mirrors <see cref="PaintSettings" />'s spread-triangle fields
    /// (<c>PaintTriangle.Build</c>'s own inputs).</summary>
    internal readonly struct PaintEffectParams
    {
        public readonly float SpreadOffset;
        public readonly float SpreadLength;
        public readonly float SpreadBaseWidth;
        public readonly float SpreadBlobRadius;

        public PaintEffectParams(float spreadOffset, float spreadLength, float spreadBaseWidth, float spreadBlobRadius)
        {
            SpreadOffset = spreadOffset;
            SpreadLength = spreadLength;
            SpreadBaseWidth = spreadBaseWidth;
            SpreadBlobRadius = spreadBlobRadius;
        }
    }

    /// <summary>Snipe scalars — no core (Shield/Snipe are pure state grants), so these ride here
    /// unused until Phase C6 reads them.</summary>
    internal readonly struct SnipeEffectParams
    {
        public readonly float SpeedBuffMultiplier;
        public readonly int ChargePerToughHit;
        public readonly float BloomBaseRadius;
        public readonly float BloomRadiusPerCharge;
        public readonly float BloomRadiusCap;

        public SnipeEffectParams(
            float speedBuffMultiplier, int chargePerToughHit, float bloomBaseRadius, float bloomRadiusPerCharge,
            float bloomRadiusCap)
        {
            SpeedBuffMultiplier = speedBuffMultiplier;
            ChargePerToughHit = chargePerToughHit;
            BloomBaseRadius = bloomBaseRadius;
            BloomRadiusPerCharge = bloomRadiusPerCharge;
            BloomRadiusCap = bloomRadiusCap;
        }
    }

    /// <summary>One item type's config-free effect snapshot — <see cref="ItemSettings" /> and
    /// <see cref="PaintSettings" /> etc. are plain <c>[Serializable]</c> classes with no public
    /// constructor a test can build, so every core (<c>BombBlast</c>/<c>LaserCross</c>/
    /// <c>LightningChain</c>/<c>PaintSpread</c>) reads this immutable value snapshot instead.
    /// <see cref="Damage" />/<see cref="Flags" /> are per item type (Bomb/Laser/Lightning deal 3,
    /// everything else 1 — see <see cref="ItemSettings.Damage" />); the other five fields only matter
    /// for the item type they name.</summary>
    internal readonly struct ItemEffectParams
    {
        public readonly BombEffectParams Bomb;
        public readonly LaserEffectParams Laser;
        public readonly LightningEffectParams Lightning;
        public readonly PaintEffectParams Paint;
        public readonly SnipeEffectParams Snipe;
        public readonly int Damage;
        public readonly DamageFlags Flags;

        public ItemEffectParams(
            BombEffectParams bomb, LaserEffectParams laser, LightningEffectParams lightning, PaintEffectParams paint,
            SnipeEffectParams snipe, int damage, DamageFlags flags)
        {
            Bomb = bomb;
            Laser = laser;
            Lightning = lightning;
            Paint = paint;
            Snipe = snipe;
            Damage = damage;
            Flags = flags;
        }

        /// <summary>Snapshots every configured item type once — the sim's own scratch-buffer
        /// convention (build once per gather/test, reuse across a whole sweep).</summary>
        public static IReadOnlyDictionary<ItemType, ItemEffectParams> FromConfiguration(IItemConfiguration configuration)
        {
            var result = new Dictionary<ItemType, ItemEffectParams>();
            foreach (var settings in configuration.Items)
            {
                result[settings.Type] = new ItemEffectParams(
                    new BombEffectParams(settings.Bomb.Radius, settings.Bomb.RainbowEffectScale, settings.Bomb.RainbowConversionRange),
                    new LaserEffectParams(settings.Laser.RaycastDistance, settings.Laser.CircleCastRadius, settings.Laser.ColorCycles),
                    new LightningEffectParams(settings.Lightning.SegmentsMultiplier, settings.Lightning.Randomness, settings.Lightning.JumpTime),
                    new PaintEffectParams(
                        settings.Paint.SpreadOffset, settings.Paint.SpreadLength, settings.Paint.SpreadBaseWidth,
                        settings.Paint.SpreadBlobRadius),
                    new SnipeEffectParams(
                        settings.Snipe.SpeedBuffMultiplier, settings.Snipe.ChargePerToughHit, settings.Snipe.BloomBaseRadius,
                        settings.Snipe.BloomRadiusPerCharge, settings.Snipe.BloomRadiusCap),
                    settings.Damage, settings.Flags);
            }

            return result;
        }
    }
}
