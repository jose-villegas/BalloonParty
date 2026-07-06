using System;
using System.Collections.Generic;
using UnityEngine;
using BalloonParty.Configuration.Effects;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Pre-baked bush variant: branch map texture + leaf attachment points.</summary>
    [CreateAssetMenu(menuName = "Configuration/Bush Variant Data", fileName = "BushVariantData")]
    internal class BushVariantData : ScriptableObject
    {
        [SerializeField] private Texture2D _branchMap;
        [SerializeField] private LeafSlotData[] _leafSlots;
        [SerializeField] private Vector2 _boundsSize = Vector2.one;

#if UNITY_EDITOR
        [SerializeField] private Vector4[] _debugSegments;
        [SerializeField] private float _debugBushWorldSize;

        /// <summary>Raw generator segment pairs as (start.x, start.y, end.x, end.y) in UV space.</summary>
        internal IReadOnlyList<Vector4> DebugSegments => _debugSegments;
        internal float DebugBushWorldSize => _debugBushWorldSize;
#endif

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

        internal void SetDebugData(Vector4[] segments, float bushWorldSize)
        {
            _debugSegments = segments;
            _debugBushWorldSize = bushWorldSize;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    [Serializable]
    internal struct LeafSlotData
    {
        /// <summary>UV-space position [0–1] on the branch map.</summary>
        [SerializeField] internal Vector2 UVPosition;
        [SerializeField] internal float BaseAngle;
        [SerializeField] internal float Depth;
        [SerializeField] internal float PhaseOffset;
        [SerializeField] internal float Scale;
        [SerializeField] internal int SpriteVariant;
        [SerializeField] internal Color32 Tint;
    }
}
