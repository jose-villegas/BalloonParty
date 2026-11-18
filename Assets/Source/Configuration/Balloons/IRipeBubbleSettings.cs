using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     A bubble cluster that reaches <see cref="IBalloonsConfiguration.BubbleMergeMax" /> is ripe: any hit bursts it
    ///     whole, and a fuse on the level clock runs until it hands its bubbles out and leaves.
    /// </summary>
    public interface IRipeBubbleSettings
    {
        /// <summary>Seconds of level clock from ripening to the fuse running out. 0 or less: clusters never ripen.</summary>
        float FuseSeconds { get; }

        /// <summary>The hit each neighbour takes from a ripe cluster's ripple.</summary>
        int RippleDamage { get; }

        /// <summary>Seconds of level clock from the hit that pops a ripe cluster to its ripple: the cluster folds into
        /// one big bubble meanwhile, and the ripple lands as that bubble pops. A ripe neighbour the ripple pops waits
        /// as long again.</summary>
        float BurstSeconds { get; }

        /// <summary>Seconds between the bubbles a cluster whose fuse ran out hands to its neighbours.</summary>
        float FoamStepSeconds { get; }

        /// <summary>A ripe cluster's size over its rest size.</summary>
        float SwellScale { get; }

        /// <summary>Seconds a cluster takes to swell as it ripens, and to settle back when it stops being ripe.</summary>
        float SwellSeconds { get; }

        /// <summary>How hard each throb of the fuse punches the cluster out, as a share of its size.</summary>
        float ThrobStrength { get; }

        /// <summary>Seconds a throb takes to settle.</summary>
        float ThrobSeconds { get; }

        /// <summary>The last seconds of the fuse, in which it throbs twice a second instead of once.</summary>
        float ThrobFastSeconds { get; }

        /// <summary>The colour a ripe cluster is tinted toward as its fuse runs out.</summary>
        Color WarnTint { get; }

        /// <summary>A ripe cluster's float speed over its rest speed.</summary>
        float FloatSpeedup { get; }

        /// <summary>A ripe cluster's spin over its rest spin as its fuse runs out: 1 at ripening, this at the end.</summary>
        float SpinSpeedup { get; }

        /// <summary>How far toward its warning tint a cluster is the moment it ripens (0..1).</summary>
        float WarnStart { get; }

        /// <summary>Seconds between the soap bubbles a ripe cluster lets off; 0 for none. They leave from a
        /// point that turns round the cluster, so together they draw a spiral arm.</summary>
        float SpiralInterval { get; }

        /// <summary>Seconds a spiral's bubble lives: with the turning, how many turns of the arm show.</summary>
        float SpiralSeconds { get; }

        /// <summary>How far a spiral's bubble gets from the cluster, in slot spacings.</summary>
        float SpiralReach { get; }

        /// <summary>Turns a second of the point the bubbles leave from.</summary>
        float SpiralTurnsPerSecond { get; }

        /// <summary>How much faster the spiral sheds bubbles in the fuse's last seconds.</summary>
        float SpiralFastSpeedup { get; }

        /// <summary>A spiral bubble's radius at the arm's outer end, in slot spacings; it leaves smaller.</summary>
        float SpiralBubbleRadius { get; }

        /// <summary>The most layers of foam a balloon wears; each takes an ordinary hit of its own.</summary>
        int CoatLayers { get; }

        /// <summary>The soap material a coat is drawn with: the cluster's, showing one bubble.</summary>
        Material CoatMaterial { get; }

        /// <summary>The quad the coat's material is drawn on: the sprite the cluster prefab's renderer uses.</summary>
        Sprite CoatSprite { get; }

        /// <summary>The coat's bubble radius over the balloon body's own: above 1 so the bubble encloses it.</summary>
        float CoatScale { get; }

        /// <summary>Seconds a coat takes to puff away when a hit takes it off.</summary>
        float CoatPuffSeconds { get; }

        /// <summary>The coat's size at the end of its puff, over its fitted size.</summary>
        float CoatPuffScale { get; }

        /// <summary>Seconds the ring a player's burst sends over the water takes to spread and fade.</summary>
        float RingSeconds { get; }

        /// <summary>How far the ring spreads, in slot spacings from the burst.</summary>
        float RingReach { get; }

        /// <summary>The sorting layer the ring draws on.</summary>
        string RingSortingLayerName { get; }

        /// <summary>The ring's order on its layer: below the balloons, so it reads as on the water.</summary>
        int RingSortingOrder { get; }
    }
}
