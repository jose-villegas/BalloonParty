namespace BalloonParty.Item
{
    /// <summary>
    ///     A <see cref="SightReaction"/> that eases a continuous value every frame off the probe's raw
    ///     centrality (0 = off-aim, 1 = dead-centre) — the shape for speed/scale/emission/tint reactions.
    ///     Subclasses implement <see cref="OnSightTick"/>; the per-frame poll (over a subscription) is
    ///     deliberate since the ease runs every frame regardless.
    /// </summary>
    internal abstract class SightRampReaction : SightReaction
    {
        protected virtual void LateUpdate()
        {
            if (Probe != null)
            {
                OnSightTick(Probe.Sight.Value);
            }
        }

        protected abstract void OnSightTick(float centrality);
    }
}
