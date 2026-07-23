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
        [Tooltip("If > 0, wraps digits in <mspace=X> for uniform character widths.")]
        [SerializeField] private float _monospaceWidth;

        private TMP_Text _label;
        private IDisposable _subscription;
        private char[] _buffer;
        private int _mspaceTagLength;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
            _label.text = "--";
            _mspaceTagLength = BuildMspacePrefix();
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
            if (_mspaceTagLength > 0)
            {
                int numLen = TmpTextExtensions.FormatThousands(value, _buffer, true, _mspaceTagLength);
                _label.SetCharArray(_buffer, 0, _mspaceTagLength + numLen);
            }
            else
            {
                _label.SetThousands(value);
            }
        }

        private int BuildMspacePrefix()
        {
            if (_monospaceWidth <= 0f)
            {
                return 0;
            }

            _buffer = new char[32];
            var tag = $"<mspace={_monospaceWidth:0.#}>";
            for (int i = 0; i < tag.Length; i++)
            {
                _buffer[i] = tag[i];
            }

            return tag.Length;
        }
    }
}
