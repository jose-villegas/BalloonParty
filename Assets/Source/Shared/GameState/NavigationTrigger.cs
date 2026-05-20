using UnityEngine;

namespace BalloonParty.Shared.GameState
{
    /// <summary>
    ///     Triggers a navigation state transition. Wire the public <see cref="Transition" />
    ///     method to a Button's onClick in the Inspector and set <see cref="_targetState" />
    ///     to the desired destination.
    /// </summary>
    public class NavigationTrigger : MonoBehaviour
    {
        [SerializeField] private NavigationState _targetState;

        public void Transition()
        {
            Navigation.TransitionTo(_targetState);
        }
    }
}
