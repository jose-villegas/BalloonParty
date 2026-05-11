#region

using System;
using UnityEngine;

#endregion

namespace BalloonParty.Configuration
{
    [Serializable]
    public class ItemSettings
    {
        [SerializeField] private ItemType _type;
        [SerializeField] private int _turnCheckEvery;
        [SerializeField] private float _weight;
        [SerializeField] private int _maximumAllowed;
        [SerializeField] private GameObject _visualPrefab;

        public ItemType Type => _type;
        public int TurnCheckEvery => _turnCheckEvery;
        public float Weight => _weight;
        public int MaximumAllowed => _maximumAllowed;
        public GameObject VisualPrefab => _visualPrefab;
    }
}
