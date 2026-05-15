using UnityEngine;
using UnityEngine.SceneManagement;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     General-purpose scene transition component. Attach to any GameObject and
    ///     wire <see cref="Load" /> to a Button's onClick event in the Inspector.
    /// </summary>
    public class SceneTransition : MonoBehaviour
    {
        [SerializeField] private string _sceneName;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_sceneName))
            {
                Debug.LogWarning(
                    $"[SceneTransition] No scene name configured on \"{gameObject.name}\".", this);
            }
        }
#endif

        public void Load()
        {
            if (string.IsNullOrWhiteSpace(_sceneName))
            {
                Debug.LogWarning("[SceneTransition] No scene name configured.", this);
                return;
            }

            SceneManager.LoadScene(_sceneName);
        }
    }
}
