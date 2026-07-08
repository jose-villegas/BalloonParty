#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using System.Linq;
using BalloonParty.Shared.Pause;
using UnityEngine;
using VContainer;

namespace BalloonParty.Cheats
{
    public class CheatConsoleView : MonoBehaviour
    {
        private const float HandleHeight = 14f;
        private const float MinHeight = 80f;
        private const float ReferenceHeight = 720f;

        [Inject] private IEnumerable<ICheat> _cheats;
        [Inject] private PauseService _pauseService;

        private readonly HashSet<string> _favorites = new();

        private string _activeTag = string.Empty;
        private float _consoleHeight = 280f;
        private bool _resizing;
        private Vector2 _scroll;
        private string _search = string.Empty;

        private bool _visible;
        private bool _throwerHeld;
        private int _lastTouchCount;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote) || ThreeFingerTapped())
            {
                _visible = !_visible;
            }

            SyncThrowerHold();

            if (!_visible)
            {
                return;
            }

            var scale = Screen.height / ReferenceHeight;
            var handleTopInScreenSpace = Screen.height - ((_consoleHeight + HandleHeight) * scale);
            var handleBottomInScreenSpace = Screen.height - (_consoleHeight * scale);

            // Input.mousePosition is bottom-origin; convert to top-left.
            var mouseY = Screen.height - Input.mousePosition.y;

            if (Input.GetMouseButtonDown(0) && mouseY >= handleTopInScreenSpace && mouseY <= handleBottomInScreenSpace)
            {
                _resizing = true;
            }

            if (Input.GetMouseButtonUp(0))
            {
                _resizing = false;
            }

            if (_resizing && Input.GetMouseButton(0))
            {
                // Converts mouseY into reference height space.
                var referenceMouseY = mouseY / scale;
                _consoleHeight = Mathf.Clamp(ReferenceHeight - referenceMouseY, MinHeight, ReferenceHeight * 0.9f);
            }
        }

        private void OnDisable()
        {
            if (_throwerHeld && _pauseService != null)
            {
                _pauseService.Resume(PauseSource.Cheat);
                _throwerHeld = false;
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            var scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var sw = Screen.width / scale;
            var sh = ReferenceHeight;

            var handleRect = new Rect(0, sh - _consoleHeight - HandleHeight, sw, HandleHeight);
            var bodyRect = new Rect(0, sh - _consoleHeight, sw, _consoleHeight);

            var guiMouseY = Event.current.mousePosition.y / scale;
            var overHandle = guiMouseY >= handleRect.y && guiMouseY <= handleRect.yMax;
            GUI.Box(handleRect, overHandle || _resizing ? "↕" : "—");
            GUI.Box(bodyRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(bodyRect.x + 6, bodyRect.y + 6, bodyRect.width - 12, bodyRect.height - 12));
            DrawContent();
            GUILayout.EndArea();
        }

        // Keyboard-less toggle for touch: fires once on the rising edge to 3+ simultaneous touches
        // (desktop stays at touchCount 0). Backtick still toggles in the editor.
        private bool ThreeFingerTapped()
        {
            var touches = Input.touchCount;
            var tapped = touches >= 3 && _lastTouchCount < 3;
            _lastTouchCount = touches;
            return tapped;
        }

        // Holds the thrower (via PauseService) exactly while the console is open; released on close or
        // teardown. Reference-counted, so paired Pause/Resume is required.
        private void SyncThrowerHold()
        {
            if (_visible == _throwerHeld || _pauseService == null)
            {
                return;
            }

            if (_visible)
            {
                _pauseService.Pause(PauseSource.Cheat);
            }
            else
            {
                _pauseService.Resume(PauseSource.Cheat);
            }

            _throwerHeld = _visible;
        }

        private List<ICheat> ApplyFilters(IReadOnlyList<ICheat> cheats)
        {
            var result = cheats.AsEnumerable();

            if (!string.IsNullOrEmpty(_search))
            {
                result = result.Where(c => c.Name.ToLower().Contains(_search.ToLower()));
            }

            if (!string.IsNullOrEmpty(_activeTag))
            {
                result = result.Where(c => c.Tags.Contains(_activeTag));
            }

            return result.ToList();
        }

        private void DrawCheatRow(ICheat cheat)
        {
            // Interactive cheats render their own controls below a plain name+star header row.
            if (cheat is ICheatControls controls)
            {
                GUILayout.BeginHorizontal();
                DrawFavoriteToggle(cheat);
                GUILayout.Label(cheat.Name);
                GUILayout.EndHorizontal();
                controls.DrawControls();
                return;
            }

            GUILayout.BeginHorizontal();
            DrawFavoriteToggle(cheat);
            if (GUILayout.Button(cheat.Name))
            {
                cheat.Execute();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawFavoriteToggle(ICheat cheat)
        {
            var isFavorite = _favorites.Contains(cheat.Name);
            if (!GUILayout.Button(isFavorite ? "★" : "☆", GUILayout.Width(28)))
            {
                return;
            }

            if (isFavorite)
            {
                _favorites.Remove(cheat.Name);
            }
            else
            {
                _favorites.Add(cheat.Name);
            }
        }

        private void DrawContent()
        {
            var cheats = _cheats.ToList();

            DrawSearchBar();
            DrawTagFilters(cheats);

            var filtered = ApplyFilters(cheats);
            var favorites = filtered.Where(c => _favorites.Contains(c.Name)).ToList();
            var rest = filtered.Where(c => !_favorites.Contains(c.Name)).ToList();

            _scroll = GUILayout.BeginScrollView(_scroll);

            if (favorites.Count > 0)
            {
                DrawSection("★ Favorites", favorites);
            }

            foreach (var section in rest.GroupBy(c => c.Section).OrderBy(g => g.Key))
            {
                DrawSection(section.Key, section.ToList());
            }

            GUILayout.EndScrollView();
        }

        private void DrawSearchBar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(55));
            _search = GUILayout.TextField(_search);
            if (GUILayout.Button("✕", GUILayout.Width(28)))
            {
                _search = string.Empty;
            }

            GUILayout.EndHorizontal();
        }

        private void DrawSection(string title, IReadOnlyList<ICheat> cheats)
        {
            GUILayout.Label($"— {title} —");
            foreach (var cheat in cheats)
            {
                DrawCheatRow(cheat);
            }
        }

        private void DrawTagFilters(IReadOnlyList<ICheat> cheats)
        {
            var allTags = cheats.SelectMany(c => c.Tags).Distinct().OrderBy(t => t).ToList();
            if (allTags.Count == 0)
            {
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Tags:", GUILayout.Width(40));

            if (GUILayout.Toggle(_activeTag == string.Empty, "All", GUI.skin.button, GUILayout.Width(36)))
            {
                _activeTag = string.Empty;
            }

            foreach (var tagLabel in allTags)
            {
                var active = _activeTag == tagLabel;
                if (GUILayout.Toggle(active, tagLabel, GUI.skin.button) != active)
                {
                    _activeTag = active ? string.Empty : tagLabel;
                }
            }

            GUILayout.EndHorizontal();
        }
    }
}
#endif
