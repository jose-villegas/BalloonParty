using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Game.Telemetry
{
    // Bucket <-> name mapping for the three MetricAxis kinds. TelemetryEnvelopeSerializer needed the
    // forward direction (index -> name) first; W4's MetricValueResolver (BalloonParty.UI.Telemetry) needs
    // the inverse (name -> index) for a MetricBinding authored against a color NAME rather than its
    // runtime-reordered palette index. Lives beside MetricCatalog rather than in UI/Telemetry: this is
    // telemetry vocabulary (palette color buckets, balloon/item enum buckets), not a UI concern, and the
    // serializer already carried this exact logic before the resolver needed its inverse — duplicating it
    // in two places is precisely the drift a shared home exists to prevent.
    //
    // Takes the colour names as a bare IReadOnlyList<string> rather than IGamePalette, per the plan's
    // Separability table: this file is the bucketIndex -> name half of the axis descriptor the extracted
    // library will take at construction, and a game config interface in its signature would come along.
    internal static class AxisBucketNaming
    {
        internal const string OtherColorBucketName = "other";
        internal const string UnknownBucketName = "unknown";

        // Colour buckets index IGamePalette.ProgressColorNames, with one trailing bucket for ids that are
        // not progress colours — rainbow's sentinel, paint-converted, or an actor with no colour at all.
        // BalloonType and ItemType index their enums directly. Every branch falls back to a NAME on an
        // out-of-range bucket — Enum.ToString() on an undefined value returns the integer as a string,
        // which would silently reintroduce the dense index this whole scheme exists to keep off the wire
        // (and off a label).
        internal static string BucketName(IReadOnlyList<string> colorNames, MetricAxis axis, int bucket)
        {
            switch (axis)
            {
                case MetricAxis.Color:
                    return colorNames != null && bucket < colorNames.Count
                        ? colorNames[bucket]
                        : OtherColorBucketName;
                case MetricAxis.BalloonType:
                    return Enum.IsDefined(typeof(BalloonType), bucket)
                        ? ((BalloonType)bucket).ToString()
                        : UnknownBucketName;
                case MetricAxis.ItemType:
                    return Enum.IsDefined(typeof(ItemType), bucket)
                        ? ((ItemType)bucket).ToString()
                        : UnknownBucketName;
                default:
                    throw new ArgumentOutOfRangeException(nameof(axis), axis, "Unhandled MetricAxis.");
            }
        }

        // The inverse the serializer never needed: a MetricBinding stores a colour bucket by NAME (a
        // stored index would silently repoint every label the day the palette is reordered), so rendering
        // it means resolving that name back to today's index. False on no match — an authored colour that
        // no longer exists in the palette — so the caller can warn once and fall back to a placeholder
        // instead of throwing inside a popup.
        internal static bool TryColorBucketIndexOf(IReadOnlyList<string> colorNames, string colorName,
            out int bucket)
        {
            var count = colorNames?.Count ?? 0;
            if (colorName == OtherColorBucketName)
            {
                bucket = count;
                return true;
            }

            for (var i = 0; i < count; i++)
            {
                if (colorNames[i] == colorName)
                {
                    bucket = i;
                    return true;
                }
            }

            bucket = -1;
            return false;
        }
    }
}
