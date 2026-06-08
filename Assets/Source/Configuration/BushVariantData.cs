using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    /// Pre-baked bush variant: branch map texture + leaf attachment points.
    /// Created by the Bush Baker editor window. Loaded at runtime by BushView.
    /// </summary>
    [CreateAssetMenu(menuName = "Configuration/Bush Variant Data", fileName = "BushVariantData")]
    internal class BushVariantData : ScriptableObject
    {
        [SerializeField] private Texture2D _branchMap;
        [SerializeField] private LeafSlotData[] _leafSlots;
        [SerializeField] private Vector2 _boundsSize = Vector2.one;

        internal Texture2D BranchMap => _branchMap;
        internal IReadOnlyList<LeafSlotData> LeafSlots => _leafSlots;
        internal Vector2 BoundsSize => _boundsSize;

#if UNITY_EDITOR
        internal void SetBakeData(
            Texture2D branchMap, LeafSlotData[] leafSlots, Vector2 boundsSize)
        {
            _branchMap = branchMap;
            _leafSlots = leafSlots;
            _boundsSize = boundsSize;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    [Serializable]
    internal struct LeafSlotData
    {
        [SerializeField] internal Vector2 Position;
        [SerializeField] internal float BaseAngle;
        [SerializeField] internal float Depth;
        [SerializeField] internal float PhaseOffset;
        [SerializeField] internal float Scale;
        [SerializeField] internal int SpriteVariant;
        [SerializeField] internal Color32 Tint;
    }
}
