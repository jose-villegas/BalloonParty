using System.Collections.Generic;
using BalloonParty.Item.Preview;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Item
{
    // ItemPreviewSightOrder.OrderAlongTrace is the pure ordering step ItemRangePreviewController relies on
    // to sequence several item hosts along one prediction line — factored out, like ItemPreviewEntry's own
    // helpers, so the interesting case (a bent trace) is edit-mode testable without the controller's
    // grid/pool machinery.
    [TestFixture]
    public class ItemPreviewSightOrderTests
    {
        [Test]
        public void OrderAlongTrace_EmptyList_DoesNothing()
        {
            var hosts = new List<ItemPreviewSightedHost>();
            var trace = new List<Vector3> { new(0f, 0f, 0f), new(10f, 0f, 0f) };

            Assert.DoesNotThrow(() => ItemPreviewSightOrder.OrderAlongTrace(hosts, trace));
            Assert.AreEqual(0, hosts.Count);
        }

        [Test]
        public void OrderAlongTrace_SingleHost_StaysAlone()
        {
            var hosts = new List<ItemPreviewSightedHost> { MakeHost(new Vector2Int(1, 1), new Vector2(5f, 0f)) };
            var trace = new List<Vector3> { new(0f, 0f, 0f), new(10f, 0f, 0f) };

            ItemPreviewSightOrder.OrderAlongTrace(hosts, trace);

            Assert.AreEqual(1, hosts.Count);
            Assert.AreEqual(new Vector2Int(1, 1), hosts[0].Slot);
        }

        // Straight trace, reverse input: the farthest-along host is listed first, so a naive "keep input
        // order" bug would leave this failing.
        [Test]
        public void OrderAlongTrace_StraightTrace_OrdersFirstToLastAlongLine()
        {
            var far = MakeHost(new Vector2Int(9, 0), new Vector2(9f, 0f));
            var near = MakeHost(new Vector2Int(2, 0), new Vector2(2f, 0f));
            var hosts = new List<ItemPreviewSightedHost> { far, near };
            var trace = new List<Vector3> { new(0f, 0f, 0f), new(10f, 0f, 0f) };

            ItemPreviewSightOrder.OrderAlongTrace(hosts, trace);

            Assert.AreEqual(new Vector2Int(2, 0), hosts[0].Slot, "the nearer-along-the-line host comes first");
            Assert.AreEqual(new Vector2Int(9, 0), hosts[1].Slot);
        }

        // The interesting case: a bent trace where a host spatially CLOSE to the trace's own start point
        // still sits LATE in true arc-length order, because its nearest point on the polyline actually
        // falls on the far leg, past the bend. A naive "sort by distance from the trace's first point"
        // would put this host first; the correct arc-offset order (via TryFindNearestPointOnPolyline)
        // puts it last — exactly the deflected-aim case the ordering exists to get right.
        [Test]
        public void OrderAlongTrace_BentTrace_OrdersByArcOffsetNotStraightLineDistance()
        {
            // Leg 1: (0,0) -> (10,0), length 10.
            // Leg 2: (10,0) -> (0,-1), length ~10.05 — bends back toward the trace's own start point.
            var trace = new List<Vector3>
            {
                new(0f, 0f, 0f),
                new(10f, 0f, 0f),
                new(0f, -1f, 0f),
            };

            // Sits partway along leg 1 — an early arc offset (~3), 3 units from the trace's start.
            var early = MakeHost(new Vector2Int(3, 0), new Vector2(3f, 0f));

            // Sits right next to leg 2's own end point — under 1 unit from the trace's start point (0,0)
            // in straight-line terms, but its nearest point on the polyline is near the far end of leg 2,
            // giving it a late arc offset (~20).
            var late = MakeHost(new Vector2Int(0, -1), new Vector2(0f, -0.9f));

            var hosts = new List<ItemPreviewSightedHost> { late, early };

            ItemPreviewSightOrder.OrderAlongTrace(hosts, trace);

            Assert.AreEqual(new Vector2Int(3, 0), hosts[0].Slot, "early along leg 1 must sequence first");
            Assert.AreEqual(
                new Vector2Int(0, -1), hosts[1].Slot,
                "close to the trace's start point spatially, but late along the bent path, must sequence last");
        }

        private static ItemPreviewSightedHost MakeHost(Vector2Int slot, Vector2 origin)
        {
            return new ItemPreviewSightedHost(slot, preview: null, origin, direction: Vector2.up);
        }
    }
}
