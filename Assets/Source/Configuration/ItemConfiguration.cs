using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Item Configuration", fileName = "ItemConfiguration")]
    public class ItemConfiguration : ScriptableObject, IItemConfiguration
    {
        [SerializeField] private List<ItemSettings> _items;

        public IReadOnlyList<ItemSettings> Items => _items;

        // Plain loop over the handful of entries — First() allocates a closure and an enumerator
        // per lookup, and every item activation resolves its settings through here.
        public ItemSettings this[ItemType type]
        {
            get
            {
                for (var i = 0; i < _items.Count; i++)
                {
                    if (_items[i].Type == type)
                    {
                        return _items[i];
                    }
                }

                throw new InvalidOperationException($"No ItemSettings entry for item type '{type}'.");
            }
        }
    }
}
