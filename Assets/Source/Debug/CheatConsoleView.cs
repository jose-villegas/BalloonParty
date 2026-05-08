#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Debug
{
    public class CheatConsoleView : MonoBehaviour, IInitializable
    {
        [Inject] private IEnumerable<ICheat> _cheats;

        public void Initialize() { }

        private bool _visible;
        private string _search = string.Empty;
        private string _activeTag = string.Empty;
        private Vector2 _scroll;
        private readonly HashSet<string> _favorites = new HashSet<string>();

        private float _consoleHeight = 280f;
        private const float MinHeight = 80f;
        private const float HandleHeight = 10f;
        private const float ReferenceHeight = 720f;
        private bool _resizing;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
                _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible) return;

            var scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            // all coords are now in reference-resolution space
            var sw = Screen.width / scale;
            var sh = ReferenceHeight;

            var handleRect = new Rect(0, sh - _consoleHeight - HandleHeight, sw, HandleHeight);
            var bodyRect   = new Rect(0, sh - _consoleHeight, sw, _consoleHeight);

            HandleResize(handleRect, sh, scale);

            GUI.Box(handleRect, "▲ ─────────────────── drag to resize ─────────────────── ▲");
            GUI.Box(bodyRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(bodyRect.x + 6, bodyRect.y + 6, bodyRect.width - 12, bodyRect.height - 12));
            DrawContent();
            GUILayout.EndArea();
        }

        private void HandleResize(Rect handleRect, float scaledScreenHeight, float scale)
        {
            var e = Event.current;
            // Event.current.mousePosition is in raw screen pixels; convert to reference space.
            var mousePos = e.mousePosition / scale;

            if (e.type == EventType.MouseDown && handleRect.Contains(mousePos))
            {
                _resizing = true;
                e.Use();
            }

            if (_resizing)
            {
                if (e.type == EventType.MouseDrag)
                {
                    _consoleHeight = Mathf.Clamp(scaledScreenHeight - mousePos.y, MinHeight, scaledScreenHeight * 0.9f);
                    e.Use();
                }

                if (e.type == EventType.MouseUp)
                {
                    _resizing = false;
                    e.Use();
                }
            }
        }

        private void DrawContent()
        {
            var cheats = _cheats.ToList();

            DrawSearchBar();
            DrawTagFilters(cheats);

            var filtered  = ApplyFilters(cheats);
            var favorites = filtered.Where(c => _favorites.Contains(c.Name)).ToList();
            var rest      = filtered.Where(c => !_favorites.Contains(c.Name)).ToList();

            _scroll = GUILayout.BeginScrollView(_scroll);

            if (favorites.Count > 0)
                DrawSection("★ Favorites", favorites);

            foreach (var section in rest.GroupBy(c => c.Section).OrderBy(g => g.Key))
                DrawSection(section.Key, section.ToList());

            GUILayout.EndScrollView();
        }

        private void DrawSearchBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(55));
            _search = GUILayout.TextField(_search);
            if (GUILayout.Button("✕", GUILayout.Width(28)))
                _search = string.Empty;
            GUILayout.EndHorizontal();
        }

        private void DrawTagFilters(List<ICheat> cheats)
        {
            var allTags = cheats.SelectMany(c => c.Tags).Distinct().OrderBy(t => t).ToList();
            if (allTags.Count == 0) return;

            GUILayout.BeginHorizontal();
            GUILayout.Label("Tags:", GUILayout.Width(40));

            if (GUILayout.Toggle(_activeTag == string.Empty, "All", GUI.skin.button, GUILayout.Width(36)))
                _activeTag = string.Empty;

            foreach (var tagLabel in allTags)
            {
                var active = _activeTag == tagLabel;
                if (GUILayout.Toggle(active, tagLabel, GUI.skin.button) != active)
                    _activeTag = active ? string.Empty : tagLabel;
            }

            GUILayout.EndHorizontal();
        }

        private void DrawSection(string title, List<ICheat> cheats)
        {
            GUILayout.Label($"— {title} —");
            foreach (var cheat in cheats)
                DrawCheatRow(cheat);
        }

        private void DrawCheatRow(ICheat cheat)
        {
            GUILayout.BeginHorizontal();

            var isFavorite = _favorites.Contains(cheat.Name);
            if (GUILayout.Button(isFavorite ? "★" : "☆", GUILayout.Width(28)))
            {
                if (isFavorite) _favorites.Remove(cheat.Name);
                else _favorites.Add(cheat.Name);
            }

            if (GUILayout.Button(cheat.Name))
                cheat.Execute();

            GUILayout.EndHorizontal();
        }

        private List<ICheat> ApplyFilters(List<ICheat> cheats)
        {
            var result = cheats.AsEnumerable();

            if (!string.IsNullOrEmpty(_search))
                result = result.Where(c => c.Name.ToLower().Contains(_search.ToLower()));

            if (!string.IsNullOrEmpty(_activeTag))
                result = result.Where(c => c.Tags.Contains(_activeTag));

            return result.ToList();
        }
    }
}
#endif
