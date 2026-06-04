using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    /// <summary>
    /// Renders a single Gielis leaf into a <see cref="Texture2D"/> using
    /// <c>BushBakeLeaf.shader</c> via a temporary offscreen camera.
    /// </summary>
    internal static class BushLeafBaker
    {
        private const string ShaderName = "BalloonParty/Grid/BushBakeLeaf";
        private const int BakeLayer = 31;

        private static readonly int LeafRadiusId = Shader.PropertyToID("_LeafRadius");
        private static readonly int AAWidthId = Shader.PropertyToID("_AAWidth");
        private static readonly int GielisMId = Shader.PropertyToID("_GielisM");
        private static readonly int GielisN1Id = Shader.PropertyToID("_GielisN1");
        private static readonly int GielisN2Id = Shader.PropertyToID("_GielisN2");
        private static readonly int GielisN3Id = Shader.PropertyToID("_GielisN3");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EdgeShadeId = Shader.PropertyToID("_EdgeShade");
        private static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");
        private static readonly int HighlightSizeId = Shader.PropertyToID("_HighlightSize");
        private static readonly int HighlightOffsetId = Shader.PropertyToID("_HighlightOffset");
        private static readonly int VeinWidthId = Shader.PropertyToID("_VeinWidth");
        private static readonly int VeinDarkenId = Shader.PropertyToID("_VeinDarken");
        private static readonly int SSSStrengthId = Shader.PropertyToID("_SSSStrength");
        private static readonly int SSSAbsorptionId = Shader.PropertyToID("_SSSAbsorption");
        private static readonly int SSSColorId = Shader.PropertyToID("_SSSColor");
        private static readonly int HueShiftId = Shader.PropertyToID("_HueShift");
        private static readonly int EdgeBrowningWidthId = Shader.PropertyToID("_EdgeBrowningWidth");
        private static readonly int BrowningColorId = Shader.PropertyToID("_BrowningColor");
        private static readonly int VeinTexId = Shader.PropertyToID("_VeinTex");
        private static readonly int VeinTexStrengthId = Shader.PropertyToID("_VeinTexStrength");

        /// <summary>
        /// Bakes a single leaf variant into a <see cref="Texture2D"/> with
        /// premultiplied alpha. Each variant gets unique Gielis jitter, hue
        /// shift, and venation from the seed.
        /// </summary>
        internal static Texture2D BakeLeaf(BushLeafBakeSettings settings, int variantIndex, uint seed)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[BushLeafBaker] Shader '{ShaderName}' not found.");
                return null;
            }

            var hash = HashFromSeed(seed, variantIndex);

            var rt = RenderTexture.GetTemporary(settings.Resolution, settings.Resolution, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;

            var material = new Material(shader);
            ConfigureMaterial(material, settings, hash);

            var veinTex = BakeVeinTexture(settings, seed);
            if (veinTex != null)
            {
                material.SetTexture(VeinTexId, veinTex);
                material.SetFloat(VeinTexStrengthId, settings.VeinTexStrength);
            }

            var cameraGo = CreateBakeCamera(settings.LeafRadius, rt);
            var quadGo = CreateBakeQuad(material, settings.LeafRadius);

            cameraGo.GetComponent<Camera>().Render();

            var result = ReadbackTexture(rt, settings.Resolution);

            Object.DestroyImmediate(quadGo);
            Object.DestroyImmediate(cameraGo);
            Object.DestroyImmediate(material);
            RenderTexture.ReleaseTemporary(rt);

            if (veinTex != null)
            {
                Object.DestroyImmediate(veinTex);
            }

            return result;
        }

        private static void ConfigureMaterial(
            Material material, BushLeafBakeSettings settings, float hash)
        {
            material.SetFloat(LeafRadiusId, settings.LeafRadius);
            material.SetFloat(AAWidthId, 0.008f);

            material.SetFloat(GielisMId, settings.GielisM + (hash - 0.5f) * 0.6f);
            material.SetFloat(GielisN1Id, settings.GielisN1 + (hash * 7.13f % 1f) * 0.3f - 0.15f);
            material.SetFloat(GielisN2Id, settings.GielisN2 + (hash * 13.37f % 1f) * 0.2f - 0.1f);
            material.SetFloat(GielisN3Id, settings.GielisN3 + (hash * 23.71f % 1f) * 0.2f - 0.1f);

            material.SetColor(BaseColorId, settings.BaseColor);
            material.SetFloat(EdgeShadeId, settings.EdgeShade);
            material.SetColor(HighlightColorId, settings.HighlightColor);
            material.SetFloat(HighlightSizeId, settings.HighlightSize);
            material.SetFloat(HighlightOffsetId, settings.HighlightOffset);

            material.SetFloat(VeinWidthId, settings.VeinWidth);
            material.SetFloat(VeinDarkenId, settings.VeinDarken);

            material.SetFloat(SSSStrengthId, settings.SSSStrength);
            material.SetFloat(SSSAbsorptionId, settings.SSSAbsorption);
            material.SetColor(SSSColorId, settings.SSSColor);

            material.SetFloat(HueShiftId, (hash - 0.5f) * 2f * settings.HueJitter / 360f);
            material.SetFloat(EdgeBrowningWidthId, settings.EdgeBrowningWidth);
            material.SetColor(BrowningColorId, settings.BrowningColor);
        }


        private static Texture2D BakeVeinTexture(BushLeafBakeSettings settings, uint seed)
        {
            if (settings.VeinSources <= 0)
            {
                return null;
            }

            var veinSettings = new LeafVenationSimulator.SimulationSettings
            {
                LeafSize = new Vector2(settings.LeafRadius * 2.4f, settings.LeafRadius * 2.4f),
                KillDistance = 0.03f,
                AttractionDistance = 0.1f,
                SourceCount = settings.VeinSources,
                MaxIterations = 120,
                StepSize = 0.015f,
                Seed = seed
            };

            var segments = LeafVenationSimulator.Simulate(veinSettings);
            return LeafVenationSimulator.RasteriseVeins(segments, settings.Resolution, veinSettings.LeafSize);
        }

        private static GameObject CreateBakeCamera(float leafRadius, RenderTexture rt)
        {
            var go = new GameObject("_BakeCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = leafRadius * 1.3f;
            cam.nearClipPlane = -1f;
            cam.farClipPlane = 1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.targetTexture = rt;
            cam.cullingMask = 1 << BakeLayer;
            cam.enabled = false;

            go.transform.position = new Vector3(0f, 0f, -0.5f);

            return go;
        }

        private static GameObject CreateBakeQuad(Material material, float leafRadius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "_BakeQuad";
            go.hideFlags = HideFlags.HideAndDontSave;
            go.layer = BakeLayer;

            // Quad needs to be large enough to encompass the leaf + AA border
            var scale = leafRadius * 2.8f;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.transform.position = Vector3.zero;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Remove the collider — not needed for baking
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            return go;
        }

        private static Texture2D ReadbackTexture(RenderTexture rt, int resolution)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;

            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            tex.Apply();

            RenderTexture.active = previous;
            return tex;
        }

        private static float HashFromSeed(uint seed, int variantIndex)
        {
            var combined = seed * 7919u + (uint)variantIndex * 4637u + 31u;
            return (combined % 10007u) / 10007f;
        }
    }
}

