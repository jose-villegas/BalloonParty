using System;
using BalloonParty.Shared.Extensions;
using BalloonParty.UI.Binding;
using UniRx;
using UnityEngine;

namespace BalloonParty.UI
{
    /// <summary>
    ///     Subscribes to a reactive int and delegates rendering to an <see cref="ICounterDisplay"/>
    ///     on the same GameObject. Add <see cref="RollingCounterDisplay"/> or
    ///     <see cref="PlainCounterDisplay"/> as a sibling component.
    /// </summary>
    internal abstract class ReactiveCounterLabel : MonoBehaviour, IReactiveBindable<int>
    {
        private ICounterDisplay _display;
        private IDisposable _subscription;

        protected virtual void Awake()
        {
            _display = GetComponent<ICounterDisplay>();
            _display.ShowPlaceholder();
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
            _display.ShowPlaceholder();
        }

        protected virtual void OnValueChanged(int value)
        {
            _display.Display(value);
        }
    }
}
