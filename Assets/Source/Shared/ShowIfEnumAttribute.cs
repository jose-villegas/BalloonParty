using System;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>Hides the decorated field in the inspector unless a sibling enum field (on the same
    /// serialized object or struct) currently equals one of <see cref="Values"/>. Pass the enum's
    /// integer constants, cast at the call site — e.g.
    /// <c>[ShowIfEnum(nameof(_source), (int)Source.Realtime)]</c>. Assumes a contiguous 0-based enum
    /// (value == declaration index). Don't combine with another drawer attribute (e.g. <c>[Range]</c>) —
    /// Unity uses only one property drawer per field. Editor-only behaviour: <c>ShowIfEnumDrawer</c>.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ShowIfEnumAttribute : PropertyAttribute
    {
        public string EnumFieldName { get; }
        public int[] Values { get; }

        public ShowIfEnumAttribute(string enumFieldName, params int[] values)
        {
            EnumFieldName = enumFieldName;
            Values = values;
        }
    }
}
