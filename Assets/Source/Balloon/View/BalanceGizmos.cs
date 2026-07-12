using BalloonParty.Balloon.Controller;
using BalloonParty.Slots.Grid;
using UnityEngine;
using VContainer;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     Scene-view overlay for the recorded pressure propagations: seed shove, expansion edges
    ///     colored by alignment, rejected edges, and the executed chain. Events fade over a few
    ///     seconds; the recorder's ring keeps the last resolutions inspectable after the fact.
    /// </summary>
    public class BalanceGizmos : MonoBehaviour
    {
#if UNITY_EDITOR
        private const float SeedArrowLength = 0.6f;
        private const float EdgeTrim = 0.15f;
        private const float HeadLength = 0.12f;
        private const float HeadWidth = 0.06f;
        private const float RejectMarkSize = 0.08f;
        private const float ChainWidth = 6f;

        private static readonly Color SeedColor = new(0.35f, 0.75f, 1f, 1f);
        private static readonly Color AlignmentLow = new(1f, 0.25f, 0.2f, 1f);
        private static readonly Color AlignmentHigh = new(0.2f, 1f, 0.3f, 1f);
        private static readonly Color RejectedStaticColor = new(0.55f, 0.55f, 0.55f, 0.45f);
        private static readonly Color RejectedVisitedColor = new(0.4f, 0.4f, 0.5f, 0.45f);
        private static readonly Color RejectedBackflowColor = new(0.6f, 0.35f, 0.35f, 0.45f);
        private static readonly Color ChainMoveColor = new(0.3f, 1f, 0.4f, 1f);
        private static readonly Color ChainRelocationColor = new(1f, 0.4f, 1f, 1f);

        [SerializeField] private float _fadeSeconds = 5f;
        [SerializeField] private bool _showEdges = true;
        [SerializeField] private bool _showWeights = true;
        [SerializeField] private bool _showRejected = true;

        [Inject] private BalanceDebugRecorder _recorder;
        [Inject] private SlotGrid _grid;

        private GUIStyle _labelStyle;

        private void OnDrawGizmos()
        {
            if (_recorder == null || _grid == null || _fadeSeconds <= 0f)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            var events = _recorder.Events;
            for (var i = 0; i < events.Count; i++)
            {
                var resolution = events[i];
                var age = now - resolution.Timestamp;
                if (age < 0f || age > _fadeSeconds)
                {
                    continue;
                }

                DrawResolution(resolution, 1f - (age / _fadeSeconds));
            }
        }

        private void DrawResolution(BalanceDebugRecorder.Resolution resolution, float alpha)
        {
            DrawSeed(resolution, alpha);

            if (_showEdges)
            {
                DrawEdges(resolution, alpha);
            }

            if (resolution.Resolved)
            {
                DrawChain(resolution, alpha);
            }

            if (_showWeights)
            {
                DrawWeights(resolution, alpha);
            }
        }

        private void DrawSeed(BalanceDebugRecorder.Resolution resolution, float alpha)
        {
            var tip = _grid.IndexToWorldPosition(resolution.Seed);
            var tail = tip - ((Vector3)resolution.SeedDirection * SeedArrowLength);
            DrawArrow(tail, tip, Fade(SeedColor, alpha));
        }

        private void DrawEdges(BalanceDebugRecorder.Resolution resolution, float alpha)
        {
            var edges = resolution.Edges;
            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                var from = _grid.IndexToWorldPosition(edge.From);
                var to = _grid.IndexToWorldPosition(edge.To);
                // Trim both ends so arrows don't bury themselves under the slot markers.
                var tail = Vector3.Lerp(from, to, EdgeTrim);
                var tip = Vector3.Lerp(from, to, 1f - EdgeTrim);

                if (edge.Accepted)
                {
                    var color = Color.Lerp(AlignmentLow, AlignmentHigh, Mathf.Clamp01(edge.Alignment));
                    DrawArrow(tail, tip, Fade(color, alpha));
                }
                else if (_showRejected)
                {
                    var color = Fade(RejectionColor(edge.Rejection), alpha);
                    Gizmos.color = color;
                    Gizmos.DrawLine(tail, tip);
                    DrawRejectMark(tip, color);
                }
            }
        }

        private void DrawChain(BalanceDebugRecorder.Resolution resolution, float alpha)
        {
            var color = Fade(
                resolution.Terminal == BalanceDebugRecorder.TerminalKind.Relocation
                    ? ChainRelocationColor
                    : ChainMoveColor,
                alpha);
            Handles.color = color;

            var chain = resolution.Chain;
            for (var i = 0; i < chain.Count; i++)
            {
                var from = _grid.IndexToWorldPosition(chain[i].From);
                var to = _grid.IndexToWorldPosition(chain[i].To);
                Handles.DrawAAPolyLine(ChainWidth, from, to);
                DrawArrowHead(from, to, color);
            }
        }

        private void DrawWeights(BalanceDebugRecorder.Resolution resolution, float alpha)
        {
            _labelStyle ??= new GUIStyle
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
            };
            _labelStyle.normal.textColor = Fade(Color.white, alpha);

            var nodes = resolution.Nodes;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var position = _grid.IndexToWorldPosition(node.Slot);
                Handles.Label(position, $"{node.Order}: {node.PathScore:0.00}", _labelStyle);
            }
        }

        private static void DrawArrow(Vector3 from, Vector3 to, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawLine(from, to);
            DrawArrowHead(from, to, color);
        }

        private static void DrawArrowHead(Vector3 from, Vector3 to, Color color)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < 1e-6f)
            {
                return;
            }

            var direction = delta.normalized;
            var right = new Vector3(-direction.y, direction.x, 0f);
            var headBase = to - (direction * HeadLength);
            Gizmos.color = color;
            Gizmos.DrawLine(to, headBase + (right * HeadWidth));
            Gizmos.DrawLine(to, headBase - (right * HeadWidth));
        }

        private static void DrawRejectMark(Vector3 position, Color color)
        {
            Gizmos.color = color;
            var a = new Vector3(RejectMarkSize, RejectMarkSize, 0f);
            var b = new Vector3(RejectMarkSize, -RejectMarkSize, 0f);
            Gizmos.DrawLine(position - a, position + a);
            Gizmos.DrawLine(position - b, position + b);
        }

        private static Color RejectionColor(BalanceDebugRecorder.EdgeRejection rejection)
        {
            return rejection switch
            {
                BalanceDebugRecorder.EdgeRejection.Visited => RejectedVisitedColor,
                BalanceDebugRecorder.EdgeRejection.Backflow => RejectedBackflowColor,
                _ => RejectedStaticColor,
            };
        }

        private static Color Fade(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, color.a * alpha);
        }
#endif
    }
}
