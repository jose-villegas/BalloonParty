using UnityEngine;
#if UNITY_EDITOR || ((DEVELOPMENT_BUILD || CHEATS_IN_RELEASE) && (UNITY_ANDROID || UNITY_IOS))
using BalloonParty.Shared.Diagnostics;
#endif

namespace BalloonParty.Cheats
{
    /// <summary>
    ///     Spawns yasirkula's In-game Debug Console (assigned as a prefab) at startup so <c>Debug.Log</c>
    ///     output is readable on-device — mobile development builds only. Place one in the Launch scene
    ///     and assign the console prefab.
    /// </summary>
    internal class DevLogConsole : MonoBehaviour
    {
        [SerializeField] private GameObject _consolePrefab;

#if UNITY_EDITOR || (DEVELOPMENT_BUILD && (UNITY_ANDROID || UNITY_IOS))
        private void Awake()
        {
            if (Application.isEditor)
            {
                return;
            }

            if (_consolePrefab == null)
            {
                Log.Warn("DevLogConsole", "no console prefab assigned — nothing to spawn.", this);
                return;
            }

            DontDestroyOnLoad(Instantiate(_consolePrefab));
        }
#endif
    }
}
