using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BalloonParty.EditorUI.Tables
{
    public sealed class CellConfig<TEnum> where TEnum : struct, Enum
    {
        public string TypeFieldName { get; set; } = "_type";
        public float DropdownWidth { get; set; } = 70f;
        public IReadOnlyList<FieldSpec> Fields { get; set; } = Array.Empty<FieldSpec>();
        public Action<SerializedProperty> InitializeNewEntry { get; set; }
        public Func<int, Texture2D> GetThumbnail { get; set; }
        public Func<SerializedProperty, bool> IsEntryActive { get; set; }
    }
}
