using System;
using System.Collections.Generic;
using System.Text;
using BalloonParty.Configuration.Level;
using BalloonParty.EditorUI.Utilities;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     The Level Pacing window's view of generation: the formula and catalogue to edit, and a read-only
    ///     table of what they generate per level, so a tuning change can be eyeballed without playing.
    /// </summary>
    internal static class LevelGenerationPreview
    {
        private const float LevelColWidth = 44f;
        private const float NumberColWidth = 64f;
        private const float PlayColWidth = 30f;
        private const float ListColMinWidth = 180f;
        private const int MaxPreviewLevels = 100;

        private static readonly Color SelectedRowColor = new(0.22f, 0.30f, 0.22f, 1f);

        private static bool _settingsExpanded;
        private static bool _previewExpanded = true;
        private static int _previewLevels = 20;
        private static Vector2 _scroll;

        internal static void Draw(LevelPacingConfiguration asset, SerializedObject serialized, Action<int> play)
        {
            _settingsExpanded = EditorGUILayout.Foldout(_settingsExpanded, "Level Generation (formula and catalogue)", true);
            if (_settingsExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serialized.FindProperty("_generation"), true);
                EditorGUI.indentLevel--;
            }

            _previewExpanded = EditorGUILayout.Foldout(_previewExpanded, "Generated Levels", true);
            if (!_previewExpanded)
            {
                return;
            }

            _previewLevels = EditorGUILayout.IntSlider("Levels shown", _previewLevels, 1, MaxPreviewLevels);
            DrawHeader();

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(260f));
            for (var level = 1; level <= _previewLevels; level++)
            {
                DrawLevelRow(asset, level, play);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawHeader()
        {
            var style = EditorStyles.miniBoldLabel;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Level", style, GUILayout.Width(LevelColWidth));
            GUILayout.Label(new GUIContent("Budget", "Threat / aid budget"), style, GUILayout.Width(NumberColWidth));
            GUILayout.Label("Board", style, GUILayout.Width(NumberColWidth));
            GUILayout.Label("Colors", style, GUILayout.Width(NumberColWidth));
            GUILayout.Label("Balloons", style, GUILayout.MinWidth(ListColMinWidth));
            GUILayout.Label("Items", style, GUILayout.MinWidth(ListColMinWidth));
            GUILayout.Label("Actors", style, GUILayout.MinWidth(ListColMinWidth));
            GUILayout.Space(PlayColWidth);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawLevelRow(LevelPacingConfiguration asset, int level, Action<int> play)
        {
            var settings = asset.Generation;
            var parameters = asset.ParametersForLevel(level);
            var style = EditorStyles.miniLabel;

            var rowRect = EditorGUILayout.BeginHorizontal();
            if (level == LevelPacingCurvePanel.SelectedLevel)
            {
                EditorGUI.DrawRect(rowRect, SelectedRowColor);
            }

            GUILayout.Label(level.ToString(), style, GUILayout.Width(LevelColWidth));
            var budget = $"{settings.BudgetForLevel(ContentRole.Threat, level):0.#} / " +
                         $"{settings.BudgetForLevel(ContentRole.Aid, level):0.#}";
            GUILayout.Label(budget, style, GUILayout.Width(NumberColWidth));
            GUILayout.Label(parameters.BoardLines.ToString(), style, GUILayout.Width(NumberColWidth));
            GUILayout.Label(settings.ColorsForLevel(level).ToString(), style, GUILayout.Width(NumberColWidth));
            ListLabel(Balloons(parameters, level, settings), style);
            ListLabel(Items(parameters, level, settings), style);
            ListLabel(Actors(parameters, level, settings), style);

            if (IconButtonHelper.DrawButton(
                    GUILayoutUtility.GetRect(PlayColWidth, EditorGUIUtility.singleLineHeight, GUILayout.Width(PlayColWidth)),
                    "PlayButton", "▶", $"Play from level {level}"))
            {
                play(level);
            }

            EditorGUILayout.EndHorizontal();
        }

        // The full list rides in the tooltip, since a long one is clipped in the column.
        private static void ListLabel(string text, GUIStyle style)
        {
            GUILayout.Label(new GUIContent(text, text), style, GUILayout.MinWidth(ListColMinWidth));
        }

        // A piece new at this level is starred.
        private static string Balloons(RangedLevelParameters parameters, int level, LevelGenerationSettings settings)
        {
            var text = new StringBuilder();
            foreach (var weight in parameters.BalloonWeights)
            {
                var star = IsNew(settings.Balloons, e => e.Type == weight.Type, level) ? "*" : "";
                Append(text, $"{weight.Type}{star} {weight.Weight:0.##}");
            }

            return text.ToString();
        }

        private static string Items(RangedLevelParameters parameters, int level, LevelGenerationSettings settings)
        {
            var text = new StringBuilder();
            foreach (var weight in parameters.ItemWeights)
            {
                var star = IsNew(settings.Items, e => e.Type == weight.Type, level) ? "*" : "";
                Append(text, $"{weight.Type}{star} {weight.Weight:0.##}");
            }

            return text.ToString();
        }

        private static string Actors(RangedLevelParameters parameters, int level, LevelGenerationSettings settings)
        {
            var text = new StringBuilder();
            foreach (var gate in parameters.GridActorGates)
            {
                var star = IsNew(settings.GridActors, e => e.Type == gate.Type, level) ? "*" : "";
                Append(text, $"{gate.Type}{star} {gate.Count.Min}–{gate.Count.Max}");
            }

            return text.ToString();
        }

        private static bool IsNew<T>(IReadOnlyList<T> catalogue, Func<T, bool> isSameType, int level)
            where T : IContentPiece
        {
            foreach (var entry in catalogue)
            {
                if (isSameType(entry) && !entry.Withheld && entry.UnlockLevel == level)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Append(StringBuilder text, string piece)
        {
            if (text.Length > 0)
            {
                text.Append(", ");
            }

            text.Append(piece);
        }
    }
}
