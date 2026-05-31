using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Grid Actor Configuration", fileName = "GridActorConfiguration")]
    public class GridActorConfiguration : ScriptableObject
    {
        [SerializeField] private GridActorPrefabEntry[] _entries;

        public GridActorPrefabEntry[] Entries => _entries;
    }
}
