using UnityEngine;

namespace BalloonParty.Shared.GameState
{
    /// <summary>Lets a scene be played standalone in the Editor by transitioning to <see cref="_targetState" /> on Awake if still at <see cref="NavigationState.Launch" />.</summary>
    internal class EditorNavigationBootstrap : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField] private NavigationState _targetState = NavigationState.Game;

        private void Awake()
        {
            if (Navigation.Current.Value == NavigationState.Launch
                && gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
            {
                Navigation.TransitionTo(_targetState);
            }
        }
#endif
    }
}
