using BalloonParty.Game.Telemetry;
using VContainer.Unity;

namespace BalloonParty.UI.Telemetry
{
    /// <summary>
    ///     Binds every <see cref="MetricLabel" /> under a scope to the read model at
    ///     <see cref="Start" />, mirroring <see cref="Binding.ReactivePropertyBinder{TView,TValue}" />.
    /// </summary>
    internal sealed class MetricLabelBinder : IStartable
    {
        private readonly MetricLabel[] _labels;
        private readonly ILevelMetricsView _view;
        private readonly MetricValueResolver _resolver;

        public MetricLabelBinder(MetricLabel[] labels, ILevelMetricsView view, MetricValueResolver resolver)
        {
            _labels = labels;
            _view = view;
            _resolver = resolver;
        }

        // Not RegisterBoundViews: that helper binds every view to ONE reactive property, and each label
        // chooses its own source. The three sources also carry different snapshot types
        // (LevelMetricsSnapshot / RunMetricsSnapshot) and IReadOnlyReactiveProperty<T> is not covariant,
        // so they cannot be unified behind a single IReactiveBindable<ISealedMetrics> either.
        public void Start()
        {
            foreach (var label in _labels)
            {
                label.Bind(_view, _resolver);
            }
        }
    }
}
