using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    /// <summary>
    ///     The formation state a member keeps for <c>UnbreakableFormationService</c>. Unbreakables and toughs both
    ///     join formations, so each model holds one of these and forwards <see cref="IWriteableFormationMember" />.
    /// </summary>
    internal sealed class FormationMembership
    {
        private readonly ReactiveProperty<bool> _inFormation = new(false);

        internal IReadOnlyReactiveProperty<bool> InFormation => _inFormation;
        internal Vector2 Heading { get; private set; } = Vector2.right;
        internal bool Walking { get; private set; }

        internal void SetInFormation(bool inFormation)
        {
            _inFormation.Value = inFormation;
            if (!inFormation)
            {
                Walking = false;
            }
        }

        internal void SetWalking(bool walking)
        {
            Walking = walking;
        }

        internal void SetHeading(Vector2 heading)
        {
            Heading = heading;
        }
    }
}
