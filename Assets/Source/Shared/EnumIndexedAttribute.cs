using System;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     For a serialized array/list indexed by an enum's ordinal: labels each element with the enum
    ///     value's name ("Default", "Return", …) instead of "Element 0, 1, …". Assumes a 0-based,
    ///     contiguous enum (element N ↔ the enum value with ordinal N). The array can be longer or shorter
    ///     than the enum — extra elements keep their numeric label.
    /// </summary>
    public class EnumIndexedAttribute : PropertyAttribute
    {
        public readonly string[] Names;

        public EnumIndexedAttribute(Type enumType)
        {
            Names = Enum.GetNames(enumType);
        }
    }
}
