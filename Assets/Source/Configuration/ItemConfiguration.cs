using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Item Configuration", fileName = "ItemConfiguration")]
    public class ItemConfiguration : ScriptableObject
    {
        [SerializeField] private List<ItemSettings> _items;

        public List<ItemSettings> Items => _items;

        public ItemSettings this[ItemType type] => _items.First(x => x.Type == type);
    }
}
