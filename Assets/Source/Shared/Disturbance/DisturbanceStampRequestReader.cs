using VContainer.Unity;

namespace BalloonParty.Shared.Disturbance
{
    /// <summary>
    ///     Drains <see cref="DisturbanceStampRequest" /> each tick and applies the queued stamps to the field.
    ///     The field lives in the Game scope; requests may originate anywhere (including the launcher scene),
    ///     so this reader is the single place that turns a scope-free request into a real stamp.
    /// </summary>
    internal sealed class DisturbanceStampRequestReader : ITickable
    {
        private readonly DisturbanceFieldService _field;

        public DisturbanceStampRequestReader(DisturbanceFieldService field)
        {
            _field = field;
        }

        public void Tick()
        {
            if (_field == null)
            {
                return;
            }

            while (DisturbanceStampRequest.TryDequeue(out var stamp))
            {
                _field.Stamp(stamp.World, stamp.Radius, stamp.Strength, stamp.Direction, stamp.Duration);
            }
        }
    }
}
