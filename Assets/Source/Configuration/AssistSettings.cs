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
        public IPitySettings HeartPity => _heartPity;
        public IPitySettings ItemPity => _itemPity;
        public float ThreatEaseAtFullNeed => Mathf.Clamp01(_threatEaseAtFullNeed);
    }
}
