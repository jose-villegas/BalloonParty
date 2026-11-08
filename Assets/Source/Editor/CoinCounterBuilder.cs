using BalloonParty.UI.Coins;
using BalloonParty.UI.Score;
using BalloonParty.UI.Shields;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Builds the coin counter (<c>UI/Coins/README.md</c>) and puts it in the game canvas, bottom left. Its
    ///     backing and number are copies of the shield counter's, so the HUD counters match; the icon is the
    ///     coin. Running it again rebuilds the counter the same way and replaces the one in the canvas.
    /// </summary>
    internal static class CoinCounterBuilder
    {
        private const string MenuPath = "Tools/BalloonParty/Coins/Build Counter";
        private const string PrefabFolder = "Assets/Prefabs/Coins/UI";
        private const string PrefabPath = PrefabFolder + "/CoinsCounter.prefab";
        private const string HostPrefabPath = "Assets/Prefabs/Game/GameCanvas.prefab";
        private const string TemplatePath = "Assets/Prefabs/Shield/UI/ShieldsCounter.prefab";
        private const string TrailPath = "Assets/Prefabs/Coins/CoinTrail.prefab";
        private const string CoinSpritePath = "Assets/Sprites/UI/Coins/coin.png";

        // The HUD icons' shine sweep (BalloonParty/Sprite/ShinyDefault), as the shield's, offset in time so the
        // two don't flash together.
        private const string CoinMaterialPath = "Assets/Materials/UI/CoinUI.mat";
        private const string RootName = "Coins";
        private const string DrawAfterName = "GameOver";
        private const string ParentScopeType = "BalloonParty.Game.GameLifetimeScope";

        // Canvas units, on the 225x400 reference resolution: the icon's box, as the shield counter's, and its
        // distance from the screen's bottom-left corner.
        private const float IconSize = 15f;
        private const float Margin = 8f;

        // The shield counter shows one centred digit; a balance runs to three or four, so the number starts
        // just right of the coin and reads left to right, and the backing grows to the right to hold it.
        private const float LabelGap = 2f;
        private const float LabelWidth = 30f;
        private const float BackingExtraWidth = 8f;

        [MenuItem(MenuPath)]
        private static void BuildFromMenu()
        {
            var error = Build();
            if (error != null)
            {
                Debug.LogError($"Coin counter: {error}");
                return;
            }

            Debug.Log($"Coin counter: built {PrefabPath} and placed it in {HostPrefabPath}.");
        }

        /// <summary>Builds the prefab and places it in the game canvas; returns an error, or null on success.</summary>
        internal static string Build()
        {
            var trail = AssetDatabase.LoadAssetAtPath<FlyingTrail>(TrailPath);
            var coin = AssetDatabase.LoadAssetAtPath<Sprite>(CoinSpritePath);
            var shine = AssetDatabase.LoadAssetAtPath<Material>(CoinMaterialPath);
            if (trail == null || coin == null || shine == null)
            {
                return $"{TrailPath}, {CoinSpritePath} or {CoinMaterialPath} wasn't found.";
            }

            var error = BuildPrefab(trail, coin, shine);
            return error ?? PlaceInHost();
        }

        private static string BuildPrefab(FlyingTrail trail, Sprite coin, Material shine)
        {
            var template = PrefabUtility.LoadPrefabContents(TemplatePath);
            var root = new GameObject(RootName, typeof(RectTransform));
            try
            {
                var background = template.transform.Find("Background");
                var text = template.GetComponentInChildren<ShieldCounterLabel>(true);
                if (background == null || text == null)
                {
                    return $"{TemplatePath} has no Background or no ShieldCounterLabel to copy.";
                }

                var rect = (RectTransform)root.transform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(Margin, Margin);
                rect.sizeDelta = new Vector2(IconSize, IconSize);

                // The view hides the counter until coins mean something; it never takes a tap.
                var group = root.AddComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;

                Backing(Object.Instantiate(background.gameObject, root.transform));
                var icon = Icon(root.transform, coin, shine);
                Label(Object.Instantiate(text.gameObject, root.transform));

                var view = new SerializedObject(root.AddComponent<CoinCounterView>());
                view.FindProperty("_trailPrefab").objectReferenceValue = trail;
                view.FindProperty("_icon").objectReferenceValue = icon;
                view.ApplyModifiedPropertiesWithoutUndo();

                var scope = new SerializedObject(root.AddComponent<CoinUILifetimeScope>());
                scope.FindProperty("parentReference.TypeName").stringValue = ParentScopeType;
                scope.ApplyModifiedPropertiesWithoutUndo();

                EnsureFolder(PrefabFolder);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return null;
            }
            finally
            {
                Object.DestroyImmediate(root);
                PrefabUtility.UnloadPrefabContents(template);
            }
        }

        private static RectTransform Icon(Transform parent, Sprite coin, Material shine)
        {
            var node = new GameObject("Coin", typeof(RectTransform));
            var rect = (RectTransform)node.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = node.AddComponent<Image>();
            image.sprite = coin;
            image.material = shine;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rect;
        }

        // The copied number keeps its font, size and rolling digits; only what it counts changes.
        private static void Backing(GameObject background)
        {
            background.name = "Background";
            var rect = (RectTransform)background.transform;
            rect.sizeDelta += new Vector2(BackingExtraWidth, 0f);
            rect.anchoredPosition += new Vector2(BackingExtraWidth / 2f, 0f);
        }

        private static void Label(GameObject text)
        {
            text.name = "Text";
            Object.DestroyImmediate(text.GetComponent<ShieldCounterLabel>());
            text.AddComponent<CoinCounterLabel>();

            var rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(LabelGap, rect.anchoredPosition.y);
            rect.sizeDelta = new Vector2(LabelWidth, 0f);
            text.GetComponent<TMP_Text>().horizontalAlignment = HorizontalAlignmentOptions.Left;
        }

        private static string PlaceInHost()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var host = PrefabUtility.LoadPrefabContents(HostPrefabPath);
            try
            {
                var old = host.transform.Find(RootName);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, host.transform);
                instance.name = RootName;

                // Kept beside the popups, after the game-over screen, so it reads as part of the HUD around it.
                var after = host.transform.Find(DrawAfterName);
                if (after != null)
                {
                    instance.transform.SetSiblingIndex(after.GetSiblingIndex() + 1);
                }

                PrefabUtility.SaveAsPrefabAsset(host, HostPrefabPath);
                return null;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(host);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
