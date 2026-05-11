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
        [SerializeField] private ParticleSystem _activationVfxPrefab;

        [Header("Bomb")]
        [SerializeField] private float _bombRadius = 1.25f;

        public ItemType Type => _type;
        public int TurnCheckEvery => _turnCheckEvery;
        public float Weight => _weight;
        public int MaximumAllowed => _maximumAllowed;
        public GameObject VisualPrefab => _visualPrefab;
        public ParticleSystem ActivationVfxPrefab => _activationVfxPrefab;
        public float BombRadius => _bombRadius;
    }
}
