using BalloonParty.Slots.Actor.Archetype;
using NaughtyAttributes;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Bush Settings", fileName = "BushSettings")]
    internal class BushSettings : ScriptableObject, IBushSettings
    {
        [Header("Prefab")]
        [SerializeField] private BushView _bushPrefab;

        [Header("Animation")]
        [Tooltip("Noise scroll speed multiplier. Drives _TimeOffset on the shader.")]
        [SerializeField] private float _animationSpeed = 0.8f;

        [Header("Visual")]
        [Tooltip("Extra world-space padding beyond the cluster bounding box.")]
        [SerializeField] private float _padding = 0.5f;

        [Tooltip("Sorting layer for bush renderers.")]
        [SortingLayer]
        [SerializeField] private int _sortingLayerId;

        [Tooltip("Sorting order offset for bush renderers — lower than Puff so bushes render below clouds.")]
        [SerializeField] private int _sortingOrderOffset = -1;

        [Header("Shape")]
        [Tooltip("World-space radius of a single bush slot. Must match the shader's _SlotRadius.")]
        [SerializeField] private float _slotRadius = 0.4f;

        [Tooltip("Phyllotaxis spiral spread factor. Must match the shader's _BranchSpread.")]
        [SerializeField] private float _branchSpread = 0.55f;

        [Header("Baked Assets")]
        [SerializeField] private Sprite[] _canopyVariants;
        [SerializeField] private Sprite[] _leafAtlasSprites;

        [Tooltip("World-space diameter of a single canopy sprite per slot.")]
        [SerializeField] private float _canopyDiameter = 0.9f;

        [Tooltip("World-space size of an individual leaf sprite.")]
        [SerializeField] private float _leafSpriteSize = 0.18f;

        [Header("Ruffle")]
        [SerializeField] private int _ruffleLeafCount = 6;
        [SerializeField] private float _ruffleRadius = 1.5f;

        public BushView BushPrefab => _bushPrefab;
        public float AnimationSpeed => _animationSpeed;
        public float Padding => _padding;
        public int SortingLayerId => _sortingLayerId;
        public int SortingOrderOffset => _sortingOrderOffset;
        public Sprite[] CanopyVariants => _canopyVariants;
        public Sprite[] LeafAtlasSprites => _leafAtlasSprites;
        public float SlotRadius => _slotRadius;
        public float BranchSpread => _branchSpread;
        public float CanopyDiameter => _canopyDiameter;
        public float LeafSpriteSize => _leafSpriteSize;
        public int RuffleLeafCount => _ruffleLeafCount;
        public float RuffleRadius => _ruffleRadius;
    }
}
