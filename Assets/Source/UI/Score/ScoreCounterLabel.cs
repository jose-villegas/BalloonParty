using TMPro;
using UniRx;
using UnityEngine;

namespace BalloonParty.UI.Score
{
    [RequireComponent(typeof(TMP_Text))]
    public class ScoreCounterLabel : MonoBehaviour
    {
        // Holds digits + thousands separators for the largest int (2,147,483,647 = 10 digits + 3 commas = 13).
        private static readonly char[] DigitTemp = new char[14];
        private static readonly char[] CharBuffer = new char[16];

        private TMP_Text _label;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
        }

        public void Bind(IReadOnlyReactiveProperty<int> score)
        {
            score.Subscribe(OnScoreChanged).AddTo(this);
        }

        private void OnScoreChanged(int value)
        {
            var length = FormatInt(value, CharBuffer);
            _label.SetCharArray(CharBuffer, 0, length);
        }

        private static int FormatInt(int value, char[] buffer)
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
