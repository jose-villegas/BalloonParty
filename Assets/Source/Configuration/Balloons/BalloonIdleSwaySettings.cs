using System;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     Baked, not typed in: every value here comes from <see cref="Source" /> and its clips, written by the
    ///     idle sway baker. Edit the controller or its clips and bake again.
    /// </summary>
    [Serializable]
    public class BalloonIdleSwaySettings : IBalloonIdleSwaySettings
    {
        [Tooltip("The Animator controller the sway was baked from. Balloons whose Animator plays it are swayed " +
                 "by one shared ticker and their Animator is switched off.")]
        [SerializeField] private RuntimeAnimatorController _source;

        [Tooltip("The swayed child, as a path from the Animator's transform.")]
        [SerializeField] private string _pivotPath;

        [Tooltip("The sway of a balloon at rest in its slot.")]
        [SerializeField] private BalloonSwayLoop _stable = new();

        [Tooltip("The wider sway of a balloon on the move.")]
        [SerializeField] private BalloonSwayLoop _unstable = new();

        public RuntimeAnimatorController Source => _source;
        public string PivotPath => _pivotPath;
        public IBalloonSwayLoop Stable => _stable;
        public IBalloonSwayLoop Unstable => _unstable;

        public BalloonIdleSwaySettings()
        {
        }

        public BalloonIdleSwaySettings(
            RuntimeAnimatorController source, string pivotPath, BalloonSwayLoop stable, BalloonSwayLoop unstable)
        {
            _source = source;
            _pivotPath = pivotPath;
            _stable = stable;
            _unstable = unstable;
        }
    }
}
