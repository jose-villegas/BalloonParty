using UnityEngine;
using UnityEngine.SceneManagement;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     General-purpose scene transition component. Attach to any GameObject and
    ///     wire <see cref="Load" /> to a Button's onClick event in the Inspector.
    ///     When <see cref="_preload" /> is enabled the target scene is loaded in the
    ///     background on <c>Start</c> and held at 90% until <see cref="Load" /> activates it.
    /// </summary>
    public class SceneTransition : MonoBehaviour
    {
        [SerializeField] private string _sceneName;
        [SerializeField] private bool _preload;

        private AsyncOperation _preloadOperation;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_sceneName))
            {
                Debug.LogWarning(
                    $"[SceneTransition] No scene name configured on \"{gameObject.name}\".",
                    this);
            }
        }
#endif

        private void Start()
        {
            if (_preload)
            {
                Preload();
            }
        }

        public void Load()
        {
            if (string.IsNullOrWhiteSpace(_sceneName))
            {
                Debug.LogWarning("[SceneTransition] No scene name configured.", this);
                return;
            }

            if (_preloadOperation != null)
            {
                _preloadOperation.allowSceneActivation = true;
                return;
            }

            SceneManager.LoadScene(_sceneName);
        }

        private void Preload()
        {
            if (string.IsNullOrWhiteSpace(_sceneName))
            {
                return;
            }

            _preloadOperation = SceneManager.LoadSceneAsync(_sceneName);
            if (_preloadOperation == null)
            {
                Debug.LogWarning(
                    $"[SceneTransition] Failed to preload scene \"{_sceneName}\".", this);
                return;
            }

            _preloadOperation.allowSceneActivation = false;
        }
    }
}
