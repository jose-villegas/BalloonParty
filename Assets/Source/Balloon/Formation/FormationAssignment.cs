using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>One member and the formation slot it goes to.</summary>
    internal readonly struct FormationAssignment
    {
        public readonly IWriteableFormationMember Member;
        public readonly Vector2Int Target;

        public FormationAssignment(IWriteableFormationMember member, Vector2Int target)
        {
            Member = member;
            Target = target;
        }
    }
}
