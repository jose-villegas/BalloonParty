using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Formation;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Slots.Actor;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     The ring over the balloon an item cover stands round (<see cref="FormationGlowHosts" />), through the
    ///     glow's own field: the view's path each frame is <c>Sync</c> then <c>FormationGlowField.Step</c>, and the
    ///     tests run the two the same way. The settings are the shipped ones, not a substitute, so the ring is the
    ///     size it is in the game and a frame can be measured for allocations.
    /// </summary>
    [TestFixture]
    public class FormationGlowHostsTests
    {
        private const int Formation = 7;
        private const float Settled = 10f;

        // The board's slot pitch: a cover's members stand this far from the balloon they cover.
        private const float Pitch = 0.75f;

        private static readonly Vector2 HostCentre = new(2f, 3f);

        // A cover's members in the order it takes its slots: the two below, the two sides, the two above.
        private static readonly int[] CoverOrder = { 1, 2, 0, 3, 5, 4 };

        private IFormationGlowSettings _settings;
        private FormationGlowField _field;
        private FormationGlowHosts _hosts;
        private FormationGlowSources _sources;
        private List<CoveredHost> _covered;
        private List<Member> _members;
        private ISlotActor _host;
        private Vector4[] _points;
        private float[] _formations;
        private float _threshold;
        private int _count;

        [SetUp]
        public void SetUp()
        {
            _settings = new FormationGlowSettings();
            _field = new FormationGlowField();
            _hosts = new FormationGlowHosts();
            _sources = new FormationGlowSources();
            _covered = new List<CoveredHost>();
            _members = new List<Member>();
            _host = Substitute.For<ISlotActor>();
            _points = new Vector4[FormationGlowField.MaxPoints];
            _formations = new float[FormationGlowField.MaxPoints];
            _threshold = FormationGlowField.Threshold(_settings.RingRadius, _settings.Reach);
        }

        [Test]
        public void ACoveredBalloon_GetsARingOfTheFormationCoveringIt()
        {
            StandCover(6);
            Cover(_host);

            Frame(Settled);

            Assert.AreEqual(7, _count, "six members and the balloon they cover");
            Assert.AreEqual(Formation, FormationAt(HostCentre));
            Assert.AreEqual(1f, RingAt(HostCentre).w, 1e-4f);
        }

        // Directions as HexCoordinates.Step numbers them: three members are the two below and one side, six the ring.
        [TestCase(6)]
        [TestCase(3)]
        public void TheCoveredBalloonsCentre_IsOutsideTheOutlineWithoutItsRing_AndInsideWithIt(int members)
        {
            StandCover(members);
            Frame(Settled);
            var without = FieldAtTheHost();

            Cover(_host);
            Frame(Settled);
            var with = FieldAtTheHost();

            Assert.That(without, Is.LessThan(_threshold), "the members' rings alone leave the middle open");
            Assert.That(with, Is.GreaterThan(_threshold), "the outline closes over the covered balloon");
        }

        [Test]
        public void TheCoveredBalloonsRing_DissolvesIn_AsAMembersDoes()
        {
            StandCover(6);
            Frame(Settled);

            Cover(_host);
            Frame(_settings.DissolveSeconds * 0.5f);

            Assert.AreEqual(0.5f, RingAt(HostCentre).w, 1e-4f);
            Assert.AreEqual(1f, RingAt(HostCentre).z, 1e-4f, "at full brightness from the start: its members are a slot away");

            Frame(_settings.DissolveSeconds * 0.5f);
            Assert.AreEqual(1f, RingAt(HostCentre).w, 1e-4f);
        }

        [Test]
        public void WithItsMembersStillFarOff_TheCoveredBalloonLightsNoRing()
        {
            AddMember(HostCentre + new Vector2(_settings.FadeDistance + 0.5f, 0f));
            AddMember(HostCentre + new Vector2(_settings.FadeDistance + 0.5f + Pitch, 0f));
            Cover(_host);

            Frame(Settled);

            Assert.AreEqual(2, _count, "only the two members, who light each other");
            Assert.IsFalse(Has(HostCentre));
        }

        // The balloon is a host, and held in its slot, from the cycle its cover is planned, with every member still
        // on its way. Its ring is lit by how close the nearest member has drawn, so it comes in as they arrive.
        [Test]
        public void TheCoveredBalloonsRing_ComesInAsItsMembersArrive_NotBefore()
        {
            StandCover(6);
            MoveMembers(new Vector2(3f, 0f));
            Cover(_host);

            Frame(Settled);
            Assert.IsFalse(Has(HostCentre), "the nearest member is past the fade distance: no ring yet");

            MoveMembers(new Vector2(2.25f, 0f));
            Frame(Settled);
            var onTheWay = RingAt(HostCentre).z;

            MoveMembers(Vector2.zero);
            Frame(Settled);
            var arrived = RingAt(HostCentre).z;

            Assert.That(onTheWay, Is.GreaterThan(0f).And.LessThan(1f), "the nearest is 1.5 off: dimly lit");
            Assert.AreEqual(1f, arrived, 1e-4f, "its members stand beside it: fully lit");
        }

        // The balloon popped, or was taken off the board: the members stand on, and only its ring goes.
        [Test]
        public void TheCoveredBalloonGone_ItsRingDissolvesWhereItWas_AndTheMembersKeepTheirs()
        {
            StandCover(6);
            Cover(_host);
            Frame(Settled);

            _covered.Clear();
            Frame(_settings.DissolveSeconds * 0.5f);

            Assert.AreEqual(7, _count, "still drawn half way");
            Assert.AreEqual(0.5f, RingAt(HostCentre).w, 1e-4f);
            Assert.AreEqual(Formation, FormationAt(HostCentre), "it dissolves as its formation's");

            Frame(_settings.DissolveSeconds * 0.5f);

            Assert.AreEqual(6, _count);
            Assert.IsFalse(Has(HostCentre));
            Assert.That(FieldAtTheHost(), Is.LessThan(_threshold), "the middle is open again");
        }

        // The cover opened: its hold ran out and the formation broke up, so every ring of it dissolves together.
        [Test]
        public void TheCoverBroken_TheCoveredBalloonsRingDissolvesWithItsMembers()
        {
            StandCover(6);
            Cover(_host);
            Frame(Settled);

            _covered.Clear();
            foreach (var member in _members)
            {
                member.InFormation = false;
            }

            Frame(_settings.DissolveSeconds * 0.5f);
            Assert.AreEqual(7, _count);
            Assert.AreEqual(0.5f, RingAt(HostCentre).w, 1e-4f);

            Frame(_settings.DissolveSeconds * 0.5f);
            Assert.AreEqual(0, _count);
        }

        // The cover changed to a line that still stands beside the balloon: the balloon is no member of the line.
        [Test]
        public void TheCoverChangedShape_TheBalloonItCoveredKeepsNoRing()
        {
            StandCover(3);
            Cover(_host);
            Frame(Settled);

            _covered.Clear();
            Frame(Settled);

            Assert.AreEqual(3, _count);
            Assert.AreEqual(3, _sources.All.Count, "its source is taken out, not left listed");
        }

        // A second cover round another balloon takes up the first one's pooled ring before that has dissolved.
        [Test]
        public void ARingTakenUpForAnotherBalloon_LeavesTheOldOneDissolvingWhereItWas()
        {
            StandCover(6);
            Cover(_host);
            Frame(Settled);
            var elsewhere = HostCentre + new Vector2(6f, 0f);
            AddMember(elsewhere + new Vector2(Pitch, 0f), Formation + 1);
            AddMember(elsewhere + new Vector2(-Pitch, 0f), Formation + 1);

            _covered.Clear();
            Frame(0f);
            _covered.Add(new CoveredHost(Substitute.For<ISlotActor>(), Formation + 1, elsewhere));
            Frame(_settings.DissolveSeconds * 0.5f);

            Assert.IsTrue(Has(HostCentre), "the first balloon's ring is still dissolving where it was");
            Assert.AreEqual(Formation, FormationAt(HostCentre));
            Assert.AreEqual(0.5f, RingAt(HostCentre).w, 1e-4f);
            Assert.AreEqual(Formation + 1, FormationAt(elsewhere), "and the second one's grows in round it");
            Assert.AreEqual(0.5f, RingAt(elsewhere).w, 1e-4f);
        }

        // The same balloon covered by another formation the moment the first lets go: a ring of each, one going
        // and one coming, never one ring handed across.
        [Test]
        public void TheSameBalloonUnderAnotherFormation_GetsAFreshRing()
        {
            StandCover(3);
            Cover(_host);
            Frame(Settled);
            AddMember(HostCentre + Around(4), Formation + 1);
            AddMember(HostCentre + Around(5), Formation + 1);

            _covered.Clear();
            _covered.Add(new CoveredHost(_host, Formation + 1, HostCentre));
            Frame(_settings.DissolveSeconds * 0.5f);

            var rings = 0;
            var formations = 0f;
            for (var i = 0; i < _count; i++)
            {
                if (((Vector2)_points[i] - HostCentre).sqrMagnitude < 1e-6f)
                {
                    rings++;
                    formations += _formations[i];
                    Assert.AreEqual(0.5f, _points[i].w, 1e-4f);
                }
            }

            Assert.AreEqual(2, rings);
            Assert.AreEqual(Formation + Formation + 1, formations, 1e-4f, "one ring of each formation");
        }

        [Test]
        public void AStandingCover_KeepsTheOneSource_AndAFrameAllocatesNothing()
        {
            StandCover(6);
            Cover(_host);
            Frame(Settled);
            var source = _sources.All[_sources.All.Count - 1];
            Frame(0.016f);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 200; i++)
            {
                Frame(0.016f);
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated);
            Assert.AreEqual(7, _sources.All.Count);
            Assert.AreSame(source, _sources.All[_sources.All.Count - 1], "the ring is known by its source: it must not change");
            Assert.AreEqual(7, _count);
        }

        // The view's frame: the covered balloons become sources, then the field moves every ring on.
        private void Frame(float seconds)
        {
            _hosts.Sync(_covered, _sources);
            _count = _field.Step(_sources.All, _settings, seconds, _points, _formations, out _);
        }

        private void Cover(ISlotActor host)
        {
            _covered.Add(new CoveredHost(host, Formation, HostCentre));
        }

        private void StandCover(int members)
        {
            for (var i = 0; i < members; i++)
            {
                AddMember(HostCentre + Around(CoverOrder[i]));
            }
        }

        // Every member that far off its slot beside the covered balloon, as while it glides in.
        private void MoveMembers(Vector2 offTheirSlots)
        {
            for (var i = 0; i < _members.Count; i++)
            {
                _members[i].Centre = HostCentre + Around(CoverOrder[i]) + offTheirSlots;
            }
        }

        // A neighbouring slot's offset, by HexCoordinates.Step's directions: 0 east, then clockwise.
        private static Vector2 Around(int direction)
        {
            var angle = -direction * 60f * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Pitch;
        }

        private void AddMember(Vector2 centre, int formation = Formation)
        {
            var member = new Member { Centre = centre, InFormation = true, Formation = formation };
            _members.Add(member);
            _sources.Add(member);
        }

        private float FieldAtTheHost()
        {
            return FormationGlowField.FieldAt(
                _points, _count, HostCentre, _settings.Reach, _threshold, _formations, Formation);
        }

        private bool Has(Vector2 centre)
        {
            return IndexAt(centre) >= 0;
        }

        private Vector4 RingAt(Vector2 centre)
        {
            var index = IndexAt(centre);
            Assert.GreaterOrEqual(index, 0, $"no ring at {centre}");
            return _points[index];
        }

        private int FormationAt(Vector2 centre)
        {
            var index = IndexAt(centre);
            Assert.GreaterOrEqual(index, 0, $"no ring at {centre}");
            return (int)_formations[index];
        }

        private int IndexAt(Vector2 centre)
        {
            for (var i = 0; i < _count; i++)
            {
                if (((Vector2)_points[i] - centre).sqrMagnitude < 1e-6f)
                {
                    return i;
                }
            }

            return -1;
        }

        private sealed class Member : IFormationGlowSource
        {
            public Vector2 Centre;
            public bool InFormation;
            public int Formation;

            public Vector2 GlowCentre => Centre;
            public bool GlowsInFormation => InFormation;
            public int GlowFormation => Formation;
        }
    }
}
