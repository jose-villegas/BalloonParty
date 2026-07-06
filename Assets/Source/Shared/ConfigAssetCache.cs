#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>Lazily finds and caches a <see cref="ScriptableObject"/> asset by type; one instance per config type.</summary>
    public sealed class ConfigAssetCache<T>
        where T : ScriptableObject
    {
        private T _asset;
        private bool _searched;

        /// <summary>Returns the cached asset, searching on first access; <c>null</c> if none exists.</summary>
        public T Value
        {
            get
            {
                if (!_searched)
                {
                    _searched = true;
                    _asset = Find();
                }

                return _asset;
            }
        }

        /// <summary>Clears the cache so the next <see cref="Value"/> access re-searches.</summary>
        public void Invalidate()
        {
            _asset = null;
            _searched = false;
        }

        private static T Find()
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

            if (guids.Length == 0)
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
#endif
