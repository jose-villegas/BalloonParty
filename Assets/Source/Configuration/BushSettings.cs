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
        [SerializeField] private float _animationSpeed = 0.8f;

        [Header("Visual")]
        [SerializeField] private float _padding = 0.5f;

        [SortingLayer]
        [SerializeField] private int _sortingLayerId;

        [SerializeField] private int _sortingOrderOffset = -1;

        [Header("Branch Map")]
        [SerializeField] private BushVariantData[] _bushVariants;
        [SerializeField] private Shader _branchShader;
        [SerializeField] private Shader _leafShader;
        [SerializeField] private float _bushWorldSize = 0.9f;

        [Header("Leaf Atlas")]
        [SerializeField] private Sprite[] _leafAtlasSprites;

        [Header("Leaf Shadow")]
        [SerializeField] private Color _leafShadowColor = new(0.15f, 0.18f, 0.1f, 0.55f);
        [SerializeField] private Vector2 _leafShadowOffset = new(0.04f, -0.06f);
        [SerializeField] [Range(0f, 0.08f)] private float _leafShadowSoftness = 0.015f;
        [SerializeField] [Range(0.3f, 1f)] private float _leafSpriteScale = 0.75f;
        [SerializeField] [Range(0f, 0.5f)] private float _leafPivotOffset = 0.15f;

        [Header("Wind")]
        [SerializeField] private float _windAmplitude = 3f;
        [SerializeField] private float _windPeriod = 2f;
        [SerializeField] private float _windNoiseAmplitude = 1.5f;
        [SerializeField] [Range(0f, 0.1f)] private float _windScalePulse = 0.03f;

        public BushView BushPrefab => _bushPrefab;
        public float AnimationSpeed => _animationSpeed;
        public float Padding => _padding;
        public int SortingLayerId => _sortingLayerId;
        public int SortingOrderOffset => _sortingOrderOffset;
        public BushVariantData[] BushVariants => _bushVariants;
        public Shader BranchShader => _branchShader;
        public Shader LeafShader => _leafShader;
        public float BushWorldSize => _bushWorldSize;
        public Sprite[] LeafAtlasSprites => _leafAtlasSprites;
        public Color LeafShadowColor => _leafShadowColor;
        public Vector2 LeafShadowOffset => _leafShadowOffset;
        public float LeafShadowSoftness => _leafShadowSoftness;
        public float LeafSpriteScale => _leafSpriteScale;
        public float LeafPivotOffset => _leafPivotOffset;
        public float WindAmplitude => _windAmplitude;
        public float WindPeriod => _windPeriod;
        public float WindNoiseAmplitude => _windNoiseAmplitude;
        public float WindScalePulse => _windScalePulse;
    }
}
