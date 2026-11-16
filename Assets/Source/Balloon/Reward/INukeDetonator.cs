using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>Sets an armed nuke off: <c>NukeBlast</c>, called by <c>NukeFuse</c> when the fuse runs out.</summary>
    internal interface INukeDetonator
    {
        void Detonate(NukeBalloonModel nuke, Vector3 center);
    }
}
