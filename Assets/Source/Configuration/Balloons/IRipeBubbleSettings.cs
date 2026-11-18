using BalloonParty.Nudge;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     A bubble cluster that reaches <see cref="IBalloonsConfiguration.BubbleMergeMax" /> is ripe: any hit bursts it
    ///     whole, a small bomb whose blast (the ripple) hits every balloon within <see cref="RippleRadius" />, and a
    ///     fuse on the level clock runs until it hands its bubbles out and leaves.
    /// </summary>
    public interface IRipeBubbleSettings
    {
        /// <summary>Seconds of level clock from ripening to the fuse running out. 0 or less: clusters never ripen.</summary>
        float FuseSeconds { get; }

        /// <summary>The most clusters that are full (ripe, or about to ripen) at once, at least 1: while that many
        /// are, a merge or a foam's hand-out fills a cluster to one bubble short of the cap, where it waits.</summary>
        int MaxRipeClusters { get; }

        /// <summary>How many of the bubbles' off-beats (<see cref="MergeOffBeat" />) pass after a ripe cluster ends
        /// (burst by a hit, or its fuse run out) before another cluster is filled to the cap. 0: none.</summary>
        int RipenCooldownBeats { get; }

        /// <summary>When in each beat of the board (<see cref="IUnbreakableFormationSettings.BeatSeconds" />) bubble
        /// clusters merge, as a share of the beat after it falls (0 to 1): the bubbles' off-beat. Clusters merge
        /// only then, one pass an off-beat.</summary>
        float MergeOffBeat { get; }

        /// <summary>The hit each balloon the ripple reaches takes.</summary>
        int RippleDamage { get; }

        /// <summary>How far a ripe cluster's ripple reaches, in world units between slot centres, the edge
        /// included: every balloon whose slot centre is that close to the burst's is hit.</summary>
        float RippleRadius { get; }

        /// <summary>Seconds of level clock from the hit that pops a ripe cluster to its ripple: the cluster folds into
        /// one big bubble meanwhile, and the ripple lands as that bubble pops. A ripe cluster the ripple pops waits
        /// as long again.</summary>
        float BurstSeconds { get; }

        /// <summary>How much bigger than the whole cluster the one bubble a fold ends on is: each step of the fold
        /// adds an even share of it.</summary>
        float FoldGrowth { get; }

        /// <summary>How far each step of the fold kicks the cluster's scale out, as a share of its size, before it
        /// settles on the step's own size.</summary>
        float FoldPulse { get; }

        /// <summary>The outward shove the board takes as a ripe cluster bursts: a shockwave nudge, as a bomb's is,
        /// with its own distance, duration and falloff. Empty: the configuration's default nudge.</summary>
        NudgeOverride[] BlastNudgeOverrides { get; }

        /// <summary>The inward pull stamped on the disturbance field just before the burst's outward shock, as a
        /// multiple of the <c>BubbleRipple</c> stamp profile's radius.</summary>
        float BlastImplosionRadiusScale { get; }

        /// <summary>The inward pull's strength, as a multiple of the <c>BubbleRipple</c> profile's. 0: no pull.</summary>
        float BlastImplosionStrengthScale { get; }

        /// <summary>Seconds the inward pull takes: short, the outward shock follows it.</summary>
        float BlastImplosionSeconds { get; }

        /// <summary>The radius of the light a burst flashes, in world units.</summary>
        float BlastLightRadius { get; }

        /// <summary>The flash's peak intensity.</summary>
        float BlastLightIntensity { get; }

        /// <summary>Seconds the flash is held. 0 or less: no flash.</summary>
        float BlastLightSeconds { get; }

        /// <summary>How many soap bubbles a burst throws outward. 0: none.</summary>
        int BlastBubbles { get; }

        /// <summary>Seconds a thrown bubble takes to reach the edge of the blast and fade.</summary>
        float BlastBubbleSeconds { get; }

        /// <summary>A thrown bubble's radius at the edge of the blast, in slot spacings; it leaves smaller.</summary>
        float BlastBubbleRadius { get; }

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

        /// <summary>How far toward its warning tint a cluster is the moment it ripens (0..1).</summary>
        float WarnStart { get; }

        /// <summary>Seconds between the soap bubbles a ripe cluster lets off; 0 for none. They leave from a
        /// point that turns round the cluster, so together they draw a spiral arm.</summary>
        float SpiralInterval { get; }

        /// <summary>Seconds a spiral's bubble lives: with the turning, how many turns of the arm show.</summary>
        float SpiralSeconds { get; }

        /// <summary>How far a spiral's bubble gets from the cluster, in slot spacings.</summary>
        float SpiralReach { get; }

        /// <summary>Turns a second of the point the bubbles leave from, and of the ripe cluster itself: the two turn
        /// together, a little faster in the fuse's last seconds.</summary>
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

        /// <summary>Seconds the ring a player's burst sends over the water takes to spread to the edge of the blast
        /// (<see cref="RippleRadius" />) and fade.</summary>
        float RingSeconds { get; }

        /// <summary>The sorting layer the ring draws on.</summary>
        string RingSortingLayerName { get; }

        /// <summary>The ring's order on its layer: below the balloons, so it reads as on the water.</summary>
        int RingSortingOrder { get; }
    }
}
