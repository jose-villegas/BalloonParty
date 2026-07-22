using System.Collections.Generic;
using BalloonParty.Shared.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.Editor.ShadowBake
{
    /// <summary>Inspector for <see cref="ImageShadowBaker"/>: bakes a blurred silhouette sprite and wires it into a shadow Image child.</summary>
    [CustomEditor(typeof(ImageShadowBaker))]
    internal sealed class ImageShadowBakerEditor : UnityEditor.Editor
    {
        private const string ShadowChildName = "BakedShadow";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            if (GUILayout.Button("Bake"))
            {
                Bake((ImageShadowBaker)target);
            }
        }

        private static void Bake(ImageShadowBaker clicked)
        {
            var assetPath = ShadowBakeUtility.ResolvePrefabPath(clicked.gameObject);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError("[ImageShadowBaker] Not part of a prefab — bake edits the prefab asset.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                var baker = contents.GetComponentInChildren<ImageShadowBaker>(true);
                var images = CollectSourceImages(baker);
                if (images.Count == 0)
                {
                    Debug.LogError($"[ImageShadowBaker] {assetPath}: no child Images with sprites to bake.");
                    return;
                }

                var sprite = BakeShadowSprite(baker, images, assetPath);
                if (sprite == null)
                {
                    return;
                }

                WireShadowChild(baker, sprite);

                PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                EditorGUIUtility.PingObject(sprite);
                Debug.Log($"[ImageShadowBaker] Baked shadow for {assetPath} → {AssetDatabase.GetAssetPath(sprite)}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static List<Image> CollectSourceImages(ImageShadowBaker baker)
        {
            var result = new List<Image>();
            foreach (var image in baker.GetComponentsInChildren<Image>(false))
            {
                if (image.enabled && image.sprite != null && image != baker.ShadowChild)
                {
                    result.Add(image);
                }
            }

            return result;
        }

        private static Sprite BakeShadowSprite(
            ImageShadowBaker baker, IReadOnlyList<Image> images, string prefabPath)
        {
            var bakerRect = baker.GetComponent<RectTransform>();
            var boundsRect = ComputeLocalBounds(images, bakerRect);

            var pixelsPerUnit = baker.ResolutionMultiplier;
            var blurPixels = Mathf.CeilToInt(baker.BlurRadius * pixelsPerUnit);
            var padPixels = blurPixels * baker.BlurPasses + 2;

            var width = Mathf.CeilToInt(boundsRect.width * pixelsPerUnit) + 2 * padPixels;
            var height = Mathf.CeilToInt(boundsRect.height * pixelsPerUnit) + 2 * padPixels;
            var padUnits = (float)padPixels / pixelsPerUnit;

            var alpha = RenderSilhouetteAlpha(images, bakerRect, boundsRect, padUnits, width, height);
            for (var pass = 0; pass < baker.BlurPasses; pass++)
            {
                ShadowBakeUtility.BoxBlur(alpha, width, height, blurPixels);
            }

            var pngPath = ShadowBakeUtility.OutputPathFor(prefabPath);

            // Pivot at the baker's local origin within the texture.
            var pivot = new Vector2(
                (0f - (boundsRect.xMin - padUnits)) * pixelsPerUnit / width,
                (0f - (boundsRect.yMin - padUnits)) * pixelsPerUnit / height);

            ShadowBakeUtility.WriteShadowPng(pngPath, alpha, width, height, baker.ShadowColor);
            ShadowBakeUtility.ImportAsSprite(pngPath, pixelsPerUnit, pivot);
            return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        }

        private static Rect ComputeLocalBounds(IReadOnlyList<Image> images, RectTransform bakerRect)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            var corners = new Vector3[4];

            foreach (var image in images)
            {
                image.rectTransform.GetWorldCorners(corners);

                for (var i = 0; i < 4; i++)
                {
                    var local = bakerRect.InverseTransformPoint(corners[i]);
                    min = Vector2.Min(min, local);
                    max = Vector2.Max(max, local);
                }
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static float[] RenderSilhouetteAlpha(
            IReadOnlyList<Image> images, RectTransform bakerRect, Rect boundsRect,
            float padUnits, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var plainShader = Shader.Find("Sprites/Default");
            var corners = new Vector3[4];

            // Ortho projection covering the padded bounds in baker-local space.
            var left = boundsRect.xMin - padUnits;
            var right = boundsRect.xMax + padUnits;
            var bottom = boundsRect.yMin - padUnits;
            var top = boundsRect.yMax + padUnits;
            var projection = Matrix4x4.Ortho(left, right, bottom, top, -100f, 100f);

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, width, 0, height);
            GL.PopMatrix();
            GL.PushMatrix();
            GL.LoadProjectionMatrix(GL.GetGPUProjectionMatrix(projection, true));
            GL.modelview = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));

            var materials = new Dictionary<Texture, Material>();

            foreach (var image in images)
            {
                var sprite = image.sprite;
                var texture = sprite.texture;
                if (!materials.TryGetValue(texture, out var material))
                {
                    material = new Material(plainShader) { mainTexture = texture };
                    materials[texture] = material;
                }

                material.SetPass(0);

                image.rectTransform.GetWorldCorners(corners);

                // Convert corners to baker-local space for the ortho projection.
                for (var i = 0; i < 4; i++)
                {
                    corners[i] = bakerRect.InverseTransformPoint(corners[i]);
                }

                // Sprite UV rect within the atlas texture.
                var texRect = sprite.textureRect;
                var uvMin = new Vector2(texRect.xMin / texture.width, texRect.yMin / texture.height);
                var uvMax = new Vector2(texRect.xMax / texture.width, texRect.yMax / texture.height);

                // Corners: 0=BL, 1=TL, 2=TR, 3=BR
                GL.Begin(GL.QUADS);
                GL.TexCoord2(uvMin.x, uvMin.y);
                GL.Vertex3(corners[0].x, corners[0].y, 0f);
                GL.TexCoord2(uvMin.x, uvMax.y);
                GL.Vertex3(corners[1].x, corners[1].y, 0f);
                GL.TexCoord2(uvMax.x, uvMax.y);
                GL.Vertex3(corners[2].x, corners[2].y, 0f);
                GL.TexCoord2(uvMax.x, uvMin.y);
                GL.Vertex3(corners[3].x, corners[3].y, 0f);
                GL.End();
            }

            GL.PopMatrix();

            var alpha = ShadowBakeUtility.ReadbackAlpha(rt, width, height);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            foreach (var material in materials.Values)
            {
                Object.DestroyImmediate(material);
            }

            return alpha;
        }

        private static void WireShadowChild(ImageShadowBaker baker, Sprite sprite)
        {
            var shadow = baker.ShadowChild;
            if (shadow == null)
            {
                var go = new GameObject(ShadowChildName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(baker.transform, false);

                // Place behind all siblings so it renders below the source images.
                go.transform.SetAsFirstSibling();

                shadow = go.GetComponent<Image>();
                shadow.raycastTarget = false;
                baker.ShadowChild = shadow;
            }

            shadow.sprite = sprite;
            shadow.SetNativeSize();

            var shadowRect = (RectTransform)shadow.transform;
            shadowRect.anchoredPosition = baker.ShadowOffset;
        }
    }
}
