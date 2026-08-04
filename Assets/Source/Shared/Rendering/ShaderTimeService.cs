using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Shared.Rendering
{
    /// <summary>
    ///     Publishes <c>_BP_UnscaledTime</c> as a global shader property so an effect can opt out of
    ///     the engine's scaled clock. Shaders have <c>_Time</c>, which is scaled — anything driven by
    ///     it freezes while <see cref="Time.timeScale" /> is 0, which is precisely when the level-up
    ///     popup wants its fill to keep shining.
    /// </summary>
    /// <remarks>
    ///     Opt-in per material (<c>_ShineUnscaledTime</c>), so every effect that has always run on
    ///     scaled time keeps doing so. See <c>Include/ShineSweep.cginc</c>.
    /// </remarks>
    internal sealed class ShaderTimeService : IStartable, ITickable
    {
        private static readonly int UnscaledTimeId = Shader.PropertyToID("_BP_UnscaledTime");

        public void Start()
        {
            // Before the first Tick, so a material that samples during the very first frame reads a
            // real value rather than the zero an unset global returns.
            Publish();
        }

        public void Tick()
        {
            Publish();
        }

        private static void Publish()
        {
            Shader.SetGlobalFloat(UnscaledTimeId, Time.unscaledTime);
        }
    }
}
