namespace BalloonParty.Configuration.Balloons
{
    /// <summary>How unbreakables gather into formations, hold them, and pick the next shape.</summary>
    public interface IUnbreakableFormationSettings
    {
        bool Enabled { get; }

        /// <summary>Fewest unbreakables on the board that form up.</summary>
        int MinMembers { get; }

        /// <summary>Most unbreakables in one formation; the rest keep roaming.</summary>
        int MaxMembers { get; }

        /// <summary>Waves a formation holds, counting the wave it formed in, before it changes shape.</summary>
        int HoldWaves { get; }

        /// <summary>Bottom rows a formation never uses, so spawn lines always have room to enter.</summary>
        int BottomClearance { get; }

        /// <summary>A formation picks at random among this many of its least-travel spots.</summary>
        int SpotChoices { get; }

        float HorizontalWeight { get; }

        /// <summary>Pick weight of each of the two diagonals.</summary>
        float DiagonalWeight { get; }

        /// <summary>Pick weight of covering an item from below; only in the pick while an item is on the board.</summary>
        float ItemCoverWeight { get; }
    }
}
