using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    ///     What a mushroom's cap is doing once it has come off (@ref plan_meadow_mushrooms): a lance's cut, the
    ///     two pieces parting, turning, falling and fading; or a blast, the cap thrown off whole, lifted, turning,
    ///     landing and fading; the bare stem after either, and the cap popping back. Pure:
    ///     <see cref="MushroomView" /> steps it and draws what it holds.
    /// </summary>
    /// <remarks>
    ///     Every value is worked out from the time since the cut or since the regrow began, not carried from frame
    ///     to frame, so it is the same at any frame rate and ends exactly at rest.
    /// </remarks>
    internal struct MushroomCutTimeline
    {
        // The share of the fall the pieces spend only parting, before they start to drop: the slice has to be seen
        // open before anything shrinks.
        private const float DropStart = 0.3f;

        // The share of the drop over which the pieces fade out, at its end: all of it. The owner found a fade
        // kept to the fall's last third too fast, so a piece thins the whole way down, slowly at first.
        private const float FadeShare = 1f;

        // How far through a throw the cap is at its highest: early, as a thing thrown up is.
        private const float ThrowPeakPower = 0.7f;

        // The share of a throw after which the cap comes down to the ground: it shrinks, its shadow closes in
        // under it and it fades.
        private const float LandingStart = 0.5f;

        private Phase _phase;
        private float _elapsed;
        private float _open;
        private float _drop;
        private float _arc;
        private float _grow;

        /// <summary>True from the cut or the blast until the regrow begins: the mushroom is a bare stem, whether
        /// or not what came off it is still in the air.</summary>
        public bool IsCut => _phase is Phase.Falling or Phase.Blown or Phase.Bare;

        /// <summary>True while a cap a blast blew off is in the air: what is drawn is one piece, the whole
        /// cap.</summary>
        public bool IsBlown => _phase == Phase.Blown;

        /// <summary>How high a blown cap is, 0 on the stem and on the ground and 1 at the top of its throw; 0
        /// whenever none is in the air.</summary>
        public float Arc => _phase == Phase.Blown ? _arc : 0f;

        /// <summary>True when nothing is moving: the cap whole, or the stem bare with its pieces gone.</summary>
        public bool IsAtRest => _phase is Phase.Whole or Phase.Bare;

        /// <summary>The whole cap's size: 1 whole, 0 while it is cut, and from 0 up past 1 and back to it as it
        /// pops back.</summary>
        public float CapGrow => _phase switch
        {
            Phase.Whole => 1f,
            Phase.Growing => _grow,
            _ => 0f
        };

        /// <summary>How much of the two pieces still shows: 1 from the cut, 0 once they have faded out, and 0
        /// whenever there are none.</summary>
        public float PieceFade => _phase switch
        {
            Phase.Falling => 1f - Mathf.SmoothStep(0f, 1f, FadeProgress),
            Phase.Blown => 1f - Mathf.SmoothStep(0f, 1f, _drop),
            _ => 0f
        };

        /// <summary>How high a cut cap's pieces still are: 1 at the cap's height, 0 on the ground.</summary>
        public float PieceHeight => _phase == Phase.Falling ? 1f - _drop : 1f;

        // The last part of the drop, as 0 to 1.
        private float FadeProgress => Mathf.Clamp01((_drop - (1f - FadeShare)) / FadeShare);

        /// <summary>How far off the cap its shadow lies, as a share of where it lies at rest: a cut cap's pieces
        /// sink, so theirs closes in from 1 to 0; a blown cap is lifted first, so its goes out past 1, and back
        /// to 0 as it lands.</summary>
        public float ShadowHeight(in MushroomCutTuning tuning)
        {
            return _phase == Phase.Blown
                ? Mathf.Lerp(1f, tuning.Blown.ShadowLift, _arc) * (1f - _drop)
                : PieceHeight;
        }

        /// <summary>How far each piece has moved off the cut line, to its own side, in world units. A blown cap
        /// is one piece, and this is how far it has been thrown.</summary>
        public float Part(in MushroomCutTuning tuning)
        {
            return _phase switch
            {
                Phase.Falling => tuning.PartDistance * _open,
                Phase.Blown => tuning.Blown.Distance * _open,
                _ => 0f
            };
        }

        /// <summary>How far the lance has carried both pieces along its way, in world units.</summary>
        public float Carry(in MushroomCutTuning tuning)
        {
            return _phase == Phase.Falling ? tuning.CarryDistance * _open : 0f;
        }

        /// <summary>How far each piece has turned, in radians, one each way; or the one blown cap.</summary>
        public float Turn(in MushroomCutTuning tuning)
        {
            return _phase switch
            {
                Phase.Falling => tuning.TurnRadians * _open,
                Phase.Blown => tuning.Blown.TurnRadians * _open,
                _ => 0f
            };
        }

        /// <summary>The pieces' size: 1 on the stem, the tuned end size on the ground. A blown cap is larger at
        /// the top of its throw first.</summary>
        public float PieceSize(in MushroomCutTuning tuning)
        {
            return _phase switch
            {
                Phase.Falling => Mathf.Lerp(1f, tuning.EndSize, _drop),
                Phase.Blown => (1f + (tuning.Blown.Lift * _arc)) * Mathf.Lerp(1f, tuning.EndSize, _drop),
                _ => 1f
            };
        }

        /// <summary>A lance cut the cap: it is gone from the stem and its two pieces start to part.</summary>
        public void Cut()
        {
            Take(Phase.Falling);
        }

        /// <summary>A blast blew the cap off: it is gone from the stem, whole, and starts its throw.</summary>
        public void Blow()
        {
            Take(Phase.Blown);
        }

        /// <summary>The cap grows back from nothing. Nothing happens to a cap that is whole.</summary>
        public void Regrow()
        {
            if (_phase == Phase.Whole)
            {
                return;
            }

            _phase = Phase.Growing;
            _elapsed = 0f;
            _grow = 0f;
        }

        /// <summary>
        ///     Moves the fall or the regrow on by <paramref name="deltaTime" /> seconds. False once nothing is
        ///     moving, when every value is exactly at rest, so a caller may stop stepping it.
        /// </summary>
        public bool Step(float deltaTime, in MushroomCutTuning tuning)
        {
            if (IsAtRest)
            {
                return false;
            }

            _elapsed += Mathf.Max(0f, deltaTime);
            if (_phase == Phase.Falling)
            {
                var progress = _elapsed / tuning.FallSeconds;
                if (progress >= 1f)
                {
                    _phase = Phase.Bare;
                    return false;
                }

                // Quick at first: most of the parting is done in the first third, as a clean slice opens.
                var left = 1f - progress;
                _open = 1f - (left * left * left);

                // Then the pieces drop, slowly at first as a thing let go does.
                var dropping = Mathf.Clamp01((progress - DropStart) / (1f - DropStart));
                _drop = dropping * dropping;
                return true;
            }

            if (_phase == Phase.Blown)
            {
                var thrown = _elapsed / tuning.Blown.Seconds;
                if (thrown >= 1f)
                {
                    _phase = Phase.Bare;
                    return false;
                }

                // Fast off the stem and slowing, as a thing thrown is by the time it lands.
                var ahead = 1f - thrown;
                _open = 1f - (ahead * ahead);

                // Up early, then down.
                _arc = Mathf.Sin(Mathf.PI * Mathf.Pow(thrown, ThrowPeakPower));
                var landing = Mathf.Clamp01((thrown - LandingStart) / (1f - LandingStart));
                _drop = landing * landing;
                return true;
            }

            var grown = _elapsed / tuning.RegrowSeconds;
            if (grown >= 1f)
            {
                _phase = Phase.Whole;
                return false;
            }

            _grow = PopBack(grown, tuning.RegrowOvershoot);
            return true;
        }

        /// <summary>The cap whole and at rest, as on a mushroom just spawned.</summary>
        public void Reset()
        {
            this = default;
        }

        private void Take(Phase phase)
        {
            _phase = phase;
            _elapsed = 0f;
            _open = 0f;
            _drop = 0f;
            _arc = 0f;
            _grow = 0f;
        }

        // From 0 to 1 by way of a little more than 1: a cubic that leaves fast, passes its size by the overshoot
        // asked for and comes to rest on it.
        private static float PopBack(float progress, float overshoot)
        {
            var back = BackFor(overshoot);
            var left = progress - 1f;
            return 1f + ((back + 1f) * left * left * left) + (back * left * left);
        }

        // The cubic's one number for a given overshoot. It peaks 4 back^3 / (27 (back + 1)^2) over 1, which grows
        // with it, so the number is found by halving.
        private static float BackFor(float overshoot)
        {
            if (overshoot <= 0f)
            {
                return 0f;
            }

            var low = 0f;
            var high = 8f;
            for (var i = 0; i < 24; i++)
            {
                var back = (low + high) * 0.5f;
                var peak = 4f * back * back * back / (27f * (back + 1f) * (back + 1f));
                if (peak < overshoot)
                {
                    low = back;
                }
                else
                {
                    high = back;
                }
            }

            return (low + high) * 0.5f;
        }

        private enum Phase : byte
        {
            Whole,
            Falling,
            Blown,
            Bare,
            Growing
        }
    }
}
