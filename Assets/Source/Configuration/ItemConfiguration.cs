#region

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#endregion

namespace BalloonParty.Configuration
{
    [Serializable]
    public class ItemConfiguration
    {
        [SerializeField] private List<ItemSettings> _items;

        public List<ItemSettings> Items => _items;

        public ItemSettings this[ItemType type] => _items.First(x => x.Type == type);
    }
}
