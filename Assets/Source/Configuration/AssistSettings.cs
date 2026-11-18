using System;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     Inspector-authored <see cref="IAssistSettings" />, serialized inside <see cref="RunConfig" />: help that
    ///     comes only when the player needs it, so its base rates can stay low.
    /// </summary>
    [Serializable]
    internal class AssistSettings : IAssistSettings
    {
        [Tooltip("How much the danger signal counts toward need, 0 to 1: at 1, a board one line from a lost heart is " +
            "as much need as the last heart. Need is the larger of the hearts' and the danger's.")]
        [SerializeField] [Range(0f, 1f)] private float _dangerNeedWeight = 1f;

        [Tooltip("The Sharpshooter card's Snipe roll: each pen loaded without a Snipe while in need raises the next " +
            "pen's chance.")]
        [SerializeField] private PitySettings _snipePity = new(4f, 0.5f);

        [Tooltip("How much armour left standing counts toward the Snipe roll's need, 0 to 1: at full armour (Armour " +
            "Count For Full toughs or unbreakables, each Armour Seconds For Full on the board) the roll reads this " +
            "much need, so its pity builds even at full hearts. The roll takes the larger of this and the player's " +
            "need. 0 = armour never counts.")]
        [SerializeField] [Range(0f, 1f)] private float _armourNeedWeight = 0.4f;

        [Tooltip("How many armoured balloons (toughs, unbreakables), each standing its full age, make full armour.")]
        [SerializeField] [Min(1f)] private float _armourCountForFull = 6f;

        [Tooltip("Seconds of game time an armoured balloon stands before it counts in full; it counts from 0 as it " +
            "lands.")]
        [SerializeField] [Min(1f)] private float _armourSecondsForFull = 30f;

        [Tooltip("How much a bomb left under a formation's cover counts toward the Snipe roll's need, 0 to 1: a " +
            "bomb that has stayed covered for Covered Bomb Seconds For Full reads this much need, so the lance that " +
            "can reach it grows likelier the longer the player is kept from it. The roll takes the largest of this, " +
            "the armour's and the player's need. 0 = a covered bomb never counts.")]
        [SerializeField] [Range(0f, 1f)] private float _coveredBombNeedWeight = 0.6f;

        [Tooltip("Seconds of game time a bomb stays covered before it counts in full; it counts from 0 as the cover " +
            "closes, and from 0 again if the cover opens.")]
        [SerializeField] [Min(1f)] private float _coveredBombSecondsForFull = 20f;

        [Tooltip("How many of the six slots round a balloon carrying a bomb must hold unbreakables in formation for " +
            "the bomb to count as covered. The item-cover formation starts at 3, below the bomb.")]
        [SerializeField] [Range(1, 6)] private int _coveredBombMinCover = 3;

        [Tooltip("Bounces off the run's first golden balloon after which the next pen loaded is a Snipe lance, " +
            "whatever the Sharpshooter roll, so the player learns what pops it. Once a run. 0 = never.")]
        [SerializeField] [Min(0)] private int _firstGoldenSnipeAfterDeflects = 3;

        [Tooltip("The heart balloon's roll on an ordinary level: each line rolled without one while in need raises " +
            "the next line's chance. Its base chance already grows with the hearts missing, so its growth is gentler.")]
        [SerializeField] private PitySettings _heartPity = new(0.5f, 0.5f);

        [Tooltip("A cadence wave's roll for any item at all (the level's item-count curve landing on more than 0): " +
            "each such wave gone without one while in need raises the next one's chance. The initial board never " +
            "rolls it.")]
        [SerializeField] private PitySettings _itemPity = new(0.5f, 0.9f);

        [Tooltip("How much the threats that come by chance ease off at full need, 0 to 1: the extra balloon a pop " +
            "can spawn and an untouched formation's call for reinforcements, both scaled down by this times the " +
            "need. 0.5 = half as likely at the last heart. 0 = they never ease.")]
        [SerializeField] [Range(0f, 1f)] private float _threatEaseAtFullNeed = 0.5f;

        public float DangerNeedWeight => _dangerNeedWeight;
        public IPitySettings SnipePity => _snipePity;
        public float ArmourNeedWeight => _armourNeedWeight;
        public float ArmourCountForFull => _armourCountForFull;
        public float ArmourSecondsForFull => _armourSecondsForFull;
        public float CoveredBombNeedWeight => _coveredBombNeedWeight;
        public float CoveredBombSecondsForFull => _coveredBombSecondsForFull;
        public int CoveredBombMinCover => _coveredBombMinCover;
        public int FirstGoldenSnipeAfterDeflects => _firstGoldenSnipeAfterDeflects;
        public IPitySettings HeartPity => _heartPity;
        public IPitySettings ItemPity => _itemPity;
        public float ThreatEaseAtFullNeed => Mathf.Clamp01(_threatEaseAtFullNeed);
    }
}
