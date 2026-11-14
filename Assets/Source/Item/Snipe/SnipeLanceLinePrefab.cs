namespace BalloonParty.Item.Snipe
{
    /// <summary>The pencil line's prefab, held so an unassigned one still registers.</summary>
    internal sealed class SnipeLanceLinePrefab
    {
        internal SnipeLanceLineView Prefab { get; }

        internal SnipeLanceLinePrefab(SnipeLanceLineView prefab)
        {
            Prefab = prefab;
        }
    }
}
