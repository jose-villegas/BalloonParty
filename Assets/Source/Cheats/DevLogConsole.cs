// Compiles in the editor (so the component stays wireable in the inspector) and in mobile development
// builds. Desktop dev builds and all release builds strip it — and with it the serialized prefab
// reference, keeping the console out of those builds entirely.
#if UNITY_EDITOR || (DEVELOPMENT_BUILD && (UNITY_ANDROID || UNITY_IOS))

using BalloonParty.Shared.Diagnostics;
using UnityEngine;

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

        private void Awake()
        {
            // The editor has its own Console window; this is for on-device logs, so skip it in-editor.
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
    }
}
#endif
