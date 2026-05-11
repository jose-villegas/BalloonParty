using System;
using UnityEngine;

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
        [SerializeField] private GameObject _activationVfxPrefab;

        [Header("Bomb")]
        [SerializeField] private float _bombRadius = 1.25f;
        [SerializeField] private float _bombNudgeDistance = 0.15f;
        [SerializeField] private float _bombNudgeFalloff = 1.5f;

        [Header("Laser")]
        [SerializeField] private float _laserRaycastDistance = 20f;
        [SerializeField] private float _laserCircleCastRadius = 0.065f;

        public ItemType Type => _type;
        public int TurnCheckEvery => _turnCheckEvery;
        public float Weight => _weight;
        public int MaximumAllowed => _maximumAllowed;
        public GameObject VisualPrefab => _visualPrefab;
        public GameObject ActivationVfxPrefab => _activationVfxPrefab;
        public float BombRadius => _bombRadius;
        public float BombNudgeDistance => _bombNudgeDistance;
        public float BombNudgeFalloff => _bombNudgeFalloff;
        public float LaserRaycastDistance => _laserRaycastDistance;
        public float LaserCircleCastRadius => _laserCircleCastRadius;
    }
}
