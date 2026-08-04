using System.Collections.Generic;
using BalloonParty.Configuration.Palette;
using BalloonParty.EditorUI.Utilities;
using BalloonParty.Game.Telemetry;
using BalloonParty.UI.Telemetry;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Two popups for a <see cref="MetricBinding" />: which snapshot, then which value out of it.
    ///     The value list is generated from <see cref="MetricCatalog" />/<see cref="TimerCatalog" />,
    ///     so adding a metric row makes it selectable here with no edit to this file.
    /// </summary>
    /// <remarks>
    ///     Deliberately a property drawer on the struct rather than a custom editor for
    ///     <c>MetricLabel</c> — Unity reuses a property drawer for each element of an array, so the day
    ///     a label needs several values this file does not change at all.
    /// </remarks>
    [CustomPropertyDrawer(typeof(MetricBinding))]
    internal sealed class MetricBindingDrawer : PropertyDrawer
    {
        private static readonly EditorAssetCache<GamePalette> PaletteCache = new();

        // One list per source, because what a source can answer differs. Everything cached here is
        // static: Unity reuses one drawer instance per array FIELD, not per element, so per-element
        // state in an instance field would corrupt across elements.
        private static SourceCatalog[] _bySource;

        // The colour names the lists were built from. Editing a GamePalette asset does not trigger a
        // domain reload, so a cache keyed only on "has this ever run" would keep offering colours that
        // no longer exist — and let an author bind to one, which then resolves to the placeholder
        // forever with no editor-time signal.
        private static string[] _builtFromColorNames;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnsureCatalog();
            EditorGUI.BeginProperty(position, label, property);

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            var source = property.FindPropertyRelative("_source");
            EditorGUI.PropertyField(line, source, label);

            var catalog = _bySource[Mathf.Clamp(source.intValue, 0, _bySource.Length - 1)];
            line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            var current = IndexOf(catalog, property);
            var picked = EditorGUI.Popup(line, " ", current, catalog.Paths);
            if (picked != current && picked >= 0)
            {
                Apply(property, catalog.Entries[picked]);
            }

            EditorGUI.EndProperty();
        }

        private static void EnsureCatalog()
        {
            var palette = PaletteCache.Value;
            var colorNames = ColorBucketNames(palette);
            if (_bySource != null && SameNames(_builtFromColorNames, colorNames))
            {
                return;
            }

            _builtFromColorNames = colorNames;
            _bySource = new[]
            {
                Build(MetricBindingSource.CeremonyLevel, colorNames),
                Build(MetricBindingSource.LastFlushedLevel, colorNames),
                Build(MetricBindingSource.Run, colorNames)
            };
        }

        // A source can only offer what its snapshot can actually answer. The sharp case is a run-scoped
        // metric (total_score, levels_completed, retries_used) on a level source: MetricScope.Absorb
        // never folds a metric BELOW its declared scope, so the level snapshot's slot is a genuine
        // zero — the label would render a plausible wrong number rather than a placeholder, and with
        // hide-when-zero it would silently vanish. Offering it at all is the bug.
        private static SourceCatalog Build(MetricBindingSource source, IReadOnlyList<string> colorNames)
        {
            var isLevel = source != MetricBindingSource.Run;
            var scope = isLevel ? MetricScopeKind.Level : MetricScopeKind.Run;
            var entries = new List<Entry>();
            var paths = new List<string>();

            foreach (var id in MetricCatalog.AllIds)
            {
                if (MetricCatalog.ScopeOf(id) > scope)
                {
                    continue;
                }

                var name = Humanize(MetricCatalog.WireNameOf(id));
                entries.Add(Entry.OfMetric(id));
                paths.Add(name);

                // Bucket counts come from the catalog's own slot table, not from BalloonType/ItemType
                // directly — the slot table is what survives into the extracted library, and a second
                // derivation here would be a fourth edit site the day axes become descriptors.
                foreach (var slot in MetricCatalog.AllSlots)
                {
                    if (slot.Id != id)
                    {
                        continue;
                    }

                    var count = slot.BucketCount == AxisSlotInfo.RuntimeSuppliedBucketCount
                        ? colorNames.Count
                        : slot.BucketCount;
                    for (var bucket = 0; bucket < count; bucket++)
                    {
                        // Through AxisBucketNaming, the same mapping the serializer and the resolver
                        // use — a second copy is how a dropdown starts offering names that no longer
                        // match what a label renders.
                        var bucketName = AxisBucketNaming.BucketName(colorNames, slot.Axis, bucket);
                        entries.Add(Entry.OfBucket(id, slot.Axis, bucket, bucketName));
                        paths.Add($"{name}/by {Humanize(slot.Axis.ToString())}/{bucketName}");
                    }
                }
            }

            // Every scope runs its own clocks, so a timer is answerable from any source.
            foreach (var timer in TimerCatalog.AllIds)
            {
                entries.Add(Entry.OfTimer(timer));
                paths.Add(Humanize(TimerCatalog.WireNameOf(timer)));
            }

            // A run snapshot carries no level identity — offering these on it authors a permanent
            // placeholder in two clicks.
            if (isLevel)
            {
                entries.Add(Entry.OfField(RecordField.LevelIndex));
                paths.Add("Record/Level index");
                entries.Add(Entry.OfField(RecordField.Completed));
                paths.Add("Record/Completed");
            }

            return new SourceCatalog(entries.ToArray(), paths.ToArray());
        }

        // One trailing bucket past the palette's own colours, for ids that are not progress colours —
        // matching how MetricSet sizes the colour axis.
        private static string[] ColorBucketNames(IGamePalette palette)
        {
            var names = palette != null ? palette.ProgressColorNames : null;
            var count = names?.Count ?? 0;
            var result = new string[count + 1];
            for (var i = 0; i < count; i++)
            {
                result[i] = names[i];
            }

            result[count] = AxisBucketNaming.OtherColorBucketName;
            return result;
        }

        // "max_wall_bounces_in_flight" → "Max wall bounces in flight". The wire name is the browsable
        // identity of a metric, so deriving the label from it keeps the dropdown and the exported
        // column obviously the same thing.
        private static string Humanize(string wireName)
        {
            var text = wireName.Replace('_', ' ');
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        private static bool SameNames(string[] left, string[] right)
        {
            if (left == null || left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        // intValue, never enumValueIndex: the latter is a position in the enum's name list, which only
        // matches the stored value while every member is implicitly numbered. MetricId is append-only,
        // which is exactly the discipline that tempts someone to reserve explicit ordinals.
        private static int IndexOf(SourceCatalog catalog, SerializedProperty property)
        {
            var kind = property.FindPropertyRelative("_kind").intValue;
            var metric = property.FindPropertyRelative("_metric").intValue;
            var timer = property.FindPropertyRelative("_timer").intValue;
            var axis = property.FindPropertyRelative("_axis").intValue;
            var field = property.FindPropertyRelative("_field").intValue;
            var colorName = property.FindPropertyRelative("_colorBucketName").stringValue;
            var ordinal = property.FindPropertyRelative("_bucketOrdinal").intValue;

            for (var i = 0; i < catalog.Entries.Length; i++)
            {
                if (catalog.Entries[i].Matches(kind, metric, timer, axis, field, colorName, ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void Apply(SerializedProperty property, in Entry entry)
        {
            property.FindPropertyRelative("_kind").intValue = (int)entry.Kind;
            property.FindPropertyRelative("_metric").intValue = entry.Metric;
            property.FindPropertyRelative("_timer").intValue = entry.Timer;
            property.FindPropertyRelative("_axis").intValue = entry.Axis;
            property.FindPropertyRelative("_field").intValue = entry.Field;
            property.FindPropertyRelative("_colorBucketName").stringValue = entry.ColorName;
            property.FindPropertyRelative("_bucketOrdinal").intValue = entry.Ordinal;
        }

        private readonly struct SourceCatalog
        {
            public readonly Entry[] Entries;
            public readonly string[] Paths;

            public SourceCatalog(Entry[] entries, string[] paths)
            {
                Entries = entries;
                Paths = paths;
            }
        }

        private readonly struct Entry
        {
            public readonly MetricValueKind Kind;
            public readonly int Metric;
            public readonly int Timer;
            public readonly int Axis;
            public readonly int Field;
            public readonly int Ordinal;
            public readonly string ColorName;

            private Entry(MetricValueKind kind, int metric, int timer, int axis, int field, int ordinal,
                string colorName)
            {
                Kind = kind;
                Metric = metric;
                Timer = timer;
                Axis = axis;
                Field = field;
                Ordinal = ordinal;
                ColorName = colorName;
            }

            public static Entry OfMetric(MetricId id)
            {
                return new Entry(MetricValueKind.Metric, (int)id, 0, 0, 0, 0, string.Empty);
            }

            public static Entry OfTimer(TimerId id)
            {
                return new Entry(MetricValueKind.Timer, 0, (int)id, 0, 0, 0, string.Empty);
            }

            // Colour buckets carry the NAME and leave the ordinal at zero: the palette is reorderable,
            // so the name is the only stable identity. The other two axes are enum ordinals.
            public static Entry OfBucket(MetricId id, MetricAxis axis, int ordinal, string name)
            {
                var color = axis == MetricAxis.Color;
                return new Entry(MetricValueKind.AxisBucket, (int)id, 0, (int)axis, 0,
                    color ? 0 : ordinal, color ? name : string.Empty);
            }

            public static Entry OfField(RecordField field)
            {
                return new Entry(MetricValueKind.RecordField, 0, 0, 0, (int)field, 0, string.Empty);
            }

            public bool Matches(int kind, int metric, int timer, int axis, int field, string colorName,
                int ordinal)
            {
                if ((int)Kind != kind)
                {
                    return false;
                }

                switch (Kind)
                {
                    case MetricValueKind.Metric:
                        return Metric == metric;
                    case MetricValueKind.Timer:
                        return Timer == timer;
                    case MetricValueKind.AxisBucket:
                        return Metric == metric && Axis == axis && ColorName == colorName
                            && Ordinal == ordinal;
                    default:
                        return Field == field;
                }
            }
        }
    }
}
