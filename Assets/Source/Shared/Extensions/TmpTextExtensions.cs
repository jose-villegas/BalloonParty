using TMPro;

namespace BalloonParty.Shared.Extensions
{
    internal static class TmpTextExtensions
    {
        // Shared scratch for the zero-alloc path below — main-thread only, like all TMP access.
        // Holds digits + thousands separators for the largest int (2,147,483,647 = 10 digits + 3 commas = 13).
        private static readonly char[] DigitTemp = new char[14];
        private static readonly char[] CharBuffer = new char[16];

        /// <summary>
        ///     Sets the label to <paramref name="value"/> thousands-separated without allocating —
        ///     the zero-alloc counterpart of <c>text = value.ToString("N0")</c> for hot counters
        ///     (score, shields, health), formatting into a shared char buffer and applying via
        ///     <see cref="TMP_Text.SetCharArray(char[], int, int)"/>.
        /// </summary>
        internal static void SetThousands(this TMP_Text label, int value)
        {
            var length = FormatThousands(value, CharBuffer);
            label.SetCharArray(CharBuffer, 0, length);
        }

        internal static int FormatThousands(int value, char[] buffer)
        {
            if (value == 0)
            {
                buffer[0] = '0';
                return 1;
            }

            var negative = value < 0;
            long v = value;
            if (negative)
            {
                v = -v;
            }

            var tempLen = 0;
            var digitCount = 0;

            while (v > 0)
            {
                if (digitCount > 0 && digitCount % 3 == 0)
                {
                    DigitTemp[tempLen++] = ',';
                }

                DigitTemp[tempLen++] = (char)('0' + (int)(v % 10));
                v /= 10;
                digitCount++;
            }

            var bufLen = 0;
            if (negative)
            {
                buffer[bufLen++] = '-';
            }

            for (var i = tempLen - 1; i >= 0; i--)
            {
                buffer[bufLen++] = DigitTemp[i];
            }

            return bufLen;
        }
    }
}
