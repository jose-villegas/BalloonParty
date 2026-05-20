using BalloonParty.Shared.Extensions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BalloonParty.Shared.GameState
{
    /// <summary>
    ///     General-purpose scene transition component. Attach to any GameObject and
    ///     wire <see cref="Load" /> to a Button's onClick event in the Inspector.
    ///     When <see cref="_preload" /> is enabled the target scene is loaded additively
    ///     on <c>Start</c> with rendering disabled so all game logic (DI, pool pre-warming)
    ///     runs in the background. <see cref="Load" /> restores rendering and unloads the
    ///     current scene.
    /// </summary>
    public class SceneTransition : MonoBehaviour
    {
        [SerializeField] private string _sceneName;
        [SerializeField] private bool _preload;

        private SceneRenderingHandle _renderingHandle;
        private Scene _preloadedScene;
        private bool _preloadComplete;
        private bool _activating;

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

            if (_activating)
            {
                return;
            }

            if (_preloadComplete)
            {
                ActivatePreloadedScene();
                return;
            }

            if (_preloadedScene.IsValid())
            {
                _activating = true;
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

            SceneManager.sceneLoaded += OnSceneLoaded;

            var operation = SceneManager.LoadSceneAsync(_sceneName, LoadSceneMode.Additive);
            if (operation != null)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            Debug.LogWarning(
                $"[SceneTransition] Failed to preload scene \"{_sceneName}\".",
                this);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != _sceneName)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;

            _preloadedScene = scene;
            _renderingHandle = scene.SuppressRendering();
            _preloadComplete = true;

            if (_activating)
            {
                ActivatePreloadedScene();
            }
        }

        private void ActivatePreloadedScene()
        {
            _renderingHandle?.Restore();
            SceneManager.SetActiveScene(_preloadedScene);
            SceneManager.UnloadSceneAsync(gameObject.scene);
        }
    }
}
