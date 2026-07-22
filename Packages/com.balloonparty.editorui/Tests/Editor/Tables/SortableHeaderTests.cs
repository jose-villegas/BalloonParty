using System;
using System.Collections.Generic;
using BalloonParty.EditorUI.Tables;
using NUnit.Framework;

namespace BalloonParty.EditorUI.Tests.Tables
{
    [TestFixture]
    public class SortableHeaderTests
    {
        [Test]
        public void ApplySort_ColumnNegative_LeavesListUnchanged()
        {
            var items = new List<SortRow>
            {
                new SortRow("b", 2),
                new SortRow("a", 1)
            };

            var state = new SortState
            {
                Column = -1,
                Ascending = true
            };

            SortableHeader.ApplySort(items, state, (_, _, _) => throw new InvalidOperationException("Should not compare."));

            Assert.That(items[0].Id, Is.EqualTo("b"));
            Assert.That(items[1].Id, Is.EqualTo("a"));
        }

        [Test]
        public void ApplySort_Ascending_SortsBySelectedColumn()
        {
            var items = new List<SortRow>
            {
                new SortRow("c", 3),
                new SortRow("a", 1),
                new SortRow("b", 2)
            };

            SortableHeader.ApplySort(items, CreateState(0, true), CompareByColumn);

            Assert.That(GetIds(items), Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void ApplySort_Descending_SortsBySelectedColumnDescending()
        {
            var items = new List<SortRow>
            {
                new SortRow("a", 1),
                new SortRow("c", 3),
                new SortRow("b", 2)
            };

            SortableHeader.ApplySort(items, CreateState(0, false), CompareByColumn);

            Assert.That(GetIds(items), Is.EqualTo(new[] { "c", "b", "a" }));
        }

        [Test]
        public void ApplySort_EqualKeys_KeepsOriginalOrderForTiedItems()
        {
            var items = new List<SortRow>
            {
                new SortRow("first", 1),
                new SortRow("second", 1),
                new SortRow("third", 2)
            };

            SortableHeader.ApplySort(items, CreateState(1, true), CompareByColumn);

            Assert.That(GetIds(items), Is.EqualTo(new[] { "first", "second", "third" }));
        }

        [Test]
        public void ApplySort_EmptyList_DoesNotThrow()
        {
            var items = new List<SortRow>();

            Assert.DoesNotThrow(() => SortableHeader.ApplySort(items, CreateState(0, true), CompareByColumn));
        }

        private static SortState CreateState(int column, bool ascending)
        {
            return new SortState
            {
                Column = column,
                Ascending = ascending
            };
        }

        private static int CompareByColumn(int column, SortRow a, SortRow b)
        {
            return column switch
            {
                0 => string.CompareOrdinal(a.Id, b.Id),
                1 => a.Key.CompareTo(b.Key),
                _ => 0
            };
        }

        private static string[] GetIds(List<SortRow> items)
        {
            var ids = new string[items.Count];

            for (var i = 0; i < items.Count; i++)
            {
                ids[i] = items[i].Id;
            }

            return ids;
        }

        private sealed class SortRow
        {
            public SortRow(string id, int key)
            {
                Id = id;
                Key = key;
            }

            public string Id { get; }

            public int Key { get; }
        }
    }
}
