namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     When the nuke balloon comes (PLAN-NukeBalloon): a tiny chance on each clock line, raised toward
    ///     <see cref="MaxChance" /> by the trouble the player is in. The trouble is the weighted sum of four signals,
    ///     each 0 to 1, capped at 1: the board's fill, the spawn clock's pressure, low hearts, and hearts lost in
    ///     quick succession.
    /// </summary>
    public interface INukeOddsSettings
    {
        /// <summary>The first level that can roll one; 0 or 1 for every level.</summary>
        int FromLevel { get; }

        /// <summary>How many a level can bring at most.</summary>
        int PerLevel { get; }

        /// <summary>Clock lines after one is placed before the next can roll.</summary>
        int CooldownLines { get; }

        /// <summary>The chance on a line out of trouble.</summary>
        float BaseChance { get; }

        /// <summary>The chance on a line in full trouble.</summary>
        float MaxChance { get; }

        /// <summary>The board's occupancy at which the fill signal starts to rise from 0.</summary>
        float FillFrom { get; }

        /// <summary>The board's occupancy at which the fill signal reaches 1.</summary>
        float FillFull { get; }

        float FillWeight { get; }

        /// <summary>The spawn clock's pressure (0 to 1) is the signal as it stands.</summary>
        float PressureWeight { get; }

        /// <summary>The hearts' need: 0 at full hearts, 1 on the last.</summary>
        float HeartsWeight { get; }

        /// <summary>The recent losses over <see cref="LossesForFull" />.</summary>
        float LossWeight { get; }

        /// <summary>Hearts lost recently that read as a full signal.</summary>
        float LossesForFull { get; }

        /// <summary>The share of the recent losses each clock line keeps, so they fade.</summary>
        float LossKeptPerLine { get; }
    }
}
