using BalloonParty.Shared;

namespace BalloonParty.Game.Upgrades
{
    /// <summary>
    ///     The run's weapon after its upgrade cards: the configured base plus every card held. Read these
    ///     instead of <see cref="IProjectileFlightConfig" /> wherever a card can change the value.
    /// </summary>
    internal interface IWeaponStats
    {
        /// <summary>Most balls in flight at once: <see cref="IProjectileFlightConfig.MaxBallsInFlight" /> plus cards.</summary>
        int MaxBallsInFlight { get; }

        /// <summary>
        ///     Seconds the next ball takes to load while another ball is flying: the longer of the load
        ///     slide and the cooldown, shortened by reload cards.
        /// </summary>
        float ReloadSeconds { get; }

        /// <summary>
        ///     Seconds the next ball takes to load while no ball is flying:
        ///     <see cref="IProjectileFlightConfig.ProjectileLoadDuration" />, which cards don't change.
        /// </summary>
        float IdleReloadSeconds { get; }

        /// <summary>Shields each new ball starts with: the configured count plus cards.</summary>
        int StartingShields { get; }

        /// <summary>Extra items per item roll; the whole part always lands, the fraction is a chance.</summary>
        float ExtraItemChance { get; }
    }
}
