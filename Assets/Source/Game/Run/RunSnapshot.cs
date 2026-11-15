using System;
using System.Collections.Generic;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     A run saved at a level boundary, so a run the OS killed can be picked up on the next launch. It holds
    ///     what the saved level began with (score, hearts, cards), the run's seed and the retries it has used, so
    ///     resuming plays that level as its retry would, without spending a retry. Saved on the game-over screen,
    ///     it brings that screen back instead, with the same offer.
    /// </summary>
    [Serializable]
    internal sealed class RunSnapshot
    {
        // Bump when a field changes meaning or goes; a save of another version is discarded.
        internal const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public int Level;
        public int Seed;
        public int LevelStartScore;
        public int Hearts;
        public int RetriesUsed;
        public bool OnGameOver;
        public int FinalScore;
        public List<HeldCardRecord> Cards = new();
        public int FreeCardsDealt;
        public string BonusCard;

        /// <summary>
        ///     Reads a save written by <see cref="ToJson" />. A corrupt save, or one of another version, gives
        ///     false with a warning: it's dropped rather than half-restored.
        /// </summary>
        internal static bool TryParse(string json, out RunSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrEmpty(json))
            {
                return false;
            }

            RunSnapshot parsed;
            try
            {
                parsed = JsonUtility.FromJson<RunSnapshot>(json);
            }
            catch (ArgumentException e)
            {
                Log.Warn("RunSave", $"Saved run is unreadable, discarded: {e.Message}");
                return false;
            }

            if (parsed == null || parsed.Version != CurrentVersion)
            {
                Log.Warn("RunSave", $"Saved run has version {parsed?.Version}, not {CurrentVersion}; discarded");
                return false;
            }

            if (!parsed.IsValid())
            {
                Log.Warn("RunSave", "Saved run holds impossible values, discarded");
                return false;
            }

            snapshot = parsed;
            return true;
        }

        internal string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        // Hearts at zero only on the game-over screen: a level can't begin with none.
        private bool IsValid()
        {
            if (Level < 1 || LevelStartScore < 0 || Hearts < 0 || RetriesUsed < 0 || FreeCardsDealt < 0)
            {
                return false;
            }

            if (Hearts == 0 && !OnGameOver)
            {
                return false;
            }

            if (OnGameOver && FinalScore < LevelStartScore)
            {
                return false;
            }

            Cards ??= new List<HeldCardRecord>();
            foreach (var card in Cards)
            {
                if (card == null || string.IsNullOrEmpty(card.Title) || card.Stacks < 1)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>A held upgrade card, by its title, and how many times the run holds it.</summary>
    [Serializable]
    internal sealed class HeldCardRecord
    {
        public string Title;
        public int Stacks;

        public HeldCardRecord()
        {
        }

        internal HeldCardRecord(string title, int stacks)
        {
            Title = title;
            Stacks = stacks;
        }
    }
}
