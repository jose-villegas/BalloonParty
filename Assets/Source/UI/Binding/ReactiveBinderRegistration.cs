using System;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.UI.Binding
{
    internal static class ReactiveBinderRegistration
    {
        /// <summary>
        ///     Gathers every <typeparamref name="TView" /> under <paramref name="scope" /> and binds them to
        ///     the reactive property <paramref name="selector" /> reads off <typeparamref name="TSource" />.
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
