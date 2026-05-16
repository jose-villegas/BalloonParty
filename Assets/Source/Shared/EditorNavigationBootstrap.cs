using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     Editor-only bootstrap that transitions to <see cref="_targetState" /> on Awake
    ///     if the current navigation state is still <see cref="NavigationState.Launch" />.
    ///     Place on a GameObject in any scene that needs to be playable standalone from
    ///     the Editor without following the full navigation path.
    /// </summary>
    internal class EditorNavigationBootstrap : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField] private NavigationState _targetState = NavigationState.Game;

        private void Awake()
        {
            if (Navigation.State.Value == NavigationState.Launch
                && gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
            {
                Navigation.TransitionTo(_targetState);
            }
        }
#endif
    }
}


