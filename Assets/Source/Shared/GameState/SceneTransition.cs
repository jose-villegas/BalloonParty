using BalloonParty.Shared.Extensions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BalloonParty.Shared.GameState
{
    /// <summary>Scene transition component; wire <see cref="Load" /> to a Button's onClick. If <see cref="_preload" /> is set, the scene loads additively with rendering suppressed until <see cref="Load" /> activates it.</summary>
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
