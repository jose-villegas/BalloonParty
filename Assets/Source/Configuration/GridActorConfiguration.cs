using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Grid Actor Configuration", fileName = "GridActorConfiguration")]
    public class GridActorConfiguration : ScriptableObject, IGridActorConfiguration
    {
        [SerializeField] private GridActorPrefabEntry[] _entries;

        public IReadOnlyList<GridActorPrefabEntry> Entries => _entries;
    }
}
