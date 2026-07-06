using UniRx;

namespace BalloonParty.UI.Binding
{
    /// <summary>
    ///     A view that binds itself to a reactive source of <typeparamref name="T" />.
    /// </summary>
    internal interface IReactiveBindable<T>
    {
        void Bind(IReadOnlyReactiveProperty<T> source);
    }
}
