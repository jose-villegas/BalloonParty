using System;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI
{
    internal static class ReactiveBinderRegistration
    {
        /// <summary>
        ///     Gathers every <typeparamref name="TView" /> under <paramref name="scope" /> and registers
        ///     a <see cref="ReactivePropertyBinder{TView,TValue}" /> entry point that binds them — at
        ///     <c>Start</c> — to the reactive property <paramref name="selector" /> reads off
        ///     <typeparamref name="TSource" /> (resolved from the parent scope). Safe when no views are
        ///     present (empty array).
        /// </summary>
        public static void RegisterBoundViews<TView, TSource, TValue>(
            this IContainerBuilder builder,
            LifetimeScope scope,
            Func<TSource, IReadOnlyReactiveProperty<TValue>> selector)
            where TView : Component, IReactiveBindable<TValue>
        {
            var views = scope.GetComponentsInChildren<TView>(true);
            builder.RegisterEntryPoint<ReactivePropertyBinder<TView, TValue>>()
                .WithParameter(views)
                .WithParameter<IReadOnlyReactiveProperty<TValue>>(resolver => selector(resolver.Resolve<TSource>()));
        }
    }
}
