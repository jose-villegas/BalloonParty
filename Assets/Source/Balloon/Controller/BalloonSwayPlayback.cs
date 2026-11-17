using BalloonParty.Configuration.Balloons;
using UnityEngine;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     One balloon's place in the idle sway. It steps the way the plain balloon's Animator controller did, so
    ///     the pose it gives is the pose that controller gave. Each rule here was read off a real Animator, step
    ///     by step (<c>BalloonSwayAnimatorParityTests</c>):
    ///     <list type="bullet">
    ///         <item>
    ///             A fresh playback (<c>default</c>) is at rest with <see cref="Stable" /> off, as an Animator
    ///             starts in its empty default state with its parameters reset.
    ///         </item>
    ///         <item>
    ///             Each step first moves what is playing on, then looks at <see cref="Stable" />. A step that finds
    ///             the wanted loop isn't the playing one starts a crossfade into it, at no progress and with the
    ///             new loop at its beginning: the pose that step is still the old one. From the next step the
    ///             crossfade and the new loop move on, the old loop still playing under them.
    ///         </item>
    ///         <item>
    ///             A crossfade is never interrupted. <see cref="Stable" /> flipping back during one waits for it to
    ///             end. The step it ends on plays the entered loop alone and starts nothing; the step after starts
    ///             the crossfade back.
    ///         </item>
    ///         <item>
    ///             Two loops mix as rotations, not as angles: the two poses' quaternions are weighed by the
    ///             crossfade's progress, summed and normalised. Between this sway's loops that is up to a tenth of
    ///             a degree off a straight line between the angles.
    ///         </item>
    ///         <item>
    ///             Rest mixes with nothing, because the Animator's empty state has no pose to weigh against a
    ///             clip's: the first loop plays whole from the step it's asked for. Its enter time still runs, as
    ///             a crossfade nothing can interrupt, so a flip of <see cref="Stable" /> during it waits.
    ///         </item>
    ///     </list>
    /// </summary>
    internal struct BalloonSwayPlayback
    {
        private Pose _current;
        private Pose _next;
        private float _blendSeconds;

        /// <summary>Which loop the balloon should play: the Animator's <c>IsStable</c> parameter.</summary>
        internal bool Stable { get; set; }

        internal bool Blending { get; private set; }

        /// <summary>The crossfade's progress, 0 to 1. Meaningless outside one.</summary>
        internal float Blend { get; private set; }

        /// <summary>Seconds into the playing loop. During a crossfade, the loop being left.</summary>
        internal float CurrentSeconds { get; private set; }

        /// <summary>Seconds into the loop a crossfade is entering.</summary>
        internal float NextSeconds { get; private set; }

        /// <summary>Steps by <paramref name="deltaTime" /> and returns the pivot's local Z angle, in degrees.</summary>
        internal float Advance(float deltaTime, IBalloonIdleSwaySettings settings)
        {
            CurrentSeconds = Step(_current, CurrentSeconds, deltaTime, settings);
            if (Blending)
            {
                AdvanceBlend(deltaTime, settings);
            }
            else
            {
                var wanted = Stable ? Pose.Stable : Pose.Unstable;
                if (wanted != _current)
                {
                    BeginBlend(wanted, settings);
                }
            }

            return Angle(settings);
        }

        /// <summary>
        ///     The angle of the rotation the Animator mixes from two poses about one axis: their quaternions
        ///     weighed by <paramref name="progress" />, summed and normalised.
        /// </summary>
        internal static float Mix(float fromDegrees, float toDegrees, float progress)
        {
            var from = fromDegrees * (Mathf.Deg2Rad * 0.5f);
            var to = toDegrees * (Mathf.Deg2Rad * 0.5f);
            var sine = Mathf.LerpUnclamped(Mathf.Sin(from), Mathf.Sin(to), progress);
            var cosine = Mathf.LerpUnclamped(Mathf.Cos(from), Mathf.Cos(to), progress);
            return 2f * Mathf.Rad2Deg * Mathf.Atan2(sine, cosine);
        }

        private void AdvanceBlend(float deltaTime, IBalloonIdleSwaySettings settings)
        {
            NextSeconds = Step(_next, NextSeconds, deltaTime, settings);
            Blend = _blendSeconds > 0f ? Blend + deltaTime / _blendSeconds : 1f;
            if (Blend < 1f)
            {
                return;
            }

            _current = _next;
            CurrentSeconds = NextSeconds;
            Blending = false;
        }

        private void BeginBlend(Pose wanted, IBalloonIdleSwaySettings settings)
        {
            var loop = LoopOf(wanted, settings);
            _next = wanted;
            _blendSeconds = _current == Pose.Rest ? loop.EnterSeconds : loop.SwitchSeconds;
            NextSeconds = 0f;
            Blend = 0f;
            Blending = true;
        }

        private float Angle(IBalloonIdleSwaySettings settings)
        {
            if (!Blending)
            {
                return LoopOf(_current, settings).AngleAt(CurrentSeconds);
            }

            var entering = LoopOf(_next, settings).AngleAt(NextSeconds);
            return _current == Pose.Rest
                ? entering
                : Mix(LoopOf(_current, settings).AngleAt(CurrentSeconds), entering, Blend);
        }

        private static float Step(Pose pose, float seconds, float deltaTime, IBalloonIdleSwaySettings settings)
        {
            if (pose == Pose.Rest)
            {
                return 0f;
            }

            var length = LoopOf(pose, settings).Seconds;
            return length > 0f ? Mathf.Repeat(seconds + deltaTime, length) : 0f;
        }

        private static IBalloonSwayLoop LoopOf(Pose pose, IBalloonIdleSwaySettings settings)
        {
            return pose == Pose.Stable ? settings.Stable : settings.Unstable;
        }

        private enum Pose : byte
        {
            Rest,
            Stable,
            Unstable
        }
    }
}
