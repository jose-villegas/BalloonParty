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
        [SerializeField] [GradientUsage(false)] private Gradient _branchGradient = CreateDefaultBranchGradient();
        [SerializeField] private Material _leafMaterial;
        [SerializeField] private float _bushWorldSize = 0.9f;

        [Header("Branch Shadow")]
        [SerializeField] [Range(0.3f, 1f)] private float _branchSpriteScale = 0.85f;
        [SerializeField] private Color _branchShadowColor = new(0.06f, 0.06f, 0.14f, 0.35f);
        [SerializeField] private Vector2 _branchShadowOffset = new(0.04f, -0.06f);
        [SerializeField] [Range(0f, 1f)] private float _branchShadowSpread = 0.15f;
        [SerializeField] [Range(0f, 0.08f)] private float _branchShadowSoftness = 0.02f;

        [Header("Branch AO")]
        [SerializeField] private Color _branchAOColor = new(0.02f, 0.02f, 0.06f, 0.4f);
        [SerializeField] [Range(0.05f, 1f)] private float _branchAORadius = 0.45f;
        [SerializeField] [Range(0.01f, 1f)] private float _branchAOSoftness = 0.3f;

        [Header("Leaf Atlas")]
        [SerializeField] private Sprite[] _leafAtlasSprites;

        [Header("Leaf Shadow")]
        [SerializeField] private Color _leafShadowColor = new(0.15f, 0.18f, 0.1f, 0.55f);
        [SerializeField] private Vector2 _leafShadowOffset = new(0.04f, -0.06f);
        [SerializeField] [Range(0f, 0.08f)] private float _leafShadowSoftness = 0.015f;
        [SerializeField] [Range(0.3f, 1f)] private float _leafSpriteScale = 0.75f;
        [SerializeField] [Range(-0.5f, 0.5f)] private float _leafPivotOffset;
        [SerializeField] [Range(0f, 1f)] private float _leafDepthSplit = 0.4f;

        [Header("Wind")]
        [SerializeField] private float _windAmplitude = 3f;
        [SerializeField] private float _windPeriod = 2f;
        [SerializeField] private float _windNoiseAmplitude = 1.5f;
        [SerializeField] [Range(0f, 0.1f)] private float _windScalePulse = 0.03f;

        [Header("Rattle")]
        [SerializeField] private bool _rattleEnabled = true;
        [SerializeField] private float _rattleAmplitude = 15f;
        [SerializeField] private float _rattleFrequency = 12f;
        [SerializeField] [Range(1f, 10f)] private float _rattleDamping = 3f;

        public BushView BushPrefab => _bushPrefab;
        public float AnimationSpeed => _animationSpeed;
        public float Padding => _padding;
        public int SortingLayerId => _sortingLayerId;
        public int SortingOrderOffset => _sortingOrderOffset;
        public BushVariantData[] BushVariants => _bushVariants;
        public Shader BranchShader => _branchShader;
        public Gradient BranchGradient => _branchGradient;
        public Material LeafMaterial => _leafMaterial;
        public float BushWorldSize => _bushWorldSize;
        public float BranchSpriteScale => _branchSpriteScale;
        public Color BranchShadowColor => _branchShadowColor;
        public Vector2 BranchShadowOffset => _branchShadowOffset;
        public float BranchShadowSpread => _branchShadowSpread;
        public float BranchShadowSoftness => _branchShadowSoftness;
        public Color BranchAOColor => _branchAOColor;
        public float BranchAORadius => _branchAORadius;
        public float BranchAOSoftness => _branchAOSoftness;
        public Sprite[] LeafAtlasSprites => _leafAtlasSprites;
        public Color LeafShadowColor => _leafShadowColor;
        public Vector2 LeafShadowOffset => _leafShadowOffset;
        public float LeafShadowSoftness => _leafShadowSoftness;
        public float LeafSpriteScale => _leafSpriteScale;
        public float LeafPivotOffset => _leafPivotOffset;
        public float LeafDepthSplit => _leafDepthSplit;
        public float WindAmplitude => _windAmplitude;
        public float WindPeriod => _windPeriod;
        public float WindNoiseAmplitude => _windNoiseAmplitude;
        public float WindScalePulse => _windScalePulse;
        public bool RattleEnabled => _rattleEnabled;
        public float RattleAmplitude => _rattleAmplitude;
        public float RattleFrequency => _rattleFrequency;
        public float RattleDamping => _rattleDamping;

        private static Gradient CreateDefaultBranchGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.25f, 0.15f, 0.08f), 0f),
                    new GradientColorKey(new Color(0.40f, 0.26f, 0.14f), 0.5f),
                    new GradientColorKey(new Color(0.25f, 0.15f, 0.08f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return g;
        }
    }
}
