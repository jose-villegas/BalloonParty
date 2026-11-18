using System.Collections.Generic;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     Plays <see cref="BubbleMergedMessage" />: the bubbles a giver passes on hop to the receiver one after another,
    ///     each a single bubble drawn with the cluster's own material. The giver drops a bubble and wobbles as each
    ///     leaves; the receiver gains one, wobbles and splashes as each lands. Both clusters hold their shown counts
    ///     (<see cref="IBubbleMergeReactive" />) until the last bubble lands, as the models changed at once. A giver
    ///     absorbed whole flows in on its own (<c>BalloonController.AbsorbInto</c>); the receiver takes it in when it
    ///     arrives. A hop follows its receiver if it moves, and is dropped if the receiver leaves the board.
    /// </summary>
    internal sealed class BubbleMergeView : MonoBehaviour
    {
        private readonly List<Hop> _hops = new();
        private readonly Stack<MergeEnds> _freeEnds = new();
        private readonly CompositeDisposable _subscriptions = new();

        private SlotGrid _grid;
        private IBalloonsConfiguration _balloons;
        private IBubbleMergeCueSettings _settings;
        private PoolManager _poolManager;
        private SoapBubbleSprites _bubbles;

        private void Update()
        {
            // Scaled time: a pause holds the hops where they are.
            var deltaTime = Time.deltaTime;
            for (var i = _hops.Count - 1; i >= 0; i--)
            {
                var hop = _hops[i];
                if (Advance(ref hop, deltaTime))
                {
                    _hops[i] = hop;
                    continue;
                }

                _hops.RemoveAt(i);
            }
        }

        private void OnDestroy()
        {
            _subscriptions.Dispose();
        }

        [Inject]
        internal void Construct(ISubscriber<BubbleMergedMessage> mergedSubscriber, SlotGrid grid,
            IBalloonsConfiguration balloons, PoolManager poolManager)
        {
            _grid = grid;
            _balloons = balloons;
            _settings = balloons.BubbleMergeCue;
            _poolManager = poolManager;
            _bubbles = new SoapBubbleSprites(transform, "MergeBubble");
            mergedSubscriber.Subscribe(OnMerged).AddTo(_subscriptions);
        }

        // Published before the counts change, so both clusters are still in their slots showing their old counts.
        private void OnMerged(BubbleMergedMessage message)
        {
            var receiver = _grid.ActorViewAt<BalloonView>(message.ReceiverSlot);
            if (message.Given <= 0 || receiver == null || receiver.Variant is not IBubbleMergeReactive receiverCue)
            {
                return;
            }

            var ends = _freeEnds.Count > 0 ? _freeEnds.Pop() : new MergeEnds();
            ends.Receiver = receiver;
            ends.ReceiverCue = receiverCue;
            ends.ReceiverBinding = receiverCue.Binding;
            ends.LastTo = message.To;
            receiverCue.HoldCount();

            if (message.Absorbed)
            {
                // The giver's own slide is the hop; the receiver takes all it gave once it arrives.
                ends.HopsLeft = 1;
                _hops.Add(new Hop(ends, _balloons.BubbleMergeSeconds, message.Given, 0f, message.From, flies: false));
                return;
            }

            var giver = _grid.ActorViewAt<BalloonView>(message.GiverSlot);
            if (giver != null && giver.Variant is IBubbleMergeReactive giverCue)
            {
                ends.Giver = giver;
                ends.GiverCue = giverCue;
                ends.GiverBinding = giverCue.Binding;
                giverCue.HoldCount();
            }

            ends.HopsLeft = message.Given;
            for (var i = 0; i < message.Given; i++)
            {
                var side = i % 2 == 0 ? 1f : -1f;
                _hops.Add(new Hop(ends, i * _settings.HopStagger, 1, side, message.From, flies: true));
            }
        }

        // False once the hop is over.
        private bool Advance(ref Hop hop, float deltaTime)
        {
            var ends = hop.Ends;
            if (!ends.ReceiverAlive)
            {
                Finish(ref hop);
                return false;
            }

            if (!hop.Departed)
            {
                hop.Delay -= deltaTime;
                if (hop.Delay > 0f)
                {
                    return true;
                }

                Depart(ref hop);
                // The time past the departure counts toward the flight.
                deltaTime = -hop.Delay;
            }

            ends.LastTo = ends.Receiver.transform.position;
            if (!hop.Flies)
            {
                Land(ref hop);
                return false;
            }

            hop.Elapsed += deltaTime;
            var t = Mathf.Clamp01(hop.Elapsed / _settings.HopSeconds);
            if (t >= 1f)
            {
                Land(ref hop);
                return false;
            }

            PlaceBubble(in hop, t);
            return true;
        }

        private void Depart(ref Hop hop)
        {
            hop.Departed = true;
            if (!hop.Flies)
            {
                return;
            }

            var ends = hop.Ends;
            if (ends.GiverAlive)
            {
                hop.From = ends.Giver.transform.position;
                ends.GiverCue.StepCount(-1);
                ends.GiverCue.Wobble(_settings.GiverWobble, _settings.WobbleSeconds);
            }

            hop.Bubble = RentBubble(ends);
            if (hop.Bubble != null)
            {
                hop.Scale = hop.Bubble.transform.localScale;
            }
        }

        private void PlaceBubble(in Hop hop, float t)
        {
            if (hop.Bubble == null)
            {
                return;
            }

            SoapBubbleHop.Place(
                hop.Bubble.transform, hop.From, hop.Ends.LastTo, hop.Side, _settings.ArcHeight, hop.Scale, t);
        }

        private void Land(ref Hop hop)
        {
            var ends = hop.Ends;
            ends.ReceiverCue.StepCount(hop.Bubbles);
            ends.ReceiverCue.Wobble(_settings.ReceiverWobble, _settings.WobbleSeconds);
            var prefab = _balloons.DefaultPopVfxPrefab;
            if (prefab != null && _settings.SplashScale > 0f)
            {
                // The pool restores the authored scale on spawn, so scaling the spawned splash never leaks.
                _poolManager.PlayParticle(prefab, ends.LastTo, _settings.SplashColor).transform.localScale *=
                    _settings.SplashScale;
            }

            Finish(ref hop);
        }

        private void Finish(ref Hop hop)
        {
            _bubbles.Return(hop.Bubble);
            hop.Bubble = null;
            var ends = hop.Ends;
            ends.HopsLeft--;
            if (ends.HopsLeft > 0)
            {
                return;
            }

            if (ends.ReceiverAlive)
            {
                ends.ReceiverCue.ReleaseCount();
            }

            if (ends.GiverAlive)
            {
                ends.GiverCue.ReleaseCount();
            }

            ends.Clear();
            _freeEnds.Push(ends);
        }

        // A single bubble in the cluster's own look: its renderer's material, sprite, tint and clock, at one bubble.
        private SpriteRenderer RentBubble(MergeEnds ends)
        {
            var sourceCue = ends.GiverAlive ? ends.GiverCue : ends.ReceiverCue;
            var source = sourceCue.BubbleRenderer;
            if (source == null)
            {
                return null;
            }

            var target = ends.ReceiverCue.BubbleRenderer;
            var topOrder = target != null ? Mathf.Max(source.sortingOrder, target.sortingOrder) : source.sortingOrder;
            var bubble = _bubbles.RentLike(source, topOrder + _settings.SortingOrderOffset);
            bubble.transform.localScale = sourceCue.BubbleWorldScale * _settings.BubbleScale;
            return bubble;
        }

        // The two clusters one merge joins, shared by its hops. A cluster counts as gone once its binding changed
        // (handed back to the pool, or frozen for a level transition), which dropped the holds this merge took.
        private sealed class MergeEnds
        {
            public BalloonView Giver;
            public IBubbleMergeReactive GiverCue;
            public int GiverBinding;
            public BalloonView Receiver;
            public IBubbleMergeReactive ReceiverCue;
            public int ReceiverBinding;
            public Vector3 LastTo;
            public int HopsLeft;

            public bool GiverAlive => GiverCue != null && GiverCue.Binding == GiverBinding;
            public bool ReceiverAlive => ReceiverCue != null && ReceiverCue.Binding == ReceiverBinding;

            public void Clear()
            {
                Giver = null;
                GiverCue = null;
                Receiver = null;
                ReceiverCue = null;
            }
        }

        private struct Hop
        {
            public readonly MergeEnds Ends;
            public readonly int Bubbles;
            public readonly float Side;
            public readonly bool Flies;
            public float Delay;
            public float Elapsed;
            public bool Departed;
            public Vector3 From;
            public Vector3 Scale;
            public SpriteRenderer Bubble;

            public Hop(MergeEnds ends, float delay, int bubbles, float side, Vector3 from, bool flies)
            {
                Ends = ends;
                Delay = delay;
                Bubbles = bubbles;
                Side = side;
                From = from;
                Flies = flies;
                Elapsed = 0f;
                Departed = false;
                Scale = Vector3.one;
                Bubble = null;
            }
        }
    }
}
