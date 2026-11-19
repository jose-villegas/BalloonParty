using BalloonParty.Balloon.Controller;
using BalloonParty.Projectile;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Item.Snipe
{
    /// <summary>
    ///     Grows back the caps a lance cut (@ref plan_meadow_mushrooms), in time with the board: a cut cap pops
    ///     back on one of the board's beats (<see cref="IBoardBeat" />), the one that brings its count to
    ///     <c>IMushroomSettings.RegrowBeats</c>, and is announced (<see cref="MushroomRegrownMessage" />).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Which beat is the first.</b> The beat is read once a frame, in <see cref="Tick" />. A cap is
    ///         first seen by the tick that follows its cut, and its count starts with the first beat seen to
    ///         fall after that tick: a beat that fell in the very frame the cap was cut in, whichever of the two
    ///         came first, is not the cap's. That is the one rule for every cap, the first cut on a whole board
    ///         and one cut while others wait alike. So a cap cut a frame before a beat has that beat as its
    ///         first, and one cut in a beat's own frame waits a whole beat for its first: with five beats of 1.7
    ///         seconds a cap is gone for between 6.8 and 8.5 seconds, and always comes back on the beat. A pause
    ///         holds the beat and so holds the caps.
    ///     </para>
    ///     <para>
    ///         <b>Not under a ball.</b> A cap whose beats are up does not grow back while a ball in flight has
    ///         its centre inside the cap's circle: a ball cannot be put inside a bumper. It is asked again on
    ///         the next beat, and the one after, until its place is clear.
    ///     </para>
    ///     <para>
    ///         It keeps the beat it last saw and how many cut caps it had seen by then, and nothing else: which
    ///         caps are cut, and how many beats each has left, is the field's
    ///         (<see cref="IMushroomRegrowth" />, which lists them in the order they were cut, so the caps cut
    ///         since the last tick are the last ones). While no cap is cut a tick is one
    ///         comparison and the beat is not read. A level's end and a reset take the cut caps off the board
    ///         with the rest of it, and the field forgets them: nothing is regrown and nothing is announced,
    ///         since the level that follows places whole caps.
    ///     </para>
    /// </remarks>
    internal sealed class MushroomRegrowController : ITickable
    {
        private readonly IMushroomRegrowth _caps;
        private readonly IBoardBeat _beat;
        private readonly IProjectileRegistry _projectiles;
        private readonly IPublisher<MushroomRegrownMessage> _regrownPublisher;

        private bool _counting;
        private int _lastBeat;
        private int _capsSeen;

        internal MushroomRegrowController(
            IMushroomRegrowth caps, IBoardBeat beat, IProjectileRegistry projectiles,
            IPublisher<MushroomRegrownMessage> regrownPublisher)
        {
            _caps = caps;
            _beat = beat;
            _projectiles = projectiles;
            _regrownPublisher = regrownPublisher;
        }

        public void Tick()
        {
            var cut = _caps.CutCount;
            if (cut == 0)
            {
                _counting = false;
                _capsSeen = 0;
                return;
            }

            var beat = _beat.OffBeats(0f);
            if (!_counting)
            {
                // The first cut since the board was whole: the beat is followed from here.
                _counting = true;
                _lastBeat = beat;
            }

            // A beat is counted for the caps the last tick had already seen, and not for the ones cut since: it
            // fell in their own frame. A lower count is a run that started over, which is no beat. Fewer caps
            // than were seen means some left the board, and the ones that are left are all old.
            var aBeatHasFallen = beat > _lastBeat;
            var seen = Mathf.Min(_capsSeen, cut);
            _lastBeat = beat;
            if (aBeatHasFallen && seen > 0)
            {
                _caps.CountBeat(seen);
                RegrowTheCapsThatAreDue();
            }

            _capsSeen = _caps.CutCount;
        }

        private void RegrowTheCapsThatAreDue()
        {
            for (var i = _caps.CutCount - 1; i >= 0; i--)
            {
                if (!_caps.IsDueAt(i) || IsUnderABall(_caps.CutCircleAt(i)))
                {
                    continue;
                }

                var slot = _caps.CutSlotAt(i);
                _caps.Regrow(i);
                _regrownPublisher.Publish(new MushroomRegrownMessage(slot));
            }
        }

        private bool IsUnderABall(in BumperCircle cap)
        {
            var reach = cap.Radius * cap.Radius;
            for (var i = 0; i < _projectiles.InFlightCount; i++)
            {
                Vector2 ball = _projectiles.InFlightPositionAt(i);
                if ((ball - cap.Center).sqrMagnitude < reach)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
