using UniRx;

namespace BalloonParty.UI.Binding
{
    /// <summary>
    ///     A view that binds itself to a reactive source of <typeparamref name="T" />. Bound once after
    ///     all <c>Awake</c>s have run — by <see cref="ReactivePropertyBinder{TView,TValue}" /> for the
    ///     simple Start-time case, or by a feature-specific driver where binding is event-gated.
    /// </summary>
    internal interface IReactiveBindable<T>
    {
        void Bind(IReadOnlyReactiveProperty<T> source);
    }
}
