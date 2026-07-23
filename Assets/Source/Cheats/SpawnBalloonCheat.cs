#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE

using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Balloon.Type;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UnityEngine;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Cheats
{
    /// <summary>
    ///     Force-spawns a chosen balloon type into open slots, optionally holding a chosen item, in a
    ///     chosen quantity. Bypasses the weighted spawner and its caps by design — it's a debug tool.
    /// </summary>
    internal class SpawnBalloonCheat : ICheat, ICheatControls
    {
        private static readonly BalloonType[] Types = (BalloonType[])Enum.GetValues(typeof(BalloonType));
        private static readonly ItemType[] Items = (ItemType[])Enum.GetValues(typeof(ItemType));

        private readonly BalloonFactory _factory;
        private readonly SlotGrid _grid;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IPublisher<BalanceBalloonsMessage> _balancePublisher;

        private readonly List<Vector3> _spawnPath = new();

        private int _typeIndex;
        private int _itemIndex;
        private int _count = 1;

        public string Name => "Spawn Balloon";
        public string Section => "Spawning";
        public IReadOnlyList<string> Tags => new[] { "balloons", "spawning", "items" };
        public bool Compact => false;

        public SpawnBalloonCheat(
            BalloonFactory factory,
            SlotGrid grid,
            IBalloonsConfiguration balloonsConfig,
            IPublisher<BalanceBalloonsMessage> balancePublisher)
        {
            _factory = factory;
            _grid = grid;
            _balloonsConfig = balloonsConfig;
            _balancePublisher = balancePublisher;
        }

        public void Execute()
        {
            Spawn();
        }

        public void DrawControls()
        {
            CheatLayout.BeginPanel("Type");
            _typeIndex = CheatLayout.SelectionGrid(_typeIndex, TypeLabels());
            CheatLayout.EndPanel();

            CheatLayout.BeginPanel("Item");
            _itemIndex = CheatLayout.SelectionGrid(_itemIndex, ItemLabels());
            CheatLayout.EndPanel();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Count", GUILayout.Width(44));
            _count = CheatLayout.IntField("spawn.count", _count, min: 1);

            if (GUILayout.Button("Spawn"))
            {
                Spawn();
            }

            GUILayout.EndHorizontal();
        }

        private void Spawn()
        {
            var type = Types[_typeIndex];
            var item = Items[_itemIndex];

            var entry = FindEntry(type);
            if (entry == null)
            {
                Log.Warn("SpawnBalloonCheat", $"no BalloonsConfiguration entry for type {type}.");
                return;
            }

            var spawned = 0;
            // Bottom-up so cheat balloons join the resting stack where there's room.
            for (var row = _grid.Rows - 1; row >= 0 && spawned < _count; row--)
            {
                for (var col = 0; col < _grid.Columns && spawned < _count; col++)
                {
                    if (!_grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    SpawnAt(new Vector2Int(col, row), entry, item);
                    spawned++;
                }
            }

            if (spawned > 0)
            {
                // Settle them into resting positions right away, rather than leaving floaters until
                // the next natural balance.
                _balancePublisher.Publish(default);
            }
        }

        private void SpawnAt(Vector2Int slot, BalloonPrefabEntry entry, ItemType item)
        {
            _spawnPath.Clear();
            _spawnPath.Add(_grid.IndexToWorldPosition(slot));

            var model = _factory.Create(entry, slot, _spawnPath, () => { });

            if (item != ItemType.None && model is IHasWriteableItemSlot itemSlot)
            {
                itemSlot.Item.Value = item;
            }
        }

        private BalloonPrefabEntry FindEntry(BalloonType type)
        {
            foreach (var entry in _balloonsConfig.Entries)
            {
                if (entry.BalloonType == type)
                {
                    return entry;
                }
            }

            return null;
        }

        private static string[] TypeLabels()
        {
            var labels = new string[Types.Length];
            for (var i = 0; i < Types.Length; i++)
            {
                labels[i] = Types[i].ToString();
            }

            return labels;
        }

        private static string[] ItemLabels()
        {
            var labels = new string[Items.Length];
            for (var i = 0; i < Items.Length; i++)
            {
                labels[i] = Items[i].ToString();
            }

            return labels;
        }
    }
}
#endif
