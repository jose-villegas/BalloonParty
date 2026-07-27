using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Run Config", fileName = "RunConfig")]
    internal class RunConfig : ScriptableObject, IRunConfig
    {
        [Header("Run")]
        [SerializeField] private int _startingHitPoints = 5;
        [SerializeField] private int _maxRetries = 1;

        public int StartingHitPoints => _startingHitPoints;
        public int MaxRetries => _maxRetries;
    }
}
