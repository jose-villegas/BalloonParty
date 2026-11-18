using System.Collections.Generic;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     Plays a ripe cluster's spiral and <see cref="RipeBubbleBurstMessage" />. A ripple sends a ring over the
    ///     water: one soap bubble drawn with the coat's material, under the balloons, spreading from the big bubble the
    ///     popped cluster folded into to <see cref="IRipeBubbleSettings.RingReach" /> slot spacings as it fades. Foam
    ///     plays the hand-out: the cluster, off the board but still on screen in its warning colour, lets one bubble go
    ///     every <see cref="IRipeBubbleSettings.FoamStepSeconds" /> (<see cref="RipeBubbleRule.FoamDepartSeconds" />),
    ///     its shown count stepping down with them until it is gone. Each bubble hops to its taker on the merge's arc
    ///     (<see cref="SoapBubbleHop" />): a coat stays hidden (<see cref="BalloonView.HoldCoat" />) until its bubble
    ///     lands, and a neighbouring cluster holds its count and takes the bubble as a merge's receiver does
    ///     (<see cref="IBubbleMergeReactive" />). A leftover drifts a little way out and puffs away. A hop whose taker
    ///     leaves its view (popped, despawned, bound again) is dropped, and that view let go of what it held.
    /// </summary>
    internal sealed class RipeBubbleBurstView : MonoBehaviour
    {
        // A ripe cluster, swollen, fills about half a slot spacing each way: the ring starts from the one big
        // bubble it folds into, a little bigger again.
        private const float RingStartReach = 0.5f;

        // Where a spiral's bubble starts, in slot spacings from the cluster's centre: at its rim, so the arm
        // winds out of the cluster without covering it.
        private const float SpiralStartReach = 0.42f;

        // A spiral's bubble leaves at this share of its full size and grows as it goes out.
        private const float SpiralStartScale = 0.3f;

        // The share of its life over which a spiral's bubble fades, at the arm's outer end.
        private const float SpiralFadeShare = 0.25f;

        // How long a source keeps shedding after the beat that fed it: a little more than the time to the next
        // beat, so the arm is unbroken while the fuse runs and stops by itself when the beats do.
        private const float SlowSourceSeconds = 1.15f;
        private const float FastSourceSeconds = 0.65f;

        // The arm turns only a little faster in the fuse's last seconds: it is the thicker shedding that
        // shows the hurry, and an arm wound much tighter stops reading as a spiral.
        private const float FastTurnSpeedup = 1.25f;

        // Over the balloons round the cluster, which the spray flies across.
        private const int SpiralSortingOrder = 5000;

        // A leftover bubble drifts this far out (slot spacings) in this share of a hop's time, then puffs away.
        private const float LeftoverReach = 0.35f;
        private const float LeftoverDriftShare = 0.5f;

        private readonly List<Ring> _rings = new();
        private readonly List<FoamHop> _hops = new();
        private readonly List<Handout> _handouts = new();
        private readonly Stack<Handout> _freeHandouts = new();
        private readonly List<SpiralBubble> _spirals = new();
        private readonly List<SpiralSource> _sources = new();
        private readonly Stack<SpiralSource> _freeSources = new();
        private readonly CompositeDisposable _subscriptions = new();

        private SlotGrid _grid;
        private IBalloonsConfiguration _balloons;
        private IRipeBubbleSettings _settings;
        private IBubbleMergeCueSettings _hopSettings;
        private PoolManager _poolManager;
        private SoapBubbleSprites _bubbles;
        private int _ringLayerId;
        private float _slotSpacing;

        private void Update()
        {
            // Scaled time: a pause holds the rings, the hand-outs and the hops where they are.
            var deltaTime = Time.deltaTime;
            for (var i = _rings.Count - 1; i >= 0; i--)
            {
                var ring = _rings[i];
                if (AdvanceRing(ref ring, deltaTime))
                {
                    _rings[i] = ring;
                    continue;
                }

                _rings.RemoveAt(i);
            }

            // Hand-outs before hops: a bubble that leaves this frame is placed this frame.
            AdvanceHandouts(deltaTime);
            for (var i = _hops.Count - 1; i >= 0; i--)
            {
                var hop = _hops[i];
                if (AdvanceHop(ref hop, deltaTime))
                {
                    _hops[i] = hop;
                    continue;
                }

                _hops.RemoveAt(i);
            }

            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                var source = _sources[i];
                if (AdvanceSource(source, deltaTime))
                {
                    continue;
                }

                // Its bubbles still fly round the centre it holds, so it is kept until the last has faded.
                if (source.Flying == 0)
                {
                    _sources.RemoveAt(i);
                    source.Clear();
                    _freeSources.Push(source);
                }
            }

            for (var i = _spirals.Count - 1; i >= 0; i--)
            {
                var spiral = _spirals[i];
                if (AdvanceSpiral(ref spiral, deltaTime))
                {
                    _spirals[i] = spiral;
                    continue;
                }

                _spirals.RemoveAt(i);
            }
        }

        private void OnDestroy()
        {
            _subscriptions.Dispose();
        }

        [Inject]
        internal void Construct(ISubscriber<RipeBubbleBurstMessage> burstSubscriber,
            ISubscriber<RipeFuseBeatMessage> beatSubscriber, ISubscriber<BubbleRipenedMessage> ripenedSubscriber,
            SlotGrid grid, IBalloonsConfiguration balloons, PoolManager poolManager)
        {
            _grid = grid;
            _balloons = balloons;
            _settings = balloons.RipeBubble;
            _hopSettings = balloons.BubbleMergeCue;
            _poolManager = poolManager;
            _bubbles = new SoapBubbleSprites(transform, "RipeBurstBubble");
            _ringLayerId = SortingLayer.NameToID(_settings.RingSortingLayerName);
            // Neighbouring slots on a row: every neighbour of a slot is this far from it.
            _slotSpacing = Vector3.Distance(
                grid.IndexToWorldPosition(Vector2Int.zero), grid.IndexToWorldPosition(Vector2Int.right));
            burstSubscriber.Subscribe(OnBurst).AddTo(_subscriptions);
            beatSubscriber.Subscribe(OnFuseBeat).AddTo(_subscriptions);
            ripenedSubscriber.Subscribe(OnRipened).AddTo(_subscriptions);
        }

        private void OnRipened(BubbleRipenedMessage message)
        {
            FeedSource(message.Slot, fast: false);
        }

        private void OnFuseBeat(RipeFuseBeatMessage message)
        {
            FeedSource(message.Slot, message.Fast);
        }

        // A ripe cluster sheds soap from a point that turns round it, so the bubbles draw a spiral arm winding
        // out of it. Each beat of its fuse feeds the source a little longer than the wait for the next, so the
        // arm is unbroken for the whole fuse and ends when the beats do. The source follows the cluster's view,
        // not its slot: a cluster the board is moving is drawn on its way there, and the arm goes with it.
        private void FeedSource(Vector2Int slot, bool fast)
        {
            if (_settings.SpiralInterval <= 0f || _settings.SpiralSeconds <= 0f
                || _settings.CoatMaterial == null || _settings.CoatSprite == null)
            {
                return;
            }

            var view = _grid.ActorViewAt<BalloonView>(slot);
            if (view == null || view.Model == null)
            {
                return;
            }

            var seconds = fast ? FastSourceSeconds : SlowSourceSeconds;
            for (var i = 0; i < _sources.Count; i++)
            {
                var known = _sources[i];
                if (ReferenceEquals(known.Model, view.Model))
                {
                    known.Remaining = seconds;
                    known.Fast = fast;
                    return;
                }
            }

            var source = _freeSources.Count > 0 ? _freeSources.Pop() : new SpiralSource();
            source.Start(view, Random.value * Mathf.PI * 2f, Random.value < 0.5f ? 1f : -1f, seconds, fast);
            _sources.Add(source);
        }

        // False once it has stopped shedding: the beats stopped feeding it, or its cluster is gone. A cluster
        // leaves the board the moment it is popped or its fuse runs out, while its view plays on: the arm stops
        // then, not when the view goes.
        private bool AdvanceSource(SpiralSource source, float deltaTime)
        {
            if (source.Remaining <= 0f)
            {
                return false;
            }

            // A popped or pooled view shows another model, or none: the cluster this followed is gone.
            if (source.View == null || !ReferenceEquals(source.View.Model, source.Model) || !OnBoard(source.Model))
            {
                source.Remaining = 0f;
                return false;
            }

            source.Centre = source.View.transform.position;
            var rate = source.Fast ? _settings.SpiralFastSpeedup : 1f;
            source.Angle += source.Turn * _settings.SpiralTurnsPerSecond * Mathf.PI * 2f * deltaTime
                            * (source.Fast ? FastTurnSpeedup : 1f);
            source.SinceBubble += deltaTime;
            var interval = _settings.SpiralInterval / rate;
            var material = _settings.CoatMaterial;
            var sprite = _settings.CoatSprite;
            var scale = SoapBubbleShader.ScaleForRadius(material, sprite, _settings.SpiralBubbleRadius * _slotSpacing);

            // At most a few a frame, however long the frame was: a hitch must not flood the board with soap.
            for (var shed = 0; shed < 3 && source.SinceBubble >= interval; shed++)
            {
                source.SinceBubble -= interval;
                var bubble = _bubbles.Rent(material, sprite, _ringLayerId, SpiralSortingOrder);
                var spiral = new SpiralBubble(bubble, source, source.Angle, scale);
                source.Flying++;
                PlaceSpiral(in spiral, 0f);
                _spirals.Add(spiral);
            }

            source.SinceBubble = Mathf.Min(source.SinceBubble, interval);
            source.Remaining -= deltaTime;
            return source.Remaining > 0f;
        }

        private bool OnBoard(IBalloonModel model)
        {
            return ReferenceEquals(_grid.At(model.SlotIndex.Value), model);
        }

        // False once the bubble has faded.
        private bool AdvanceSpiral(ref SpiralBubble spiral, float deltaTime)
        {
            spiral.Elapsed += deltaTime;
            var t = spiral.Elapsed / _settings.SpiralSeconds;
            if (t >= 1f)
            {
                _bubbles.Return(spiral.Bubble);
                spiral.Source.Flying--;
                return false;
            }

            PlaceSpiral(in spiral, t);
            return true;
        }

        // Straight out from the cluster at a steady pace, along the direction it left in: the source has turned
        // on by the time the next one leaves, which is what winds them into an arm. It is measured from where
        // the cluster is now, so the whole arm travels with a cluster the board moves. It grows as it goes and
        // fades at the arm's end.
        private void PlaceSpiral(in SpiralBubble spiral, float t)
        {
            var radius = Mathf.Lerp(SpiralStartReach, _settings.SpiralReach, t) * _slotSpacing;
            var body = spiral.Bubble.transform;
            body.position = spiral.Source.Centre
                + new Vector3(Mathf.Cos(spiral.Angle) * radius, Mathf.Sin(spiral.Angle) * radius, 0f);
            var scale = spiral.Scale * Mathf.Lerp(SpiralStartScale, 1f, t);
            body.localScale = new Vector3(scale, scale, 1f);
            var color = spiral.Bubble.color;
            color.a = 1f - Mathf.Clamp01((t - (1f - SpiralFadeShare)) / SpiralFadeShare);
            spiral.Bubble.color = color;
        }

        private void OnBurst(RipeBubbleBurstMessage message)
        {
            if (message.Kind == RipeBurstKind.Ripple)
            {
                StartRing(message.Position);
            }
            else
            {
                StartHandout(message);
            }
        }

        private void StartRing(Vector3 position)
        {
            var material = _settings.CoatMaterial;
            var sprite = _settings.CoatSprite;
            if (_settings.RingSeconds <= 0f || material == null || sprite == null)
            {
                return;
            }

            var bubble = _bubbles.Rent(material, sprite, _ringLayerId, _settings.RingSortingOrder);
            bubble.transform.position = position;
            var startReach = RingStartReach * _settings.SwellScale * RipeBubbleRule.FoldedSize;
            var from = SoapBubbleShader.ScaleForRadius(material, sprite, startReach * _slotSpacing);
            var to = SoapBubbleShader.ScaleForRadius(material, sprite, _settings.RingReach * _slotSpacing);
            var ring = new Ring(bubble, from, to);
            Place(in ring, 0f);
            _rings.Add(ring);
        }

        // False once the ring has faded.
        private bool AdvanceRing(ref Ring ring, float deltaTime)
        {
            ring.Elapsed += deltaTime;
            var t = ring.Elapsed / _settings.RingSeconds;
            if (t >= 1f)
            {
                _bubbles.Return(ring.Bubble);
                return false;
            }

            Place(in ring, t);
            return true;
        }

        // Fast out, slowing as it spreads, and fading all the way.
        private static void Place(in Ring ring, float t)
        {
            var spread = 1f - ((1f - t) * (1f - t));
            var scale = Mathf.Lerp(ring.FromScale, ring.ToScale, spread);
            ring.Bubble.transform.localScale = new Vector3(scale, scale, 1f);
            var color = ring.Bubble.color;
            color.a = 1f - t;
            ring.Bubble.color = color;
        }

        // Heard before the hand-out changes the board: the cluster is still in its slot showing all its bubbles, the
        // coats are not on yet and the neighbouring clusters still show their old counts. Each taker is held here,
        // so none shows its bubble before the bubble lands.
        private void StartHandout(RipeBubbleBurstMessage message)
        {
            var bubbles = message.Bubbles;
            var cluster = _grid.ActorViewAt<BalloonView>(message.Slot);
            if (bubbles == 0 || cluster == null || cluster.Variant is not IBubbleMergeReactive cue
                || cue.BubbleRenderer == null)
            {
                return;
            }

            var handout = _freeHandouts.Count > 0 ? _freeHandouts.Pop() : new Handout();
            handout.Start(cluster, cue, message.Position, message.Handout, bubbles,
                cue.BubbleWorldScale * _hopSettings.BubbleScale);
            cue.HoldCount();
            HexCoordinates.HexNeighborIndices(message.Slot.x, message.Slot.y, handout.Slots);
            for (var bubble = 0; bubble < bubbles; bubble++)
            {
                var gift = message.GiftOf(bubble, out var direction);
                if (gift != FoamGift.Leftover)
                {
                    HoldTaker(handout, gift, direction);
                }
            }

            _handouts.Add(handout);
        }

        private void HoldTaker(Handout handout, FoamGift gift, int direction)
        {
            var target = _grid.ActorViewAt<BalloonView>(handout.Slots[direction]);
            if (target == null || target.Model == null)
            {
                return;
            }

            handout.Targets[direction] = target;
            handout.Models[direction] = target.Model;
            if (gift == FoamGift.Coat)
            {
                // Only a balloon with no coat yet waits for its bubble: a second layer looks as the first does.
                if (target.Model is ICoatable { Coat: { Value: 0 } })
                {
                    target.HoldCoat();
                }

                return;
            }

            if (target.Variant is IBubbleMergeReactive targetCue)
            {
                // One hold for each bubble it is given, each let go as that bubble lands.
                handout.TargetCues[direction] = targetCue;
                handout.TargetBindings[direction] = targetCue.Binding;
                targetCue.HoldCount();
            }
        }

        private void AdvanceHandouts(float deltaTime)
        {
            for (var i = _handouts.Count - 1; i >= 0; i--)
            {
                var handout = _handouts[i];
                if (AdvanceHandout(handout, deltaTime))
                {
                    continue;
                }

                _handouts.RemoveAt(i);
                handout.Clear();
                _freeHandouts.Push(handout);
            }
        }

        // False once every bubble has left.
        private bool AdvanceHandout(Handout handout, float deltaTime)
        {
            handout.Elapsed += deltaTime;
            var step = _settings.FoamStepSeconds;
            while (handout.Next < handout.Bubbles
                   && handout.Elapsed >= RipeBubbleRule.FoamDepartSeconds(handout.Next, step))
            {
                Depart(handout, handout.Next);
                handout.Next++;
            }

            return handout.Next < handout.Bubbles;
        }

        // One bubble leaves the cluster, in its look and its warning colour, for its taker. The cluster's view
        // goes back to the pool as the last one leaves; if it has already gone, the bubble still flies from where
        // it was.
        private void Depart(Handout handout, int bubble)
        {
            var source = handout.Cue.BubbleRenderer;
            if (handout.ClusterAlive)
            {
                handout.From = handout.Cluster.transform.position;
                handout.Cue.StepCount(-1);
                handout.Cue.Wobble(_hopSettings.GiverWobble, _hopSettings.WobbleSeconds);
                if (bubble == handout.Bubbles - 1)
                {
                    handout.Cluster.Hide();
                }
            }

            var gift = RipeBubbleBurstMessage.HandoutGift(handout.Gifts, bubble, out var direction);
            var side = bubble % 2 == 0 ? 1f : -1f;
            if (gift == FoamGift.Leftover)
            {
                var leftover = _bubbles.RentLike(source, source.sortingOrder + _hopSettings.SortingOrderOffset);
                var drift = Random.insideUnitCircle.normalized * (LeftoverReach * _slotSpacing);
                var hop = FoamHop.Leftover(leftover, handout.From, handout.From + (Vector3)drift, handout.Scale);
                PlaceLeftover(in hop, 0f);
                _hops.Add(hop);
                return;
            }

            var target = handout.Targets[direction];
            var model = handout.Models[direction];
            if (target == null || !ReferenceEquals(target.Model, model))
            {
                return;
            }

            if (gift == FoamGift.Coat)
            {
                var order = Mathf.Max(source.sortingOrder, target.CoatSortingOrder) + _hopSettings.SortingOrderOffset;
                var coatBubble = _bubbles.RentLike(source, order);
                var coatHop = FoamHop.Coat(coatBubble, target, model, handout.From, handout.Scale, side);
                SoapBubbleHop.Place(coatBubble.transform, handout.From, target.transform.position, side,
                    _hopSettings.ArcHeight, handout.Scale, 0f);
                _hops.Add(coatHop);
                return;
            }

            var cue = handout.TargetCues[direction];
            if (cue == null || cue.Binding != handout.TargetBindings[direction])
            {
                return;
            }

            if (cue.BubbleRenderer == null)
            {
                cue.ReleaseCount();
                return;
            }

            var clusterOrder = Mathf.Max(source.sortingOrder, cue.BubbleRenderer.sortingOrder)
                               + _hopSettings.SortingOrderOffset;
            var clusterBubble = _bubbles.RentLike(source, clusterOrder);
            var clusterHop = FoamHop.Cluster(clusterBubble, target, cue, handout.TargetBindings[direction],
                handout.From, handout.Scale, side);
            SoapBubbleHop.Place(clusterBubble.transform, handout.From, target.transform.position, side,
                _hopSettings.ArcHeight, handout.Scale, 0f);
            _hops.Add(clusterHop);
        }

        // False once the hop is over: landed, puffed away, or dropped because its taker went.
        private bool AdvanceHop(ref FoamHop hop, float deltaTime)
        {
            hop.Elapsed += deltaTime;
            if (hop.Gift == FoamGift.Leftover)
            {
                return AdvanceLeftover(in hop);
            }

            var alive = hop.Gift == FoamGift.Coat
                ? ReferenceEquals(hop.Target.Model, hop.Model)
                : hop.Cue.Binding == hop.Binding;
            if (!alive)
            {
                _bubbles.Return(hop.Bubble);
                return false;
            }

            var t = _hopSettings.HopSeconds > 0f ? hop.Elapsed / _hopSettings.HopSeconds : 1f;
            if (t >= 1f)
            {
                Land(in hop);
                _bubbles.Return(hop.Bubble);
                return false;
            }

            SoapBubbleHop.Place(hop.Bubble.transform, hop.From, hop.Target.transform.position, hop.Side,
                _hopSettings.ArcHeight, hop.Scale, t);
            return true;
        }

        // A coat shows; a cluster takes its bubble as a merge's receiver does: one more shown, a wobble, a splash.
        private void Land(in FoamHop hop)
        {
            if (hop.Gift == FoamGift.Coat)
            {
                hop.Target.ReleaseCoat();
                return;
            }

            hop.Cue.StepCount(1);
            hop.Cue.Wobble(_hopSettings.ReceiverWobble, _hopSettings.WobbleSeconds);
            var prefab = _balloons.DefaultPopVfxPrefab;
            if (prefab != null && _hopSettings.SplashScale > 0f)
            {
                // The pool restores the authored scale on spawn, so scaling the spawned splash never leaks.
                _poolManager.PlayParticle(prefab, hop.Target.transform.position, _hopSettings.SplashColor)
                    .transform.localScale *= _hopSettings.SplashScale;
            }

            hop.Cue.ReleaseCount();
        }

        // False once it has puffed away.
        private bool AdvanceLeftover(in FoamHop hop)
        {
            var driftSeconds = Mathf.Max(1e-4f, _hopSettings.HopSeconds * LeftoverDriftShare);
            var puffSeconds = Mathf.Max(1e-4f, _settings.CoatPuffSeconds);
            var t = hop.Elapsed / (driftSeconds + puffSeconds);
            if (t >= 1f)
            {
                _bubbles.Return(hop.Bubble);
                return false;
            }

            PlaceLeftover(in hop, hop.Elapsed);
            return true;
        }

        // Drifts out, easing to a stop, then swells and fades as a coat taken off does.
        private void PlaceLeftover(in FoamHop hop, float elapsed)
        {
            var driftSeconds = Mathf.Max(1e-4f, _hopSettings.HopSeconds * LeftoverDriftShare);
            var drift = Mathf.Clamp01(elapsed / driftSeconds);
            var eased = 1f - ((1f - drift) * (1f - drift));
            var body = hop.Bubble.transform;
            body.position = Vector3.Lerp(hop.From, hop.To, eased);
            var puff = Mathf.Clamp01((elapsed - driftSeconds) / Mathf.Max(1e-4f, _settings.CoatPuffSeconds));
            body.localScale = hop.Scale * Mathf.Lerp(1f, _settings.CoatPuffScale, puff);
            var color = hop.Bubble.color;
            color.a = 1f - puff;
            hop.Bubble.color = color;
        }

        // A class, pooled: its bubbles read the centre it holds for as long as they fly.
        private sealed class SpiralSource
        {
            public BalloonView View;
            public IBalloonModel Model;
            public Vector3 Centre;
            public float Turn;
            public float Angle;
            public float Remaining;
            public float SinceBubble;
            public int Flying;
            public bool Fast;

            public void Start(BalloonView view, float angle, float turn, float remaining, bool fast)
            {
                View = view;
                Model = view.Model;
                Centre = view.transform.position;
                Angle = angle;
                Turn = turn;
                Remaining = remaining;
                SinceBubble = 0f;
                Flying = 0;
                Fast = fast;
            }

            public void Clear()
            {
                View = null;
                Model = null;
            }
        }

        // One cluster's hand-out, pooled: the cluster that lets its bubbles go, and each direction's taker as it
        // was when the hand-out began. The cluster counts as gone once its binding changed (handed back to the
        // pool), which dropped the count it held.
        private sealed class Handout
        {
            public readonly Vector2Int[] Slots = new Vector2Int[RipeBubbleBurstMessage.MaxHandout];
            public readonly BalloonView[] Targets = new BalloonView[RipeBubbleBurstMessage.MaxHandout];
            public readonly IBalloonModel[] Models = new IBalloonModel[RipeBubbleBurstMessage.MaxHandout];
            public readonly IBubbleMergeReactive[] TargetCues = new IBubbleMergeReactive[RipeBubbleBurstMessage.MaxHandout];
            public readonly int[] TargetBindings = new int[RipeBubbleBurstMessage.MaxHandout];
            public BalloonView Cluster;
            public IBubbleMergeReactive Cue;
            public int Binding;
            public Vector3 From;
            public Vector3 Scale;
            public int Gifts;
            public int Bubbles;
            public int Next;
            public float Elapsed;

            public bool ClusterAlive => Cue != null && Cue.Binding == Binding;

            public void Start(BalloonView cluster, IBubbleMergeReactive cue, Vector3 from, int gifts, int bubbles,
                Vector3 scale)
            {
                Cluster = cluster;
                Cue = cue;
                Binding = cue.Binding;
                From = from;
                Gifts = gifts;
                Bubbles = bubbles;
                Scale = scale;
                Next = 0;
                Elapsed = 0f;
            }

            public void Clear()
            {
                Cluster = null;
                Cue = null;
                System.Array.Clear(Targets, 0, Targets.Length);
                System.Array.Clear(Models, 0, Models.Length);
                System.Array.Clear(TargetCues, 0, TargetCues.Length);
            }
        }

        private struct SpiralBubble
        {
            public readonly SpriteRenderer Bubble;
            public readonly SpiralSource Source;
            public readonly float Angle;
            public readonly float Scale;
            public float Elapsed;

            public SpiralBubble(SpriteRenderer bubble, SpiralSource source, float angle, float scale)
            {
                Bubble = bubble;
                Source = source;
                Angle = angle;
                Scale = scale;
                Elapsed = 0f;
            }
        }

        private struct Ring
        {
            public readonly SpriteRenderer Bubble;
            public readonly float FromScale;
            public readonly float ToScale;
            public float Elapsed;

            public Ring(SpriteRenderer bubble, float fromScale, float toScale)
            {
                Bubble = bubble;
                FromScale = fromScale;
                ToScale = toScale;
                Elapsed = 0f;
            }
        }

        // One handed-out bubble in flight. The taker's model (a coat) or binding (a cluster) is kept beside its view:
        // a view handed back to the pool and bound again is another balloon.
        private struct FoamHop
        {
            public readonly FoamGift Gift;
            public readonly SpriteRenderer Bubble;
            public readonly BalloonView Target;
            public readonly IBalloonModel Model;
            public readonly IBubbleMergeReactive Cue;
            public readonly int Binding;
            public readonly Vector3 From;
            public readonly Vector3 To;
            public readonly Vector3 Scale;
            public readonly float Side;
            public float Elapsed;

            private FoamHop(FoamGift gift, SpriteRenderer bubble, BalloonView target, IBalloonModel model,
                IBubbleMergeReactive cue, int binding, Vector3 from, Vector3 to, Vector3 scale, float side)
            {
                Gift = gift;
                Bubble = bubble;
                Target = target;
                Model = model;
                Cue = cue;
                Binding = binding;
                From = from;
                To = to;
                Scale = scale;
                Side = side;
                Elapsed = 0f;
            }

            public static FoamHop Coat(SpriteRenderer bubble, BalloonView target, IBalloonModel model, Vector3 from,
                Vector3 scale, float side)
            {
                return new FoamHop(FoamGift.Coat, bubble, target, model, null, 0, from, from, scale, side);
            }

            public static FoamHop Cluster(SpriteRenderer bubble, BalloonView target, IBubbleMergeReactive cue,
                int binding, Vector3 from, Vector3 scale, float side)
            {
                return new FoamHop(FoamGift.Cluster, bubble, target, null, cue, binding, from, from, scale, side);
            }

            public static FoamHop Leftover(SpriteRenderer bubble, Vector3 from, Vector3 to, Vector3 scale)
            {
                return new FoamHop(FoamGift.Leftover, bubble, null, null, null, 0, from, to, scale, 0f);
            }
        }
    }
}
