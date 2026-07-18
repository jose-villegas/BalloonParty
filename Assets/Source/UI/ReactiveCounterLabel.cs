using System;
using BalloonParty.Shared.Extensions;
using BalloonParty.UI.Binding;
using TMPro;
using UniRx;
using UnityEngine;

namespace BalloonParty.UI
{
    /// <summary>
    ///     Shows an int reactive value as a thousands-separated label, with a <c>"--"</c> placeholder
    ///     until bound and on unbind.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    internal abstract class ReactiveCounterLabel : MonoBehaviour, IReactiveBindable<int>
    {
        // Same char-buffer approach as ScoreCounterLabel (UI/Score/ScoreCounterLabel.cs); duplicated
        // rather than shared because its FormatInt is private to that class.
        // Holds digits + thousands separators for the largest int (2,147,483,647 = 10 digits + 3 commas = 13).
        private static readonly char[] DigitTemp = new char[14];
        private static readonly char[] CharBuffer = new char[16];

        private TMP_Text _label;
        private IDisposable _subscription;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            _label.text = "--";
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
        }

        public void Bind(IReadOnlyReactiveProperty<int> source)
        {
            _subscription?.Dispose();
            _subscription = source.Subscribe(OnValueChanged);
        }

        public void Unbind()
        {
            LifecycleHelper.DisposeAndClear(ref _subscription);
            _label.text = "--";
        }

        private void OnValueChanged(int value)
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
