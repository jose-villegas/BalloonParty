using UniRx;
using VContainer.Unity;

namespace BalloonParty.UI.Binding
{
    /// <summary>
    ///     Binds every view in <paramref name="views" /> to a single reactive source at
    ///     <see cref="Start" /> — after every <c>Awake</c> has run, so each view has resolved its
    ///     components. Wire it via <see cref="ReactiveBinderRegistration.RegisterBoundViews" /> rather
    ///     than constructing it directly.
    /// </summary>
    internal sealed class ReactivePropertyBinder<TView, TValue> : IStartable
        where TView : IReactiveBindable<TValue>
    {
        private readonly TView[] _views;
        private readonly IReadOnlyReactiveProperty<TValue> _source;

        public ReactivePropertyBinder(TView[] views, IReadOnlyReactiveProperty<TValue> source)
        {
            _views = views;
            _source = source;
        }

        public void Start()
        {
            foreach (var view in _views)
            {
                view.Bind(_source);
            }
        }
    }
}
