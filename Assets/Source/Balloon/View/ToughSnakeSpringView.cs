using System.Collections.Generic;
using BalloonParty.Balloon.Formation;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared.Math;
using BalloonParty.Slots.Grid;
using UnityEngine;
using VContainer;
#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE
using BalloonParty.Cheats;
#endif

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     A trial look (<see cref="IToughSnakeSpringSettings" />): a spring between each pair of neighbours in the
    ///     tough snake, tied to their knots. Each link is a <see cref="SpringChain" /> drawn as a line, so it sags
    ///     while the pair stands side by side, stretches as the one ahead steps away and swings as the one behind
    ///     catches up. A link lives as long as the same pair stands next to each other in the snake.
    ///     A link shows only once both its balloons are on screen, and it reveals: it grows out of the knot ahead
    ///     and reaches for the one behind until it ties on, then stays tied. A snake's links reveal head to tail,
    ///     each once the one ahead has tied on, and one whose balloon behind is further than the attach distance
    ///     (a tough flying in to the tail) stays hidden, growing and reaching as it closes in.
    /// </summary>
    internal sealed class ToughSnakeSpringView : MonoBehaviour
    {
        // Within this share of the gap between neighbouring slots, the one behind has arrived.
        private const float ArrivedShare = 1.15f;

        [Inject] private IToughSnakeBody _snake;
        [Inject] private SlotGrid _grid;
        [Inject] private IBalloonsConfiguration _balloonsConfig;

        private readonly List<Link> _links = new();
        private readonly List<Link> _spare = new();

        private float _neighbourGap;
        private Camera _camera;

        private void LateUpdate()
        {
            var settings = _balloonsConfig?.ToughSnakeSprings;
            if (settings == null || !Shown(settings))
            {
                RetireAll();
                return;
            }

            for (var i = 0; i < _links.Count; i++)
            {
                _links[i].Seen = false;
            }

            var members = _snake.ToughSnakeMembers;
            var aheadTied = true;
            for (var i = 0; i + 1 < members.Count; i++)
            {
                var ahead = KnotOf(members[i].SlotIndex);
                var behind = KnotOf(members[i + 1].SlotIndex);
                if (ahead == null || behind == null || !OnScreen(ahead) || !OnScreen(behind))
                {
                    aheadTied = false;
                    continue;
                }

                var link = LinkFor(members[i], members[i + 1], ahead, settings);
                Reveal(link, ahead, behind, aheadTied, settings);
                Draw(link, ahead, behind, settings);
                aheadTied = link.Tied;
            }

            for (var i = _links.Count - 1; i >= 0; i--)
            {
                if (!_links[i].Seen)
                {
                    Retire(i);
                }
            }
        }

        private static bool Shown(IToughSnakeSpringSettings settings)
        {
#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE
            return settings.Enabled != CheatState.FlipToughSnakeSprings;
#else
            return settings.Enabled;
#endif
        }

        // Null while the slot's view is gone or its knot hidden (a balloon mid-pop, or not a tough).
        private SpriteRenderer KnotOf(Vector2Int slot)
        {
            if (!_grid.InBounds(slot) || _grid.ViewAt(slot) is not BalloonView view)
            {
                return null;
            }

            var knot = view.Knot;
            return knot != null && knot.gameObject.activeInHierarchy ? knot : null;
        }

        // A balloon still rising from below the board hasn't appeared yet.
        private bool OnScreen(SpriteRenderer knot)
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return true;
                }
            }

            var point = _camera.WorldToViewportPoint(knot.transform.position);
            return point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
        }

        private Link LinkFor(object ahead, object behind, SpriteRenderer aheadKnot, IToughSnakeSpringSettings settings)
        {
            for (var i = 0; i < _links.Count; i++)
            {
                if (ReferenceEquals(_links[i].Ahead, ahead) && ReferenceEquals(_links[i].Behind, behind))
                {
                    return _links[i];
                }
            }

            var link = Take(settings);
            link.Ahead = ahead;
            link.Behind = behind;
            link.Grown = 0f;
            link.Reach = 0f;
            link.Tied = false;
            link.Chain.Place(aheadKnot.transform.position, aheadKnot.transform.position);
            link.Line.sharedMaterial = aheadKnot.sharedMaterial;
            link.Line.gameObject.SetActive(true);
            _links.Add(link);
            return link;
        }

        private Link Take(IToughSnakeSpringSettings settings)
        {
            for (var i = _spare.Count - 1; i >= 0; i--)
            {
                var spare = _spare[i];
                _spare.RemoveAt(i);
                if (spare.Chain.Points == settings.Segments + 1)
                {
                    return spare;
                }

                Destroy(spare.Line.gameObject);
            }

            var line = new GameObject("ToughSnakeSpring").AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.useWorldSpace = true;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.positionCount = settings.Segments + 1;
            return new Link(new SpringChain(settings.Segments), line);
        }

        // Grows a link not yet tied, once the link ahead has tied on, no further than the one behind has closed in.
        private void Reveal(Link link, SpriteRenderer ahead, SpriteRenderer behind, bool aheadTied,
            IToughSnakeSpringSettings settings)
        {
            link.Seen = true;
            if (link.Tied)
            {
                return;
            }

            if (aheadTied)
            {
                link.Grown = Mathf.MoveTowards(link.Grown, 1f, Time.deltaTime / settings.RevealSeconds);
            }

            var arrived = NeighbourGap() * ArrivedShare;
            var gap = Vector3.Distance(ahead.transform.position, behind.transform.position);
            var closedIn = Mathf.InverseLerp(Mathf.Max(settings.AttachDistance, arrived + 0.01f), arrived, gap);
            link.Reach = Mathf.Min(link.Grown, closedIn);
            link.Tied = link.Reach >= 1f;
        }

        private void Draw(Link link, SpriteRenderer ahead, SpriteRenderer behind, IToughSnakeSpringSettings settings)
        {
            var line = link.Line;
            if (link.Reach <= 0f)
            {
                // Hidden, it waits in the knot ahead, to grow out of it.
                line.enabled = false;
                link.Chain.Place(ahead.transform.position, ahead.transform.position);
                return;
            }

            line.enabled = true;
            var tuning = new SpringChainTuning(settings.Stiffness, settings.Gravity, settings.Damping,
                1f / settings.StepsPerSecond, settings.Iterations);
            var rest = NeighbourGap() * settings.Slack;
            link.Chain.Step(ahead.transform.position, behind.transform.position, rest, Time.deltaTime, tuning,
                link.Tied ? 1f : link.Reach);

            line.widthMultiplier = settings.Width;
            line.startColor = Shade(ahead.color, settings.KnotTint);
            line.endColor = Shade(behind.color, settings.KnotTint);
            line.sortingLayerID = ahead.sortingLayerID;
            // Under both knots, so it ties on behind them.
            line.sortingOrder = Mathf.Min(ahead.sortingOrder, behind.sortingOrder) - 1;
            for (var i = 0; i < link.Chain.Points; i++)
            {
                line.SetPosition(i, link.Chain.Point(i));
            }
        }

        // The world gap between neighbouring slots (a hex grid's are all the same); the grid's own slot separation
        // is half of it across a row.
        private float NeighbourGap()
        {
            if (_neighbourGap <= 0f)
            {
                _neighbourGap = Vector3.Distance(_grid.IndexToWorldPosition(Vector2Int.zero),
                    _grid.IndexToWorldPosition(Vector2Int.right));
            }

            return _neighbourGap;
        }

        private static Color Shade(Color knot, float tint)
        {
            return new Color(knot.r * tint, knot.g * tint, knot.b * tint, knot.a);
        }

        private void RetireAll()
        {
            for (var i = _links.Count - 1; i >= 0; i--)
            {
                Retire(i);
            }
        }

        private void Retire(int index)
        {
            var link = _links[index];
            _links.RemoveAt(index);
            link.Ahead = null;
            link.Behind = null;
            link.Line.gameObject.SetActive(false);
            _spare.Add(link);
        }

        private sealed class Link
        {
            internal readonly SpringChain Chain;
            internal readonly LineRenderer Line;

            internal object Ahead;
            internal object Behind;
            internal bool Seen;
            internal float Grown;
            internal float Reach;
            internal bool Tied;

            internal Link(SpringChain chain, LineRenderer line)
            {
                Chain = chain;
                Line = line;
            }
        }
    }
}
