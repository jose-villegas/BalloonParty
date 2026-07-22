using System;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Utilities
{
    /// <summary>
    /// Lazy-loading cache for a ScriptableObject asset found by type.
    /// Assumes a single instance exists in the project; logs a warning if multiple are found.
    /// </summary>
    public sealed class EditorAssetCache<T> where T : ScriptableObject
    {
        private readonly Func<T[]> _finder;
        private T _cached;
        private bool _searched;

        public EditorAssetCache() : this(DefaultFinder) { }

        /// <summary>Test seam: inject a custom finder function.</summary>
        internal EditorAssetCache(Func<T[]> finder)
        {
            _finder = finder ?? throw new ArgumentNullException(nameof(finder));
        }

        /// <summary>Returns the cached asset, searching on first access. Null if none exists.</summary>
        public T Value
        {
            get
            {
                if (!_searched)
                {
                    _searched = true;
                    var results = _finder();

                    if (results == null || results.Length == 0)
                    {
                        _cached = null;
                    }
                    else
                    {
                        if (results.Length > 1)
                        {
                            Debug.LogWarning(
                                $"[EditorAssetCache] Multiple {typeof(T).Name} assets found ({results.Length}). Using first.");
                        }

                        _cached = results[0];
                    }
                }

                return _cached;
            }
        }

        /// <summary>Clears the cache so the next <see cref="Value"/> access re-searches.</summary>
        public void Invalidate()
        {
            _cached = null;
            _searched = false;
        }

        private static T[] DefaultFinder()
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

            if (guids.Length == 0)
            {
                return Array.Empty<T>();
            }

            var results = new T[guids.Length];

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                results[i] = AssetDatabase.LoadAssetAtPath<T>(path);
            }

            return results;
        }
    }
}
