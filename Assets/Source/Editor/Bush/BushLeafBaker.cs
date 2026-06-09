using BalloonParty.Shared.Rendering;
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
        private static readonly int HueShiftId = Shader.PropertyToID("_HueShift");
        private static readonly int MidribEnabledId = Shader.PropertyToID("_MidribEnabled");
        private static readonly int MidribWidthId = Shader.PropertyToID("_MidribWidth");
        private static readonly int MidribGradientId = Shader.PropertyToID("_MidribGradient");
        private static readonly int LateralCountId = Shader.PropertyToID("_LateralCount");
        private static readonly int LateralAngleMinId = Shader.PropertyToID("_LateralAngleMin");
        private static readonly int LateralAngleMaxId = Shader.PropertyToID("_LateralAngleMax");
        private static readonly int LateralWidthId = Shader.PropertyToID("_LateralWidth");
        private static readonly int LateralStartId = Shader.PropertyToID("_LateralStart");
        private static readonly int LateralLengthMinId = Shader.PropertyToID("_LateralLengthMin");
        private static readonly int LateralLengthMaxId = Shader.PropertyToID("_LateralLengthMax");
        private static readonly int LateralSubCountId = Shader.PropertyToID("_LateralSubCount");
        private static readonly int LateralSubChanceId = Shader.PropertyToID("_LateralSubChance");
        private static readonly int LateralSubLengthMinId = Shader.PropertyToID("_LateralSubLengthMin");
        private static readonly int LateralSubLengthMaxId = Shader.PropertyToID("_LateralSubLengthMax");
        private static readonly int LateralCurvatureId = Shader.PropertyToID("_LateralCurvature");
        private static readonly int SubVeinCurvatureId = Shader.PropertyToID("_SubVeinCurvature");
        private static readonly int VeinSeedId = Shader.PropertyToID("_VeinSeed");
        private static readonly int ReticulateEnabledId = Shader.PropertyToID("_ReticulateEnabled");
        private static readonly int ReticulateDensityId = Shader.PropertyToID("_ReticulateDensity");
        private static readonly int ReticulateWidthId = Shader.PropertyToID("_ReticulateWidth");
        private static readonly int ReticulateOpacityId = Shader.PropertyToID("_ReticulateOpacity");
        private static readonly int ReticulateAngleId = Shader.PropertyToID("_ReticulateAngle");
        private static readonly int PetioleEnabledId = Shader.PropertyToID("_PetioleEnabled");
        private static readonly int PetioleLengthId = Shader.PropertyToID("_PetioleLength");
        private static readonly int PetioleWidthId = Shader.PropertyToID("_PetioleWidth");
        private static readonly int PetioleTaperId = Shader.PropertyToID("_PetioleTaper");

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

            Texture2D gradientTex = null;
            if (settings.MidribEnabled)
            {
                gradientTex = GradientTextureHelper.Bake(settings.MidribGradient);
                material.SetTexture(MidribGradientId, gradientTex);
            }

            var viewRadius = settings.LeafRadius * 1.3f;
            if (settings.PetioleEnabled)
            {
                viewRadius = Mathf.Max(viewRadius, settings.LeafRadius + settings.PetioleLength + 0.02f);
            }

            var cameraGo = CreateBakeCamera(viewRadius, rt);
            var quadGo = CreateBakeQuad(material, viewRadius);

            cameraGo.GetComponent<Camera>().Render();

            var result = ReadbackTexture(rt, settings.Resolution);

            Object.DestroyImmediate(quadGo);
            Object.DestroyImmediate(cameraGo);
            if (gradientTex != null)
            {
                Object.DestroyImmediate(gradientTex);
            }

            Object.DestroyImmediate(material);
            RenderTexture.ReleaseTemporary(rt);

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

            material.SetFloat(HueShiftId, (hash - 0.5f) * 2f * settings.HueJitter * Mathf.Deg2Rad);

            material.SetFloat(MidribEnabledId, settings.MidribEnabled ? 1f : 0f);
            material.SetFloat(MidribWidthId, settings.MidribWidth);

            material.SetFloat(LateralCountId, settings.LateralCount);
            material.SetFloat(LateralAngleMinId, settings.LateralAngle.x * Mathf.Deg2Rad);
            material.SetFloat(LateralAngleMaxId, settings.LateralAngle.y * Mathf.Deg2Rad);
            material.SetFloat(LateralWidthId, settings.MidribWidth * settings.LateralWidthRatio);
            material.SetFloat(LateralStartId, settings.LateralStart);
            material.SetFloat(LateralLengthMinId, settings.LateralLength.x);
            material.SetFloat(LateralLengthMaxId, settings.LateralLength.y);
            material.SetFloat(LateralSubCountId, settings.LateralSubCount);
            material.SetFloat(LateralSubChanceId, settings.LateralSubChance);
            material.SetFloat(LateralSubLengthMinId, settings.LateralSubLength.x);
            material.SetFloat(LateralSubLengthMaxId, settings.LateralSubLength.y);
            material.SetFloat(LateralCurvatureId, settings.LateralCurvature);
            material.SetFloat(SubVeinCurvatureId, settings.SubVeinCurvature);
            material.SetFloat(VeinSeedId, hash * 1000f);

            material.SetFloat(ReticulateEnabledId, settings.ReticulateEnabled ? 1f : 0f);
            material.SetFloat(ReticulateDensityId, settings.ReticulateDensity);
            material.SetFloat(ReticulateWidthId, settings.ReticulateWidth);
            material.SetFloat(ReticulateOpacityId, settings.ReticulateOpacity);
            material.SetFloat(ReticulateAngleId, settings.ReticulateAngle * Mathf.Deg2Rad);

            material.SetFloat(PetioleEnabledId, settings.PetioleEnabled ? 1f : 0f);
            material.SetFloat(PetioleLengthId, settings.PetioleLength);
            material.SetFloat(PetioleWidthId, settings.PetioleWidth);
            material.SetFloat(PetioleTaperId, settings.PetioleTaper);
        }

        private static GameObject CreateBakeCamera(float viewRadius, RenderTexture rt)
        {
            var go = new GameObject("_BakeCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = viewRadius;
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

        private static GameObject CreateBakeQuad(Material material, float viewRadius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "_BakeQuad";
            go.hideFlags = HideFlags.HideAndDontSave;
            go.layer = BakeLayer;

            var scale = viewRadius * 2.2f;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.transform.position = Vector3.zero;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

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
