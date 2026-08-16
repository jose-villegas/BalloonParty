using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon;
using BalloonParty.Configuration;
using BalloonParty.Item.Preview;
using BalloonParty.Prediction;
using BalloonParty.Shared.Pool;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    // Regression coverage for the standing rule this subsystem has broken six times, each time caught by
    // manual reading or a user noticing a glitch, never by a test: a pen must never be emitting at a
    // position it is about to leave discontinuously. Drives the REAL production graph — PoolManager,
    // HighlightTrail/TrailRenderer, ItemPreviewViewport, BalloonContactRadii and ItemPreviewTicker itself
    // — through Tick(float), the deltaTime-parameterized seam split out of LateTick specifically so this
    // could be exercised deterministically. Uses ShieldRangePreview (a real IItemRangePreview) rather than
    // a test-local stand-in: its precondition (TraceEnd.Kind == Wall, a two-point trace with a non-zero
    // incoming leg and normal) is trivial to satisfy directly, and a hand-built fake preview would be one
    // more thing that could itself be wrong about the shape contract in a way that hid a real bug.
    [TestFixture]
    public class ItemPreviewTickerStandingRuleTests
    {
        // Fine enough that no single tick's worth of ordinary cascade/dash travel is ever mistaken for a
        // teleport (see AssertNoTeleportWhileLit's own threshold), coarse enough that covering a full
        // draw-in + hold + ribbon fade doesn't take an unreasonable number of scripted ticks.
        private const float DeltaTime = 1f / 60f;

        // A generous multiple of the fastest a pen legitimately travels in one tick while lit — a Bloomed
        // pen sweeps at TraceSpeed * DeltaTime, and a cascading pen's rising-edge step is bounded by its
        // own (much shorter) dash window — so ordinary motion never trips this, only a genuine snap to an
        // unrelated position does. Mirrors the SHAPE of ItemPreviewTicker's own private teleport guard
        // (TeleportSpeedMultiplier/MinTeleportDistance) without reaching into its internals: this test
        // asserts the OUTCOME the guard exists to prevent, not the guard's own implementation constants.
        private const float TeleportMultiplier = 4f;
        private const float MinTeleportDistance = 0.05f;

        private readonly List<GameObject> _objects = new();
        private readonly Dictionary<HighlightTrail, PenObservation> _penHistory = new();

        private PoolManager _poolManager;
        private HighlightTrail _penPrefab;
        private ItemPreviewConfig _config;
        private ItemPreviewViewport _viewport;
        private BalloonContactRadii _balloonRadii;
        private ItemPreviewTicker _ticker;
        private ShieldRangePreview _preview;

        [SetUp]
        public void SetUp()
        {
            _poolManager = new PoolManager();

            var prefabGo = new GameObject("pen-prefab");
            _objects.Add(prefabGo);
            _penPrefab = prefabGo.AddComponent<HighlightTrail>();

            // Wired explicitly through HighlightTrail.Configure rather than left to Awake, the same way
            // LaserItemRotation (the other pooled component in this feature) is never DI-injected and gets
            // its dependencies handed in after spawn — Awake genuinely does run for a real spawned prefab
            // in actual play, but a plain synchronous [Test] EditMode method never crosses an engine tick,
            // so nothing here should depend on Unity's own lifecycle timing at all. RequireComponent still
            // guarantees the sibling TrailRenderer exists on this GameObject as a structural side effect of
            // AddComponent itself, independent of whether Awake ever runs.
            //
            // Object.Instantiate clones this already-configured template for every pooled pen and remaps
            // same-hierarchy serialized references (like _trailRenderer) to each clone's own components as
            // part of the clone operation itself — not through Awake — so configuring it here once is what
            // makes every spawned pen's own _trailRenderer correct too, not just this template's.
            _penPrefab.Configure(prefabGo.GetComponent<TrailRenderer>());

            _config = ScriptableObject.CreateInstance<ItemPreviewConfig>();
            _viewport = new ItemPreviewViewport(_config);
            _balloonRadii = new BalloonContactRadii(null, 0.3f);
            _ticker = new ItemPreviewTicker(_poolManager, _penPrefab, _config, _viewport, _balloonRadii);
            _preview = new ShieldRangePreview(_config);
        }

        [TearDown]
        public void TearDown()
        {
            _ticker.Hide();
            _penHistory.Clear();

            // Pooled pen instances live under this container (see PoolManager.Root) — cleaning it up is
            // what stops one test's pens from being findable (and mistaken for live pens) by the next.
            var poolRoot = GameObject.Find("[Pool]");
            if (poolRoot != null)
            {
                Object.DestroyImmediate(poolRoot);
            }

            foreach (var go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
            Object.DestroyImmediate(_config);
        }

        // Drives a real Shield figure — one open two-point stroke, the simplest non-empty shape a real
        // IItemRangePreview produces — through a full draw-in, a full settled hold, the loop's own fade,
        // and a restart, asserting after every single Tick (and after every Show, since Show repositions
        // pens synchronously too) that no pen was ever caught lit at a position it had just discontinuously
        // left. This is a property test over the whole sequence, not one assertion at one frame — a
        // targeted single-frame check would only catch a regression landing at that exact frame, which is
        // not what actually would have caught any of the six past regressions.
        [Test]
        public void PenNeverEmitsAtADiscontinuousPosition_AcrossDrawInHoldFadeAndRestart()
        {
            var context = BuildContext();

            Assert.IsTrue(
                _ticker.Show(_preview, in context, introduce: true),
                "Shield's stub must build a real figure for this synthetic wall-hit trace.");
            AssertNoTeleportWhileLit();

            RunTicks(TicksFor(_config.BloomDuration));
            RunTicks(TicksFor(_config.RebloomHoldSeconds));
            RunTicks(TicksFor(_penPrefab.EffectiveRibbonSeconds));

            Assert.IsTrue(
                _ticker.CycleComplete,
                "The scripted sequence should comfortably outlast one full draw+hold+fade cycle.");

            // Restart: exactly what ItemRangePreviewController does once CycleComplete fires — re-Show the
            // same host. This is the moment ResetPenForDrawIn snaps every pen back to the origin; the
            // standing rule is exactly what stops that snap from ever being observed while still lit.
            Assert.IsTrue(_ticker.Show(_preview, in context, introduce: true));
            AssertNoTeleportWhileLit();

            RunTicks(TicksFor(_config.BloomDuration));
        }

        private void RunTicks(int count)
        {
            for (var i = 0; i < count; i++)
            {
                _ticker.Tick(DeltaTime);
                AssertNoTeleportWhileLit();
            }
        }

        // Ticks enough to comfortably clear `seconds` of scripted time regardless of the config/prefab
        // defaults in play, rather than a hard-coded count tied to today's authored values.
        private static int TicksFor(float seconds)
        {
            return Mathf.CeilToInt(seconds / DeltaTime) + 5;
        }

        private ItemPreviewContext BuildContext()
        {
            var tracePoints = new List<Vector3> { new(0f, 0f, 0f), new(3f, 0f, 0f) };
            var traceEnd = new PredictionTraceEnd(0, PredictionTraceEndKind.Wall, new Vector2(0f, 1f));

            // Origin deliberately coincides with the trace's own end (and so with the stub's own entry
            // point) — Shield's telegraph launches from the wall contact, not from a balloon elsewhere on
            // the board, so there is no separate approach leg to script here; the figure's own cascade
            // already exercises everything this rule cares about.
            return new ItemPreviewContext(
                new Vector2(3f, 0f), new Vector2Int(0, 0), new Vector2(1f, 0f), tracePoints, null, 0f,
                traceEnd, _viewport, default);
        }

        // The property itself: every pen currently in the scene (excluding the prefab template, which the
        // ticker never drives directly) must not read emitting == true on any observed tick after moving
        // more than a frame's worth of ordinary travel from where this same pen was last observed.
        private void AssertNoTeleportWhileLit()
        {
            var threshold = Mathf.Max(_config.TraceSpeed * DeltaTime * TeleportMultiplier, MinTeleportDistance);
            var thresholdSq = threshold * threshold;

            foreach (var pen in FindActivePens())
            {
                var position = pen.transform.position;
                var emitting = pen.IsEmittingForTest;

                if (_penHistory.TryGetValue(pen, out var previous))
                {
                    var moved = (position - previous.Position).sqrMagnitude > thresholdSq;
                    Assert.IsFalse(
                        emitting && moved,
                        $"Pen at {position} read emitting=true after moving " +
                        $"{(position - previous.Position).magnitude:F3} units from its last observed " +
                        $"position {previous.Position} — a lit pen must never be caught at a position it " +
                        "just discontinuously left.");
                }

                _penHistory[pen] = new PenObservation(position, emitting);
            }
        }

        private List<HighlightTrail> FindActivePens()
        {
            return Object.FindObjectsByType<HighlightTrail>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(pen => pen != _penPrefab)
                .ToList();
        }

        private readonly struct PenObservation
        {
            public readonly Vector3 Position;
            public readonly bool Emitting;

            public PenObservation(Vector3 position, bool emitting)
            {
                Position = position;
                Emitting = emitting;
            }
        }
    }
}
