using UnityEngine;

namespace BalloonParty.Shared.GameState
{
    /// <summary>Wire <see cref="Transition" /> to a Button's onClick to trigger a navigation transition to <see cref="_targetState" />.</summary>
    public class NavigationTrigger : MonoBehaviour
    {
        [SerializeField] private NavigationState _targetState;

        public void Transition()
        {
            Navigation.TransitionTo(_targetState);
        }
    }
}
