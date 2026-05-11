#region

using System;
using UnityEngine;
using UnityEngine.Serialization;

#endregion

namespace BalloonParty.Configuration
{
    [Serializable]
    public class ItemSettings
    {
        [FormerlySerializedAs("Type")]
        [SerializeField] private ItemType _type;

        [FormerlySerializedAs("TurnCheckEvery")]
        [SerializeField] private int _turnCheckEvery;

        [FormerlySerializedAs("Weight")]
        [SerializeField] private float _weight;

        [FormerlySerializedAs("MaximumAllowed")]
        [SerializeField] private int _maximumAllowed;

        [FormerlySerializedAs("NudgeDistance")]
        [SerializeField] private float _nudgeDistance;

        [FormerlySerializedAs("NudgeDuration")]
        [SerializeField] private float _nudgeDuration;

        public ItemType Type => _type;
        public int TurnCheckEvery => _turnCheckEvery;
        public float Weight => _weight;
        public int MaximumAllowed => _maximumAllowed;
        public float NudgeDistance => _nudgeDistance;
        public float NudgeDuration => _nudgeDuration;
    }
}
