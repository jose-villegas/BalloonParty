using UnityEngine;

namespace BalloonParty.EditorUI.Tables
{
    public static class RowColorResolver
    {
        public static Color Resolve(
            bool isFocused,
            bool isActive,
            bool isFallback,
            int rowIndex,
            Color focusedColor,
            Color activeColor,
            Color fallbackColor,
            Color oddColor,
            Color evenColor)
        {
            if (isFocused)
            {
                return focusedColor;
            }

            if (isActive)
            {
                return activeColor;
            }

            if (isFallback)
            {
                return fallbackColor;
            }

            return rowIndex % 2 == 1 ? oddColor : evenColor;
        }

        public static bool IsInRange(int selectedLevel, int from, int to, bool isFallback)
        {
            if (isFallback)
            {
                return false;
            }

            return selectedLevel >= from && selectedLevel <= to;
        }
    }
}
