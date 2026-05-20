using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    internal sealed class ScriptSearchPopup : EditorWindow
    {
        private const int MaxVisible = 12;
        private const float RowHeight = 20f;
        private const float SearchBarHeight = 22f;
        private const float Padding = 4f;

        private readonly List<MonoScript> _filtered = new();

        private string _search = "";
        private Vector2 _scroll;
        private Action<MonoScript> _onSelected;
        private MonoScript[] _allScripts;
        private int _focusIndex;
        private bool _focusSearch = true;

        internal static void Show(Rect activatorRect, Action<MonoScript> onSelected)
        {
            var window = CreateInstance<ScriptSearchPopup>();
            window._onSelected = onSelected;
            window._allScripts = Resources.FindObjectsOfTypeAll<MonoScript>()
                .Where(s => s.GetClass() != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(s)))
                .OrderBy(s => s.GetClass().Name)
                .ToArray();
            window.RebuildFiltered();

            var height = SearchBarHeight + (Padding * 3) +
                         (RowHeight * Mathf.Min(MaxVisible, window._filtered.Count + 1));
            var size = new Vector2(activatorRect.width, height);
            window.ShowAsDropDown(activatorRect, size);
        }

        private void OnGUI()
        {
            HandleKeyboard();

            if (_focusSearch)
            {
                EditorGUI.FocusTextInControl("ScriptSearch");
                _focusSearch = false;
            }

            EditorGUILayout.Space(Padding);

            EditorGUI.BeginChangeCheck();
            GUI.SetNextControlName("ScriptSearch");
            _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
            {
                _focusIndex = 0;
                RebuildFiltered();
            }

            EditorGUILayout.Space(Padding);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (var i = 0; i < _filtered.Count; i++)
            {
                var script = _filtered[i];
                var type = script.GetClass();
                var label = $"{type.Name}  ({type.Namespace})";

                var rect = EditorGUILayout.GetControlRect(false, RowHeight);

                if (i == _focusIndex)
                {
                    EditorGUI.DrawRect(rect, new Color(0.24f, 0.49f, 0.91f, 0.5f));
                }

                if (GUI.Button(rect, label, EditorStyles.label))
                {
                    SelectAndClose(script);
                    return;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void HandleKeyboard()
        {
            if (Event.current.type != EventType.KeyDown)
            {
                return;
            }

            switch (Event.current.keyCode)
            {
                case KeyCode.DownArrow:
                    _focusIndex = Mathf.Min(_focusIndex + 1, _filtered.Count - 1);
                    ScrollToFocused();
                    Event.current.Use();
                    break;
                case KeyCode.UpArrow:
                    _focusIndex = Mathf.Max(_focusIndex - 1, 0);
                    ScrollToFocused();
                    Event.current.Use();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_focusIndex >= 0 && _focusIndex < _filtered.Count)
                    {
                        SelectAndClose(_filtered[_focusIndex]);
                    }

                    Event.current.Use();
                    break;
                case KeyCode.Escape:
                    Close();
                    Event.current.Use();
                    break;
            }
        }

        private void ScrollToFocused()
        {
            _scroll.y = Mathf.Max(0, (_focusIndex * RowHeight) - (RowHeight * 2));
            Repaint();
        }

        private void SelectAndClose(MonoScript script)
        {
            _onSelected?.Invoke(script);
            Close();
        }

        private void RebuildFiltered()
        {
            _filtered.Clear();

            if (string.IsNullOrWhiteSpace(_search))
            {
                _filtered.AddRange(_allScripts);
                return;
            }

            var terms = _search.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var script in _allScripts)
            {
                var type = script.GetClass();
                var haystack = $"{type.Name} {type.Namespace}";
                if (terms.All(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase)))
                {
                    _filtered.Add(script);
                }
            }
        }
    }
}
