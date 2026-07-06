using System;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>Labels a serialized array indexed by enum ordinal with the enum value's name instead of "Element N".</summary>
    public class EnumIndexedAttribute : PropertyAttribute
    {
        public readonly string[] Names;

        public EnumIndexedAttribute(Type enumType)
        {
            Names = Enum.GetNames(enumType);
        }
    }
}
