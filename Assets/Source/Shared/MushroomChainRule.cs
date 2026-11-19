namespace BalloonParty.Shared
{
    /// <summary>What a ball that is not a lance gets from a mushroom it meets: a bounce, with or without the lance,
    /// or no bounce at all.</summary>
    internal enum MushroomReward
    {
        /// <summary>The ball bounces; nothing is paid.</summary>
        None,

        /// <summary>The ball bounces and the count reached the chain target: it becomes the lance.</summary>
        Arm,

        /// <summary>
        ///     No bounce. The stretch has had every bounce it is allowed
        ///     (<see cref="MushroomChainRules.MaxBouncesPerStretch" />), so the mushrooms let this ball through
        ///     until it meets a wall. The caller must not turn the ball and must not ask the bounce rule.
        /// </summary>
        PassThrough,
    }

    /// <summary>
    ///     The meadow's mushroom chain: a ball that bounces from one wall of caps to another enough times in one
    ///     flight, without touching a wall of the board in between, is paid the Snipe lance. One rule for the game
    ///     and for the shot solver, so the two cannot disagree on which hit pays.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Only two things move the count, and each has its call: a mushroom bounce (<see cref="OnBounce" />)
    ///         and a wall (<see cref="OnWall" />). A balloon pop and a ricochet off an unbreakable or a tough balloon
    ///         leave it alone, so there is no call for them: a flight may pop and ricochet between two mushrooms and
    ///         keep its chain.
    ///     </para>
    ///     <para>
    ///         <b>A lance does not come here.</b> A piercing ball, however it was armed, does not bounce off a
    ///         mushroom: it flies straight on and cuts every cap its path enters (<see cref="IBumperField.Cut" />).
    ///         A cut counts nothing and breaks nothing: the chain a lance carries is as it was when the ball was
    ///         armed, until a wall clears it. So the rule is only ever asked about a ball that is not a lance, and
    ///         the bounce that reaches the target always arms.
    ///     </para>
    ///     <para>
    ///         A mushroom bounce spends no shield, so the rule itself bounds a flight: a stretch between two walls
    ///         has at most <see cref="MushroomChainRules.MaxBouncesPerStretch" /> bounces, whether they counted or
    ///         not. Past that the mushrooms let the ball through, and it flies straight to a wall, which does spend
    ///         a shield. Without the cap a ball could be kept for good: a ricochet off an unbreakable can send it
    ///         back to the mushroom it just left, a hit that does not count and is kicked again.
    ///     </para>
    /// </remarks>
    internal static class MushroomChainRule
    {
        /// <summary>
        ///     Counts the bounce of a ball that is not a lance off a cap of wall <paramref name="wall" />
        ///     (<see cref="BumperCircle.Wall" />, 0 or more) and says what it pays, or answers
        ///     <see cref="MushroomReward.PassThrough" /> and counts nothing when the stretch is spent
        ///     (<see cref="IsSpent" />). A wall of caps is one hit: the wall hit last does not count again until
        ///     another one has been hit, whichever of its caps the ball meets, unless
        ///     <see cref="MushroomChainRules.RepeatHitCounts" />. So a rattle along one wall is worth one hit, and
        ///     a crossing between two walls is what counts.
        /// </summary>
        /// <remarks>
        ///     The counted hit that brings the chain to <see cref="MushroomChainRules.ChainTarget" /> answers
        ///     <see cref="MushroomReward.Arm" />; every other bounce pays nothing. Once armed the ball is a lance
        ///     and bounces off no mushroom, so a count past the target is only reached by a ball whose arming
        ///     never landed (it had left the board), and pays nothing. The rule does not cap the armings: a lance
        ///     spent at a wall (it discharges there once it has pierced a tough balloon) is an ordinary ball
        ///     again, the wall has cleared its chain, and a new chain arms it again.
        /// </remarks>
        internal static MushroomReward OnBounce(ref MushroomChainState state, int wall, in MushroomChainRules rules)
        {
            if (IsSpent(state, rules))
            {
                return MushroomReward.PassThrough;
            }

            state.Bounces++;
            if (!rules.RepeatHitCounts && state.LastWall == wall)
            {
                return MushroomReward.None;
            }

            state.Count++;
            state.RememberLast(wall);
            return rules.ChainTarget > 0 && state.Count == rules.ChainTarget
                ? MushroomReward.Arm
                : MushroomReward.None;
        }

        /// <summary>
        ///     Whether the mushrooms no longer bounce this ball: it has had
        ///     <see cref="MushroomChainRules.MaxBouncesPerStretch" /> bounces since its last wall. A look ahead of
        ///     the ball (the cruise lookahead, the aim line) asks this to know whether a mushroom is in its way.
        /// </summary>
        internal static bool IsSpent(in MushroomChainState state, in MushroomChainRules rules)
        {
            return state.Bounces >= rules.MaxBouncesPerStretch;
        }

        /// <summary>A wall ends the chain: the count and the bounces start again and no mushroom is the last one
        /// hit.</summary>
        internal static void OnWall(ref MushroomChainState state)
        {
            state.Count = 0;
            state.Bounces = 0;
            state.ForgetLast();
        }
    }
}
