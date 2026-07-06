using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared.Rendering;
using UnityEngine;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Owns the materials a bush renders with. The two leaf materials differ only by
    /// render queue (inner behind branches, outer in front) — the atlas texture and all
    /// shader props are shared — so two instances cover every slot. Branch materials
    /// differ only by the variant's branch map, so slots sharing a variant share one.
    /// </summary>
    internal sealed class BushMaterialSet
    {
        private readonly IBushSettings _settings;
        private readonly Dictionary<Texture, Material> _branchMaterials = new();

        private Material _innerLeaf;
        private Material _outerLeaf;
        private Texture2D _branchGradient;

        internal Material InnerLeaf => _innerLeaf;
        internal Material OuterLeaf => _outerLeaf;

        internal BushMaterialSet(IBushSettings settings)
        {
            _settings = settings;
        }

        internal void BuildLeafMaterials(Sprite[] sprites)
        {
            if (_settings.LeafMaterial == null || sprites == null || sprites.Length == 0)
            {
                return;
            }

            _innerLeaf = CreateLeafMaterial(sprites, 2999);
            _outerLeaf = CreateLeafMaterial(sprites, 3001);
        }

        internal Material GetBranchMaterial(Texture branchMap)
        {
            if (_settings.BranchShader == null || branchMap == null)
            {
                return null;
            }

            if (_branchMaterials.TryGetValue(branchMap, out var existing))
            {
                return existing;
            }

            var branchSpriteScale = Mathf.Max(_settings.BranchSpriteScale, 0.3f);
            var material = new Material(_settings.BranchShader)
            {
                mainTexture = branchMap,
                renderQueue = 3000
            };
            material.SetFloat(BushShaderProperties.SpriteScale, branchSpriteScale);
            material.SetTexture(BushShaderProperties.BranchGradient, GetOrBakeGradient());
            material.SetColor(BushShaderProperties.ShadowColor, _settings.BranchShadowColor);
            material.SetVector(BushShaderProperties.ShadowOffset, _settings.BranchShadowOffset);
            material.SetFloat(BushShaderProperties.ShadowSpread, _settings.BranchShadowSpread);
            material.SetFloat(BushShaderProperties.ShadowSoftness, _settings.BranchShadowSoftness);
            material.SetColor(BushShaderProperties.AOColor, _settings.BranchAOColor);
            material.SetFloat(BushShaderProperties.AORadius, _settings.BranchAORadius);
            material.SetFloat(BushShaderProperties.AOSoftness, _settings.BranchAOSoftness);

            _branchMaterials[branchMap] = material;
            return material;
        }

        internal void Release()
        {
            DestroyObject(_innerLeaf);
            _innerLeaf = null;
            DestroyObject(_outerLeaf);
            _outerLeaf = null;

            foreach (var material in _branchMaterials.Values)
            {
                DestroyObject(material);
            }

            _branchMaterials.Clear();

            DestroyObject(_branchGradient);
            _branchGradient = null;
        }

        private Material CreateLeafMaterial(Sprite[] sprites, int queue)
        {
            var material = new Material(_settings.LeafMaterial)
            {
                mainTexture = sprites[0].texture,
                enableInstancing = true,
                renderQueue = queue
            };
            material.SetColor(BushShaderProperties.ShadowColor, _settings.LeafShadowColor);
            material.SetVector(BushShaderProperties.ShadowOffset, _settings.LeafShadowOffset);
            material.SetFloat(BushShaderProperties.ShadowSoftness, _settings.LeafShadowSoftness);
            material.SetFloat(BushShaderProperties.SpriteScale, _settings.LeafSpriteScale);

            var frequency = _settings.WindPeriod > 0f ? 1f / _settings.WindPeriod : 1f;
            material.SetFloat(BushShaderProperties.WindFrequency, frequency);
            material.SetFloat(BushShaderProperties.WindAmplitude, _settings.WindAmplitude);
            material.SetFloat(BushShaderProperties.WindNoiseAmplitude, _settings.WindNoiseAmplitude);
            material.SetFloat(BushShaderProperties.WindScalePulse, _settings.WindScalePulse);
            material.SetFloat(BushShaderProperties.PivotOffset, _settings.LeafPivotOffset);

            if (_settings.RattleEnabled)
            {
                material.EnableKeyword(BushShaderProperties.RattleKeyword);
                material.SetFloat(BushShaderProperties.RattleAmplitude, _settings.RattleAmplitude);
                material.SetFloat(BushShaderProperties.RattleFrequency, _settings.RattleFrequency);
                material.SetFloat(BushShaderProperties.RattleDamping, _settings.RattleDamping);
            }

            return material;
        }

        private Texture2D GetOrBakeGradient()
        {
            _branchGradient ??= GradientTextureHelper.Bake(_settings.BranchGradient);
            return _branchGradient;
        }

        // DestroyImmediate is required to release materials created in edit mode (the
        // bush view runs under [ExecuteAlways]); plain Destroy is illegal there.
        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
