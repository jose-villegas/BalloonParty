using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>The nuke's mushroom cloud: it rises from where the nuke went off and erodes away on its own.</summary>
    internal interface INukeCloudView
    {
        void Play(Vector3 center);

        /// <summary>Takes the cloud away at once: a new run or level shouldn't open under the last one's cloud.</summary>
        void Stop();
    }
}
