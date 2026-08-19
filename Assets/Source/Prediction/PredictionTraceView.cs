using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Prediction
{
    [RequireComponent(typeof(LineRenderer))]
    public class PredictionTraceView : MonoBehaviour
    {
        // Standard vector-graphics miter-limit ratio (SVG's stroke-miterlimit default is the same 4)
        // — beyond this the raw miter extension (which grows without bound as a joint approaches a
        // full reversal) gets clamped rather than left to spike out past the corner.
        private const float MiterLimit = 4f;

        // The alpha fade that hides a joint's overshoot (see ApplySegmentRender) has to span the
        // extension exactly, so a short leg between two sharp bends could otherwise have its fade-in
        // and fade-out together eat almost its whole length, reading as barely-there instead of a
        // proper line with a brief blend at each end. Capping the extension to this fraction of
        // EITHER segment it touches guarantees at least (1 - 2 * this) of a segment's own length
        // always stays fully opaque in the middle, at the cost of an imperfect corner close on the
        // rare short-and-sharp joint this actually kicks in for.
        private const float MaxJoinFraction = 0.3f;

        private static readonly GradientAlphaKey[] FullAlphaKeys =
        {
            new(1f, 0f),
            new(1f, 1f)
        };

        private readonly List<LineRenderer> _segments = new();
        private readonly List<Vector3> _segmentStarts = new();
        private readonly List<Vector3> _segmentEnds = new();
        private readonly List<float> _segmentStartExtensions = new();
        private readonly List<float> _segmentEndExtensions = new();
        private readonly GradientColorKey[] _colorKeyScratch = new GradientColorKey[2];
        private readonly GradientAlphaKey[] _flatOneFadeScratch = new GradientAlphaKey[3];
        private readonly GradientAlphaKey[] _flatBothFadeScratch = new GradientAlphaKey[4];

        private LineRenderer _template;
        private GradientAlphaKey[] _taperedAlphaKeys;
        private GradientAlphaKey[] _taperedFadeInScratch;
        private Gradient _flatGradient;
        private Gradient _taperedGradient;
        private Gradient _scratchGradient;
        private Color _color = Color.white;
        private int _activeSegmentCount;

        private void Awake()
        {
            _template = GetComponent<LineRenderer>();

            // Cached before SetColor ever touches it, so re-tinting can recover the authored alpha
            // fade over the line's length instead of losing it — only the LAST segment keeps this
            // shape now (see SetTrace); every earlier leg renders at flat full alpha instead, aside
            // from a short fade at any extended tip (see ApplySegmentRender).
            _taperedAlphaKeys = _template.colorGradient.alphaKeys;

            // Sized once here for the worst case ApplyTaperedSegment ever needs — the authored keys
            // plus one extra for the fade-in ramp — capped at Gradient's own 8-key ceiling.
            _taperedFadeInScratch = new GradientAlphaKey[Mathf.Min(_taperedAlphaKeys.Length + 1, 8)];

            _flatGradient = new Gradient();
            _taperedGradient = new Gradient();
            _scratchGradient = new Gradient();
            RebuildGradients(_color);

            _template.positionCount = 0;
            _segments.Add(_template);
        }

        // One LineRenderer per leg (two points each) instead of one shared multi-point line. With
        // numCornerVertices at 0, Unity draws a sharp shared-vertex bend visibly thin/pinched right at
        // the corner — a straight two-point segment has no bend inside it to pinch, so the fix is to
        // never share a vertex across a turn at all, eating the cost of one renderer per leg instead.
        // Every segment but the last renders at flat full alpha; only the last carries the authored
        // fade toward the tip, so the line still reads as one continuous taper despite being drawn as
        // several independent straight quads.
        public void SetTrace(IReadOnlyList<Vector3> points)
        {
            if (points == null || points.Count < 2)
            {
                Clear();
                return;
            }

            BuildSegmentEndpoints(points);

            var segmentCount = _segmentStarts.Count;
            EnsureSegmentCapacity(segmentCount);

            for (var i = 0; i < segmentCount; i++)
            {
                ApplySegmentRender(_segments[i], i, segmentCount);
            }

            for (var i = segmentCount; i < _segments.Count; i++)
            {
                _segments[i].positionCount = 0;
            }

            _activeSegmentCount = segmentCount;
        }

        // RGB is the flat config colour, same as startColor/endColor used to give; alpha is the
        // authored gradient's own shape on the last active segment, flat full alpha on every other
        // (aside from any extended-tip fade — see ApplySegmentRender).
        public void SetColor(Color color)
        {
            _color = color;
            RebuildGradients(color);

            // Re-applied immediately rather than waiting for the next SetTrace — LineRenderer's
            // colorGradient setter copies the gradient's data at assignment time, so an already-drawn
            // segment holding the old colour needs the rebuilt gradient handed back to it explicitly.
            // A currently-active joint fade is simply overwritten by the flat/tapered base here; it
            // recovers on the very next SetTrace, same as every other per-frame trace input.
            for (var i = 0; i < _activeSegmentCount; i++)
            {
                _segments[i].colorGradient = i == _activeSegmentCount - 1 ? _taperedGradient : _flatGradient;
            }
        }

        public void Clear()
        {
            for (var i = 0; i < _segments.Count; i++)
            {
                _segments[i].positionCount = 0;
            }

            _activeSegmentCount = 0;
        }

        // Each flat-capped rectangle meeting the next at an angle leaves a small notch uncovered on
        // the corner's outer side — the caps are perpendicular to their OWN segment, not to each
        // other, so they don't quite reach where the other segment's edge actually is. Rather than
        // moving the shared vertex to a single off-axis miter point (which bends each segment's own
        // rendered direction slightly away from its true heading to reach it — the two segments no
        // longer agree on where the joint even sits once one of them kinks), every segment is instead
        // extended a little PAST its own logical endpoint, straight along its OWN existing direction:
        // the segment ending at a joint reaches past it, and the segment starting there reaches back
        // before it, until each one's own tip lands inside the OTHER's rectangle. Neither segment's
        // direction is ever touched, only its length, so there's no risk of a segment kinking
        // part-way along its own leg the way the shared-point approach did. The extension amount at
        // each end is recorded (_segmentStartExtensions/_segmentEndExtensions) so ApplySegmentRender
        // can fade exactly that overshoot back to transparent instead of leaving it solidly visible.
        // The endpoints (launch, tip) are left untouched — there's no incoming/outgoing leg on either
        // side of them to extend against.
        private void BuildSegmentEndpoints(IReadOnlyList<Vector3> points)
        {
            _segmentStarts.Clear();
            _segmentEnds.Clear();
            _segmentStartExtensions.Clear();
            _segmentEndExtensions.Clear();

            var segmentCount = points.Count - 1;
            for (var i = 0; i < segmentCount; i++)
            {
                _segmentStarts.Add(points[i]);
                _segmentEnds.Add(points[i + 1]);
                _segmentStartExtensions.Add(0f);
                _segmentEndExtensions.Add(0f);
            }

            for (var i = 1; i < segmentCount; i++)
            {
                var extension = JoinExtension(points[i - 1], points[i], points[i + 1]);
                if (extension <= 0f)
                {
                    continue;
                }

                var incoming = ((Vector2)(points[i] - points[i - 1])).normalized;
                var outgoing = ((Vector2)(points[i + 1] - points[i])).normalized;

                _segmentEnds[i - 1] = points[i] + (new Vector3(incoming.x, incoming.y, 0f) * extension);
                _segmentStarts[i] = points[i] - (new Vector3(outgoing.x, outgoing.y, 0f) * extension);
                _segmentEndExtensions[i - 1] = extension;
                _segmentStartExtensions[i] = extension;
            }
        }

        // How far each of the two segments sharing this joint reaches past it, along its OWN
        // direction: halfWidth * tan(turnAngle / 2) is exactly the length that puts a segment's
        // notch-side corner right on the other segment's own boundary — any more and each tip sits
        // strictly inside the other's rectangle rather than merely touching it, which the shared,
        // symmetric extension on both sides already guarantees at this exact value. Clamped two ways:
        // a MiterLimit multiple of the half-width, so a joint approaching a full reversal (where tan
        // blows up) doesn't stretch a whisker out past the corner instead of just closing the notch;
        // and MaxJoinFraction of either adjacent segment's own length, so the alpha fade this same
        // extension drives (see ApplySegmentRender) can never eat most of a short segment.
        private float JoinExtension(Vector3 previous, Vector3 corner, Vector3 next)
        {
            var incoming = (Vector2)(corner - previous);
            var outgoing = (Vector2)(next - corner);
            var incomingLength = incoming.magnitude;
            var outgoingLength = outgoing.magnitude;
            if (incomingLength < 1e-4f || outgoingLength < 1e-4f)
            {
                return 0f;
            }

            var dot = Mathf.Clamp(Vector2.Dot(incoming / incomingLength, outgoing / outgoingLength), -1f, 1f);

            // Nearly straight (the caps already align, nothing to fill) or nearly a full reversal
            // (both edges are already the same pair of parallel lines, meeting flush with no gap) —
            // either way there's no notch, and tan(halfAngle) is undefined right at the reversal
            // extreme.
            if (dot > 0.999f || dot < -0.999f)
            {
                return 0f;
            }

            var halfWidth = _template.widthMultiplier * 0.5f;
            var halfAngle = Mathf.Acos(dot) * 0.5f;
            var rawExtension = halfWidth * Mathf.Tan(halfAngle);
            var maxExtension = Mathf.Min(
                halfWidth * MiterLimit,
                Mathf.Min(incomingLength, outgoingLength) * MaxJoinFraction);

            return Mathf.Min(rawExtension, maxExtension);
        }

        // The overlap the join fix buys at a corner is invisible from the geometry alone — with both
        // segments at full solid alpha, the overshoot each one carries past the true joint just reads
        // as two crossing flags instead of one clean corner. Fading each extended tip back toward
        // transparent hides the overshoot instead: a segment whose start was pushed back fades IN
        // from 0 over exactly that pushed-back span, and one whose end was pushed forward fades OUT
        // to 0 over exactly that span, so the two blend through the joint rather than visibly overlap.
        // Falls back to a plain 2-point segment with the cached flat/tapered gradient whenever there's
        // nothing to fade (the common case for a straight run).
        //
        // Unity's LineRenderer only SAMPLES a colorGradient at each actual vertex position, then
        // linearly interpolates the resulting vertex COLOURS across the mesh — it does not re-evaluate
        // the gradient at points in between. A 2-point segment therefore only ever shows a straight
        // ramp between the gradient's value at t=0 and at t=1; any extra keys in between (the "stay
        // fully opaque" plateau this whole fade is built from) are silently ignored, and with both
        // ends near zero the entire segment reads as uniformly faint instead of start-then-hold-then-
        // fade. ApplyFlatFade/ApplyTaperedSegment place a real vertex at every breakpoint the gradient
        // keys land on (still exactly collinear with the segment's own start/end, so no new bend is
        // introduced — a 0° turn pinches nothing) precisely so each key actually gets sampled.
        private void ApplySegmentRender(LineRenderer segment, int index, int segmentCount)
        {
            var start = _segmentStarts[index];
            var end = _segmentEnds[index];
            var isTapered = index == segmentCount - 1;

            _colorKeyScratch[0] = new GradientColorKey(_color, 0f);
            _colorKeyScratch[1] = new GradientColorKey(_color, 1f);

            // The tapered (last) segment always needs its own multi-key taper properly sampled —
            // see ApplyTaperedSegment — whether or not a preceding joint also needs a fade-in, so it
            // never takes the plain 2-point path a flat segment can.
            if (isTapered)
            {
                var taperedLength = Vector3.Distance(start, end);
                var startExtension = _segmentStartExtensions[index];
                var startFraction = taperedLength > 1e-4f ? Mathf.Clamp01(startExtension / taperedLength) : 0f;
                ApplyTaperedSegment(segment, start, end, startFraction);
                return;
            }

            var startExt = _segmentStartExtensions[index];
            var endExt = _segmentEndExtensions[index];
            if (startExt <= 0f && endExt <= 0f)
            {
                ApplyPlainSegment(segment, start, end);
                return;
            }

            var length = Vector3.Distance(start, end);
            var startFrac = length > 1e-4f ? Mathf.Clamp01(startExt / length) : 0f;
            var endFrac = length > 1e-4f ? Mathf.Clamp01(endExt / length) : 0f;

            // A segment too short for its own extensions to fit without the two fade ramps crossing
            // each other — degenerate, and rare enough (a very tight zig-zag) not to be worth a
            // partial fade; the plain base gradient reads better than an inverted one.
            if (startFrac + endFrac >= 1f)
            {
                ApplyPlainSegment(segment, start, end);
                return;
            }

            ApplyFlatFade(segment, start, end, startFrac, endFrac);
        }

        private void ApplyPlainSegment(LineRenderer segment, Vector3 start, Vector3 end)
        {
            segment.positionCount = 2;
            segment.SetPosition(0, start);
            segment.SetPosition(1, end);
            segment.colorGradient = _flatGradient;
        }

        private void ApplyFlatFade(LineRenderer segment, Vector3 start, Vector3 end, float startFraction, float endFraction)
        {
            if (startFraction > 0f && endFraction > 0f)
            {
                _flatBothFadeScratch[0] = new GradientAlphaKey(0f, 0f);
                _flatBothFadeScratch[1] = new GradientAlphaKey(1f, startFraction);
                _flatBothFadeScratch[2] = new GradientAlphaKey(1f, 1f - endFraction);
                _flatBothFadeScratch[3] = new GradientAlphaKey(0f, 1f);
                _scratchGradient.SetKeys(_colorKeyScratch, _flatBothFadeScratch);

                segment.positionCount = 4;
                segment.SetPosition(0, start);
                segment.SetPosition(1, Vector3.Lerp(start, end, startFraction));
                segment.SetPosition(2, Vector3.Lerp(start, end, 1f - endFraction));
                segment.SetPosition(3, end);
            }
            else if (startFraction > 0f)
            {
                _flatOneFadeScratch[0] = new GradientAlphaKey(0f, 0f);
                _flatOneFadeScratch[1] = new GradientAlphaKey(1f, startFraction);
                _flatOneFadeScratch[2] = new GradientAlphaKey(1f, 1f);
                _scratchGradient.SetKeys(_colorKeyScratch, _flatOneFadeScratch);

                segment.positionCount = 3;
                segment.SetPosition(0, start);
                segment.SetPosition(1, Vector3.Lerp(start, end, startFraction));
                segment.SetPosition(2, end);
            }
            else
            {
                _flatOneFadeScratch[0] = new GradientAlphaKey(1f, 0f);
                _flatOneFadeScratch[1] = new GradientAlphaKey(1f, 1f - endFraction);
                _flatOneFadeScratch[2] = new GradientAlphaKey(0f, 1f);
                _scratchGradient.SetKeys(_colorKeyScratch, _flatOneFadeScratch);

                segment.positionCount = 3;
                segment.SetPosition(0, start);
                segment.SetPosition(1, Vector3.Lerp(start, end, 1f - endFraction));
                segment.SetPosition(2, end);
            }

            segment.colorGradient = _scratchGradient;
        }

        // The tapered segment's own tip fade (hold near-full, then drop over the authored curve's
        // last stretch) needs a real vertex at each of its keys just as much as a join fade does —
        // otherwise a plain 2-point segment only ever samples the gradient's value at its start and
        // end, collapsing that shape into a flat linear ramp across the WHOLE segment instead of the
        // authored hold-then-drop. So this always places one vertex per _taperedAlphaKeys entry,
        // whether or not a preceding joint also needs a fade-in blended in front of them.
        //
        // With no fade-in (startFraction <= 0) the authored keys render completely unchanged — no
        // copy needed, _taperedAlphaKeys is used directly. With one, the keys are remapped from
        // their native [0,1] span into [startFraction,1] (linear: newTime = startFraction +
        // originalTime * (1 - startFraction)) so the taper's own shape is preserved, just compressed
        // to fit after the fade-in instead of starting from the segment's true (now offset)
        // beginning. _taperedFadeInScratch is always filled in full in that branch (see its own
        // sizing in Awake), so its Length is exactly how many vertices — and gradient keys — that
        // call needs.
        private void ApplyTaperedSegment(LineRenderer segment, Vector3 start, Vector3 end, float startFraction)
        {
            if (startFraction <= 0f)
            {
                _scratchGradient.SetKeys(_colorKeyScratch, _taperedAlphaKeys);
                segment.colorGradient = _scratchGradient;

                segment.positionCount = _taperedAlphaKeys.Length;
                for (var i = 0; i < _taperedAlphaKeys.Length; i++)
                {
                    segment.SetPosition(i, Vector3.Lerp(start, end, _taperedAlphaKeys[i].time));
                }

                return;
            }

            _taperedFadeInScratch[0] = new GradientAlphaKey(0f, 0f);
            _taperedFadeInScratch[1] = new GradientAlphaKey(_taperedAlphaKeys[0].alpha, startFraction);

            var count = Mathf.Min(_taperedAlphaKeys.Length, _taperedFadeInScratch.Length - 1);
            for (var i = 1; i < count; i++)
            {
                var original = _taperedAlphaKeys[i];
                var remappedTime = startFraction + (original.time * (1f - startFraction));
                _taperedFadeInScratch[i + 1] = new GradientAlphaKey(original.alpha, remappedTime);
            }

            _scratchGradient.SetKeys(_colorKeyScratch, _taperedFadeInScratch);
            segment.colorGradient = _scratchGradient;

            segment.positionCount = _taperedFadeInScratch.Length;
            for (var i = 0; i < _taperedFadeInScratch.Length; i++)
            {
                segment.SetPosition(i, Vector3.Lerp(start, end, _taperedFadeInScratch[i].time));
            }
        }

        // Every segment beyond the template is a plain child GameObject holding just a LineRenderer —
        // never Instantiate(_template.gameObject), which would also clone this MonoBehaviour onto every
        // new leg. Settings are copied once at creation, not re-synced per frame, since nothing here
        // ever changes them after Awake.
        private void EnsureSegmentCapacity(int count)
        {
            while (_segments.Count < count)
            {
                var segmentObject = new GameObject($"TraceSegment_{_segments.Count}");
                segmentObject.transform.SetParent(transform, worldPositionStays: false);

                var segment = segmentObject.AddComponent<LineRenderer>();
                CopyRenderSettings(_template, segment);
                _segments.Add(segment);
            }
        }

        private static void CopyRenderSettings(LineRenderer source, LineRenderer destination)
        {
            destination.sharedMaterial = source.sharedMaterial;
            destination.useWorldSpace = source.useWorldSpace;
            destination.widthMultiplier = source.widthMultiplier;
            destination.widthCurve = source.widthCurve;
            destination.numCornerVertices = source.numCornerVertices;
            destination.numCapVertices = source.numCapVertices;
            destination.alignment = source.alignment;
            destination.textureMode = source.textureMode;
            destination.textureScale = source.textureScale;
            destination.shadowBias = source.shadowBias;
            destination.generateLightingData = source.generateLightingData;
            destination.shadowCastingMode = source.shadowCastingMode;
            destination.receiveShadows = source.receiveShadows;
            destination.sortingLayerID = source.sortingLayerID;
            destination.sortingOrder = source.sortingOrder;
        }

        private void RebuildGradients(Color color)
        {
            _flatGradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                FullAlphaKeys);
            _taperedGradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                _taperedAlphaKeys);
        }
    }
}
