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

        [Header("Wind")]
        [SerializeField] private float _windAmplitude = 3f;
        [SerializeField] private float _windPeriod = 2f;

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
        public float WindAmplitude => _windAmplitude;
        public float WindPeriod => _windPeriod;
    }
}
