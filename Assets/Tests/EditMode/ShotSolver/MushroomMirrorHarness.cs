using System;
using System.Collections.Generic;
using System.Reflection;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Item;
using BalloonParty.Item.Snipe;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pool;
using BalloonParty.Solver;
using MessagePipe;
using NSubstitute;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>What turned a flight: the three things that do.</summary>
    internal enum MirrorTurnKind
    {
        Wall,
        Bumper,
        Deflect,
    }

    /// <summary>One turn of a flight, as both sides of the mushroom mirror record it.</summary>
    internal readonly struct MirrorTurn
    {
        public readonly MirrorTurnKind Kind;

        /// <summary>The ball's centre where it turned.</summary>
        public readonly Vector2 Point;

        /// <summary>For a mushroom: its id, the chain's count after the bounce, and what the chain rule answered.</summary>
        public readonly int BumperId;

        public readonly int Count;
        public readonly MushroomReward Reward;

        /// <summary>For a mushroom: whether the ball leaves it a lance.</summary>
        public readonly bool LeavesPiercing;

        public MirrorTurn(
            MirrorTurnKind kind, Vector2 point, int bumperId = -1, int count = 0,
            MushroomReward reward = MushroomReward.None, bool leavesPiercing = false)
        {
            Kind = kind;
            Point = point;
            BumperId = bumperId;
            Count = count;
            Reward = reward;
            LeavesPiercing = leavesPiercing;
        }

        public override string ToString()
        {
            return Kind == MirrorTurnKind.Bumper
                ? $"mushroom {BumperId} at {Point:F4} (count {Count}, {Reward}, lance {LeavesPiercing})"
                : $"{Kind} at {Point:F4}";
        }
    }

    /// <summary>Why the game's side of the mirror could not place a flight (see <see cref="MushroomLiveGame" />).</summary>
    internal enum MirrorUnplaceable
    {
        None,

        /// <summary>One fixed step entered a balloon and then bounced off a mushroom.</summary>
        BalloonThenMushroom,

        /// <summary>One fixed step entered a balloon and then bounced off a wall.</summary>
        BalloonThenWall,

        /// <summary>One fixed step entered a balloon and then died at a wall.</summary>
        BalloonThenDeath,

        /// <summary>One fixed step crossed two walls.</summary>
        TwoWalls,

        /// <summary>A deflect's carried stretch reached a wall, where the game clamps the ball.</summary>
        DeflectIntoWall,
    }

    /// <summary>One flight, flown by the game's code or by the simulator.</summary>
    internal sealed class MirrorFlight
    {
        /// <summary>Every turn, in order: walls, mushrooms and deflects. A wall the ball died at is the last.</summary>
        public readonly List<MirrorTurn> Turns = new();

        public int Pops;

        /// <summary>Where the ball's centre was at each pop, in order.</summary>
        public readonly List<Vector2> PopPoints = new();

        public bool Died;
        public bool EndsPiercing;
        public int LancesArmed;

        /// <summary>Where the flight began: with <see cref="Turns" />, every straight leg it flew.</summary>
        public Vector2 Origin;

        /// <summary>The id of every cap the flight cut as a lance, in the order it cut them. A cut is no turn:
        /// the ball flies on unbent.</summary>
        public readonly List<int> CapsCut = new();

        /// <summary>Simulator side only: chains flown to their target, whatever they paid.</summary>
        public int ChainsCompleted;

        /// <summary>Lance charges the flight banked by cutting caps: one a cap (decision K).</summary>
        public int ChargesBanked;

        /// <summary>Game side only, on a game that plows (<see cref="MushroomLiveGame.PlowsToughs" />): the walls
        /// at which the lance's pierce was spent on plowed toughs, and the banked charges that kept it a lance
        /// there.</summary>
        public int Discharges;

        public int ChargesSpent;

        /// <summary>Game side only: every wall the ball reached, the one it died at included, and how many of
        /// them it reached as a lance. A leg is the flight between two walls, so these count its legs.</summary>
        public int Walls;

        public int WallsReachedAsLance;

        /// <summary>Game side only: whether the ball was a lance when it reached its first wall, and how many
        /// caps it had cut by then.</summary>
        public bool LanceAtFirstWall;

        public int CapsCutByFirstWall;

        /// <summary>Game side only: how long the flight lasted, in seconds of fixed steps.</summary>
        public float Seconds;

        /// <summary>Game side only: how the ball was flying when the flight ended. The speed is the last fixed
        /// step's, buffs and all.</summary>
        public bool EndsCruising;

        public bool EndsWithASpeedBuff;
        public float LastSpeed;

        /// <summary>
        ///     Game side only. The turns that can be held against the simulator: all of them, unless a lance
        ///     plowed a balloon with hits to spare. The game keeps that balloon until the lance's next wall and
        ///     spends the pierce there; the simulator pops it at once and keeps the pierce (an approximation
        ///     older than the mushrooms, in the solver's README), so past that point the two are not held alike.
        /// </summary>
        public int ComparableTurns = int.MaxValue;

        /// <summary>
        ///     Game side only: the flight did something inside one fixed step that this harness cannot place,
        ///     and is left out of the comparison. See <see cref="MushroomLiveGame" />.
        /// </summary>
        public MirrorUnplaceable Unplaceable;
    }

    /// <summary>A balloon of a mirror board: where it is, its own radius, and its hits (1 pops, more deflect,
    /// <see cref="int.MaxValue" /> for an unbreakable).</summary>
    internal readonly struct MirrorTarget
    {
        public readonly Vector2 Center;
        public readonly float Radius;
        public readonly int Hits;

        public MirrorTarget(Vector2 center, float radius, int hits)
        {
            Center = center;
            Radius = radius;
            Hits = hits;
        }
    }

    /// <summary>
    ///     The game's side of the mushroom mirror, with no scene: a ball flown in fixed steps by the real
    ///     <see cref="ProjectileMotionResolver" /> over a bumper field, its bounces handed to the real
    ///     <see cref="MushroomChainController" />, which pays through the real <see cref="SnipeItemHandler" /> and
    ///     the real buff service, as the game is wired. A lance's cuts take the caps out of the field as the
    ///     resolver makes them; each flight starts from the whole board again unless it is asked to fly the board
    ///     as the last flight left it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         What stands in for the view. <c>ProjectileView</c> is a MonoBehaviour with a physics trigger; here
    ///         a balloon is met when the last straight leg of a step enters its contact circle, and the contact
    ///         is resolved as the view resolves it: a one-hit balloon, or any balloon under a lance, is popped
    ///         and the ball flies on; any other deflects the ball through
    ///         <see cref="ProjectileMotionResolver.Deflect" />, with the view's own bookkeeping after it.
    ///     </para>
    ///     <para>
    ///         What it cannot place. A fixed step that enters a balloon and then turns (off a mushroom or a
    ///         wall) or dies at a wall before it ends: the game resolves the turn first and the balloon from
    ///         wherever the step ends, or not at all, which is the step's own artefact and nothing the event
    ///         simulator models. A step that crosses two walls: the game spends one shield for it, the
    ///         simulator one for each. And a deflect whose carried stretch reaches a wall: the game clamps the
    ///         ball there, the simulator reflects it. All three are older than the mushrooms. Such flights are
    ///         marked <see cref="MirrorFlight.Unplaceable" /> and counted, not compared.
    ///     </para>
    /// </remarks>
    internal sealed class MushroomLiveGame
    {
        internal const float FixedStep = 0.02f;
        internal const float BaseSpeed = 9.869022f;
        internal const float LanceSpeedMultiplier = 1.6f;
        internal const float BallRadius = 0.125f;

        // A leg is looked along from this far behind its start, so an entry that falls on the seam between two
        // steps (within a rounding error of where one ended) is found by the second and not missed by both.
        private const float SeamOverlap = 1e-3f;

        private readonly ProjectileRegistry _registry = new();
        private readonly ProjectileMotionResolver _resolver;
        private readonly Field _field;
        private readonly SnipeItemHandler _snipe;
        private readonly WallLimits _walls;
        private readonly List<BumperBounce> _stepBounces = new();
        private readonly List<Vector3> _stepPath = new();

        // What each point of the step's path after the first is: a mushroom the ball bounced off, or a wall.
        private readonly List<MirrorTurnKind> _stepTurns = new();
        private readonly List<MirrorTarget> _targets = new();
        private readonly List<int> _hitsLeft = new();

        // On a game that plows: which of the targets a lance has plowed and not yet shattered, beside _targets.
        private readonly List<bool> _plowed = new();
        private readonly BalloonModel _plowedBalloon = new();

        private IMessageHandler<MushroomBouncedMessage> _bounced;
        private IMessageHandler<PierceDischargedMessage> _discharged;
        private MirrorFlight _flight;
        private Vector2 _heading;

        // The balloon the ball last deflected off and the direction it left along: while it still flies that
        // direction it is on its way out of that balloon's circle, which the seam overlap would read as entered.
        private int _leftTarget;
        private Vector2 _leftDirection;

        /// <summary>The caps that are whole as the last flight left the board, as the shot solver would gather
        /// them.</summary>
        internal IReadOnlyList<BumperCircle> WholeCaps => _field.Bumpers;

        /// <summary>
        ///     Whether a lance plows a balloon with hits to spare as the game does: the balloon stands until
        ///     the lance's next wall, where it is shattered and the pierce is spent, unless a banked charge
        ///     re-arms it (the real <c>SpendPierce</c> and the real Snipe handler, told of the discharge as
        ///     <c>ProjectileHitResolver.DischargePending</c> tells it). Off, such a balloon is popped at once
        ///     and the lance is never spent, which is all the simulator models and what the mirror is held
        ///     against.
        /// </summary>
        internal bool PlowsToughs { get; }

        /// <summary>
        ///     On, a flight the mirror could not place is flown on all the same, for measuring: a balloon entered
        ///     in the same fixed step as a turn is popped or plowed where the ball's line met it (the game may
        ///     miss such a balloon), and nothing is marked <see cref="MirrorFlight.Unplaceable" />.
        /// </summary>
        internal bool FliesEveryFlight { get; set; }

        /// <summary>
        ///     On, the ball is fired already cruising and piercing by its cruise, as the view leaves one that has
        ///     bounced between empty walls often enough: a pierce no Snipe gave, with no speed buff.
        /// </summary>
        internal bool StartsOnACruisesPierce { get; set; }

        /// <summary>
        ///     For measuring what the bank is worth: the most charges a flight's cuts may bank it, the rest
        ///     being taken back after the step that banked them. 0 is the game with no bank at all; under 0, the
        ///     game as it is.
        /// </summary>
        internal int BankLimit { get; set; } = -1;

        internal MushroomLiveGame(
            Vector4 wallsClockwise, IReadOnlyList<BumperCircle> bumpers, IMushroomSettings settings,
            IBumperBounceRule rule, bool plowsToughs = false)
        {
            PlowsToughs = plowsToughs;
            _walls = new WallLimits(wallsClockwise);
            var config = Substitute.For<IProjectileFlightConfig>();
            config.LimitsClockwise.Returns(wallsClockwise);
            config.ProjectileSpeed.Returns(BaseSpeed);
            _field = new Field(bumpers);
            _resolver = new ProjectileMotionResolver(config, new ProjectileTapResolver(config), _field, rule, settings);

            var buffs = new ProjectileBuffService(_registry, Substitute.For<IPublisher<ProjectileBuffGainedMessage>>());
            buffs.Start();
            var snipeSettings = new ItemSettings();
            SetField(snipeSettings, "_type", ItemType.Snipe);
            SetField(snipeSettings.Snipe, "_snipeSpeedBuffMultiplier", LanceSpeedMultiplier);
            var itemConfig = Substitute.For<IItemConfiguration>();
            itemConfig[ItemType.Snipe].Returns(snipeSettings);
            var palette = Substitute.For<IGamePalette>();
            var dischargedSubscriber = Substitute.For<ISubscriber<PierceDischargedMessage>>();
            dischargedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<PierceDischargedMessage>>(handler => _discharged = handler),
                    Arg.Any<MessageHandlerFilter<PierceDischargedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            _snipe = new SnipeItemHandler(
                itemConfig, palette, new ItemEffectPlayer(new PoolManager(), palette), buffs, _registry,
                dischargedSubscriber, Substitute.For<ISnipeLanceArming>());
            _snipe.Start();

            var bouncedSubscriber = Substitute.For<ISubscriber<MushroomBouncedMessage>>();
            bouncedSubscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<MushroomBouncedMessage>>(handler => _bounced = handler),
                    Arg.Any<MessageHandlerFilter<MushroomBouncedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            var lancePublisher = Substitute.For<IPublisher<MushroomLanceMessage>>();
            lancePublisher.Publish(Arg.Do<MushroomLanceMessage>(OnLance));
            new MushroomChainController(bouncedSubscriber, lancePublisher, _snipe, _registry).Start();
        }

        /// <summary>
        ///     Flies one ball to its death (or <paramref name="maxSteps" />). <paramref name="startsAsLance" />
        ///     arms it in the thrower as the Sharpshooter card does. With <paramref name="onTheBoardAsLeft" /> the
        ///     caps the last flight cut stay cut; otherwise every cap is whole again first.
        /// </summary>
        internal MirrorFlight Fly(
            Vector2 origin, Vector2 direction, int shields, bool startsAsLance, IReadOnlyList<MirrorTarget> targets,
            int maxSteps = 8000, bool onTheBoardAsLeft = false)
        {
            if (!onTheBoardAsLeft)
            {
                _field.Regrow();
            }

            _flight = new MirrorFlight { Origin = origin };
            _leftTarget = -1;
            _targets.Clear();
            _hitsLeft.Clear();
            _plowed.Clear();
            for (var i = 0; i < targets.Count; i++)
            {
                _targets.Add(targets[i]);
                _hitsLeft.Add(targets[i].Hits);
                _plowed.Add(false);
            }

            var bankedWithinLimit = 0;

            var ball = new ProjectileModel { Direction = direction.normalized, Speed = BaseSpeed, IsFree = true };
            ball.ShieldsRemaining.Value = shields;
            _registry.Load(ball, null);
            if (startsAsLance)
            {
                _snipe.Arm(ball);
            }

            _registry.Launch(ball);
            if (StartsOnACruisesPierce)
            {
                ball.IsCruising.Value = true;
                ball.IsPiercing.Value = true;
            }

            Vector3 position = origin;
            ball.Flight.SegmentStartPosition = position;
            ball.Flight.LastBouncePosition = position;

            for (var i = 0; i < maxSteps && _flight.Unplaceable == MirrorUnplaceable.None; i++)
            {
                _heading = ball.Direction;
                var arrivesPiercing = ball.IsPiercing.Value;
                var chargesBefore = ball.Flight.BankedPierceCharges;
                var plowedBefore = ball.Flight.PendingPierceHits.Count;
                var step = _resolver.Step(ball, position, FixedStep);
                _flight.Seconds += FixedStep;
                _flight.LastSpeed = step.Speed;
                var hitAWall = step.Outcome == ProjectileStepOutcome.Bounced
                    || step.Outcome == ProjectileStepOutcome.Destroyed;
                if (BankLimit >= 0)
                {
                    // The charges this step's cuts banked past the limit are taken back. When the step also
                    // reached a wall with toughs plowed and no charge the limit allows, the pierce it kept
                    // armed is ended, as SpendPierce would have ended it.
                    var allowed = Mathf.Min(_resolver.StepCapCuts.Count, BankLimit - bankedWithinLimit);
                    bankedWithinLimit += allowed;
                    ball.Flight.BankedPierceCharges -= _resolver.StepCapCuts.Count - allowed;
                    if (step.Outcome == ProjectileStepOutcome.Bounced && arrivesPiercing && plowedBefore > 0
                        && chargesBefore + allowed == 0)
                    {
                        ball.IsPiercing.Value = false;
                    }
                }

                if (hitAWall)
                {
                    _flight.Walls++;
                    _flight.WallsReachedAsLance += arrivesPiercing ? 1 : 0;
                    if (_flight.Walls == 1)
                    {
                        _flight.LanceAtFirstWall = arrivesPiercing;
                        _flight.CapsCutByFirstWall = _flight.CapsCut.Count + _resolver.StepCapCuts.Count;
                    }
                }

                // As the view does: the step's bounces are copied before any is announced, the ones on the way
                // to the step's wall are announced first, then the wall is settled, then the ones off it.
                _stepBounces.Clear();
                _stepBounces.AddRange(_resolver.StepBumperBounces);
                foreach (var cut in _resolver.StepCapCuts)
                {
                    _flight.CapsCut.Add(cut.BumperId);
                }

                _stepPath.Clear();
                _stepTurns.Clear();
                _stepPath.Add(position);
                Announce(ball, step, afterWall: false);
                if (step.Outcome == ProjectileStepOutcome.Bounced || step.Outcome == ProjectileStepOutcome.Destroyed)
                {
                    if (!FliesEveryFlight && IsOnAVerticalWall(step.WallContact)
                        && IsOnAHorizontalWall(step.WallContact))
                    {
                        _flight.Unplaceable = MirrorUnplaceable.TwoWalls;
                        break;
                    }

                    // The step reports the wall for show, as the point its overshoot projects onto. Where the
                    // ball's line really met the wall is worked out from the leg it flew into it.
                    var crossing = _walls.TryFindCrossing(_stepPath[^1], _heading, out var met, out _)
                        ? met
                        : step.WallContact;
                    _flight.Turns.Add(new MirrorTurn(MirrorTurnKind.Wall, crossing));
                    _stepPath.Add(crossing);
                    _stepTurns.Add(MirrorTurnKind.Wall);
                }

                var died = step.Outcome == ProjectileStepOutcome.Destroyed;
                if (!died)
                {
                    Announce(ball, step, afterWall: true);
                    _stepPath.Add(step.Position);
                    position = step.Position;
                }

                // Every leg but the step's last, and every leg of the step the ball died in: the game has no
                // trigger left to fire for a balloon entered there.
                for (var leg = 0; leg < _stepPath.Count - (died ? 1 : 2); leg++)
                {
                    if (FliesEveryFlight)
                    {
                        PassThrough(ball, _stepPath[leg], _stepPath[leg + 1], arrivesPiercing);
                    }
                    else if (_flight.Unplaceable == MirrorUnplaceable.None
                        && FindContact(_stepPath[leg], _stepPath[leg + 1], out _, out _))
                    {
                        _flight.Unplaceable = _stepTurns[leg] == MirrorTurnKind.Bumper
                            ? MirrorUnplaceable.BalloonThenMushroom
                            : died ? MirrorUnplaceable.BalloonThenDeath : MirrorUnplaceable.BalloonThenWall;
                    }
                }

                if (hitAWall)
                {
                    Discharge(ball, died, sawThePlowed: plowedBefore > 0);
                }

                if (died)
                {
                    _flight.Died = true;
                    _registry.Land(ball);
                    break;
                }

                if (_flight.Unplaceable == MirrorUnplaceable.None)
                {
                    position = ResolveContacts(ball, _stepPath[^2], position);
                }
            }

            _flight.EndsPiercing = ball.IsPiercing.Value;
            _flight.EndsCruising = ball.IsCruising.Value;
            _flight.EndsWithASpeedBuff = ball.HasBuff(ProjectileBuffId.Speed);
            _flight.ChargesBanked = ball.Flight.CapsCut;
            _registry.Retire(ball);
            return _flight;
        }

        // The view's part of a discharge (ProjectileView.ResolvePierceAtBounce and
        // ProjectileHitResolver.DischargePending): the toughs the lance plowed on the way to this wall are
        // shattered, and when the ball lived the Snipe is told, which spends a banked charge on a lance that
        // SpendPierce kept armed for it. A ball that died at the wall takes its charges with it. When every
        // tough was plowed in the very step that reached the wall, the resolver saw none and left the pierce as
        // it was: the view's sweep finds them and spends it then, and so does this.
        private void Discharge(ProjectileModel ball, bool died, bool sawThePlowed)
        {
            var pending = ball.Flight.PendingPierceHits;
            if (pending.Count == 0)
            {
                return;
            }

            if (!died && !sawThePlowed && ball.IsPiercing.Value)
            {
                ball.SpendPierce();
            }

            var shattered = 0;
            for (var i = _targets.Count - 1; i >= 0; i--)
            {
                if (_plowed[i])
                {
                    RemoveTarget(i);
                    shattered++;
                }
            }

            pending.Clear();
            _flight.Pops += shattered;
            _flight.Discharges++;
            if (died)
            {
                return;
            }

            var charges = ball.Flight.BankedPierceCharges;
            _discharged.Handle(new PierceDischargedMessage(
                ball, ball.Flight.SegmentStartPosition, shattered, shattered, false, ball.Direction,
                ball.Flight.SegmentStartPosition));
            _flight.ChargesSpent += charges - ball.Flight.BankedPierceCharges;
        }

        // A leg the step flew before a turn, on a game that flies every flight: the balloons its line enters are
        // popped, or plowed by a lance, in order. One that would deflect the ball is left standing: the turn
        // that followed in the same step has already been flown.
        private void PassThrough(ProjectileModel ball, Vector3 from, Vector3 to, bool piercing)
        {
            for (var i = _targets.Count - 1; i >= 0; i--)
            {
                if (_plowed[i] || !Enters(from, to, i, out _))
                {
                    continue;
                }

                if (_hitsLeft[i] <= 1 || (piercing && !PlowsToughs))
                {
                    _flight.Pops++;
                    RemoveTarget(i);
                }
                else if (piercing)
                {
                    Plow(ball, i, _targets[i].Center);
                }
            }
        }

        private void Plow(ProjectileModel ball, int index, Vector3 at)
        {
            _plowed[index] = true;
            ball.Flight.PendingPierceHits.Add(new PendingPierceHit(_plowedBalloon, at));
            ball.Flight.SegmentSweepValid = false;
        }

        private void RemoveTarget(int index)
        {
            _targets.RemoveAt(index);
            _hitsLeft.RemoveAt(index);
            _plowed.RemoveAt(index);
            _leftTarget = index == _leftTarget ? -1 : index < _leftTarget ? _leftTarget - 1 : _leftTarget;
        }

        /// <summary>Leaves the board with exactly the caps of these ids cut, whatever the last flight cut.</summary>
        internal void CutOnly(IReadOnlyList<int> capIds)
        {
            _field.Regrow();
            for (var i = _field.Bumpers.Count - 1; i >= 0; i--)
            {
                for (var c = 0; c < capIds.Count; c++)
                {
                    if (_field.Bumpers[i].Id == capIds[c])
                    {
                        _field.Cut(i);
                        break;
                    }
                }
            }
        }

        private void Announce(ProjectileModel ball, in ProjectileStep step, bool afterWall)
        {
            for (var i = 0; i < _stepBounces.Count; i++)
            {
                var bounce = _stepBounces[i];
                if (bounce.AfterWall != afterWall)
                {
                    continue;
                }

                _bounced.Handle(bounce.ToMessage(ball, step.Speed));
                _flight.Turns.Add(new MirrorTurn(
                    MirrorTurnKind.Bumper, bounce.Point, bounce.BumperId, bounce.Count, bounce.Reward,
                    ball.IsPiercing.Value));
                _stepPath.Add(bounce.Point);
                _stepTurns.Add(MirrorTurnKind.Bumper);
                _heading = bounce.Outgoing;
            }
        }

        private void OnLance(MushroomLanceMessage message)
        {
            _flight.LancesArmed++;
        }

        // The trigger and what the game does with it, for the balloons the leg enters, nearest first. A pop leaves
        // the ball where the step put it; a deflect moves it, and the stretch it flies off the balloon is looked
        // along in turn.
        private Vector3 ResolveContacts(ProjectileModel ball, Vector3 legStart, Vector3 position)
        {
            while (FindContact(legStart, position, out var index, out var entry))
            {
                var target = _targets[index];
                var piercing = ball.IsPiercing.Value;
                if (piercing || _hitsLeft[index] <= 1)
                {
                    if (piercing && _hitsLeft[index] > 1 && _flight.ComparableTurns == int.MaxValue)
                    {
                        _flight.ComparableTurns = _flight.Turns.Count;
                    }

                    if (PlowsToughs && piercing && _hitsLeft[index] > 1)
                    {
                        Plow(ball, index, target.Center);
                        continue;
                    }

                    _flight.Pops++;
                    _flight.PopPoints.Add((Vector2)legStart + (((Vector2)(position - legStart)).normalized * entry));
                    RemoveTarget(index);
                    continue;
                }

                if (_hitsLeft[index] != int.MaxValue)
                {
                    _hitsLeft[index]--;
                }

                position = _resolver.Deflect(ball, position, target.Center, target.Radius + BallRadius);
                if (!FliesEveryFlight && (IsOnAVerticalWall(position) || IsOnAHorizontalWall(position)))
                {
                    _flight.Unplaceable = MirrorUnplaceable.DeflectIntoWall;
                    return position;
                }

                // ProjectileHitResolver.Resolve and ProjectileView.OnBalloonDeflected.
                ball.Flight.SegmentPopCount = 0;
                ball.Flight.SegmentSweepValid = true;
                ball.Flight.LastBouncePosition = ball.Flight.SegmentStartPosition;
                ball.Flight.ConsecutiveWallBounces = 0;
                ball.Flight.ConsecutiveSweeps = 0;
                ball.Flight.TotalCruiseTaps = 0;
                ball.IsCruising.Value = false;

                legStart = ball.Flight.SegmentStartPosition;
                _leftTarget = index;
                _leftDirection = ball.Direction;
                _flight.Turns.Add(new MirrorTurn(MirrorTurnKind.Deflect, legStart));
            }

            return position;
        }

        // The balloon whose contact circle the leg enters first: the line's first crossing of the circle, on the
        // leg or within the seam just behind its start.
        private bool FindContact(Vector2 from, Vector2 to, out int index, out float distance)
        {
            index = -1;
            distance = float.MaxValue;
            var leg = to - from;
            var length = leg.magnitude;
            if (length < 1e-7f)
            {
                return false;
            }

            var heading = leg / length;
            for (var i = 0; i < _targets.Count; i++)
            {
                // A tough a lance has plowed stands until the wall and is not met again on the way there.
                if ((i == _leftTarget && heading == _leftDirection) || _plowed[i])
                {
                    continue;
                }

                if (Enters(from, to, i, out var entry) && entry < distance)
                {
                    index = i;
                    distance = entry;
                }
            }

            return index >= 0;
        }

        // Whether the leg's line enters the target's contact circle, on the leg or within the seam just behind
        // its start, and how far along.
        private bool Enters(Vector2 from, Vector2 to, int target, out float entry)
        {
            entry = 0f;
            var leg = to - from;
            var length = leg.magnitude;
            if (length < 1e-7f)
            {
                return false;
            }

            var heading = leg / length;
            var radius = _targets[target].Radius + BallRadius;
            var toStart = from - _targets[target].Center;
            var along = Vector2.Dot(toStart, heading);
            var discriminant = (along * along) - toStart.sqrMagnitude + (radius * radius);
            if (discriminant < 0f)
            {
                return false;
            }

            entry = -along - Mathf.Sqrt(discriminant);
            return entry > -SeamOverlap && entry <= length;
        }

        private bool IsOnAVerticalWall(Vector3 point)
        {
            return Mathf.Abs(point.x - _walls.Left) < 1e-4f || Mathf.Abs(point.x - _walls.Right) < 1e-4f;
        }

        private bool IsOnAHorizontalWall(Vector3 point)
        {
            return Mathf.Abs(point.y - _walls.Bottom) < 1e-4f || Mathf.Abs(point.y - _walls.Top) < 1e-4f;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }

        private sealed class Field : IBumperField
        {
            private readonly List<BumperCircle> _whole = new();
            private readonly List<BumperCircle> _bumpers = new();

            public IReadOnlyList<BumperCircle> Bumpers => _bumpers;

            internal Field(IReadOnlyList<BumperCircle> bumpers)
            {
                _whole.AddRange(bumpers);
                _bumpers.AddRange(bumpers);
            }

            public Vector2Int SlotAt(int index)
            {
                return new Vector2Int(_bumpers[index].Id, 0);
            }

            public void Cut(int index)
            {
                _bumpers.RemoveAt(index);
            }

            internal void Regrow()
            {
                _bumpers.Clear();
                _bumpers.AddRange(_whole);
            }
        }
    }

    /// <summary>
    ///     The simulator's side of the mushroom mirror: the same board through
    ///     <see cref="ShotSimulator.Simulate" />, its path read back as turns.
    /// </summary>
    internal sealed class MushroomSimGame
    {
        private readonly Vector4 _wallsClockwise;
        private readonly WallLimits _walls;
        private readonly ShotBumperLayer _bumpers;
        private readonly List<ShotBumperBounce> _bounces = new();
        private readonly List<int> _cuts = new();
        private readonly List<Vector2> _path = new();

        /// <summary>The simulator's own result for the last flight.</summary>
        internal ShotSimulationResult Result { get; private set; }

        internal MushroomSimGame(
            Vector4 wallsClockwise, IReadOnlyList<BumperCircle> bumpers, IMushroomSettings settings,
            IBumperBounceRule rule)
        {
            _wallsClockwise = wallsClockwise;
            _walls = new WallLimits(wallsClockwise);
            _bumpers = bumpers.Count == 0
                ? null
                : new ShotBumperLayer(bumpers, rule, settings.Rules, MushroomLiveGame.LanceSpeedMultiplier)
                {
                    BounceLog = _bounces,
                    CutLog = _cuts,
                };
        }

        internal MirrorFlight Fly(
            Vector2 origin, Vector2 direction, int shields, bool startsAsLance, IReadOnlyList<MirrorTarget> targets)
        {
            // One target out of every ball's reach keeps the board from ever counting as cleared, which is where
            // the simulator stops a flight.
            var board = new ShotBalloonSnapshot[targets.Count + 1];
            for (var i = 0; i < targets.Count; i++)
            {
                board[i] = targets[i].Hits <= 1
                    ? ShotBalloonSnapshot.ForColorTarget(targets[i].Center, targets[i].Radius, "Red", 1, 1)
                    : ShotBalloonSnapshot.ForToughTarget(targets[i].Center, targets[i].Radius, 1, targets[i].Hits);
            }

            board[targets.Count] = ShotBalloonSnapshot.ForToughTarget(new Vector2(1000f, 1000f), 0.1f, 0, int.MaxValue);
            var seed = startsAsLance ? ShotFlightSeed.Armed(MushroomLiveGame.LanceSpeedMultiplier) : default;
            Result = ShotSimulator.Simulate(
                board, _wallsClockwise, origin, direction, shields, MushroomLiveGame.BallRadius,
                new ShotBalloonState[board.Length], rippleDamage: 1, pathOut: _path,
                projectileSpeed: MushroomLiveGame.BaseSpeed, seed: seed, streakGrantsShields: false,
                bumpers: _bumpers);

            var flight = new MirrorFlight
            {
                Origin = origin,
                Pops = Result.Pops,
                Died = Result.Died,
                EndsPiercing = Result.FinalPiercing,
                LancesArmed = Result.Mushrooms.LancesArmed,
                ChainsCompleted = Result.Mushrooms.ChainsCompleted,
                ChargesBanked = Result.Mushrooms.ChargesBanked,
            };
            if (_bumpers != null)
            {
                flight.CapsCut.AddRange(_cuts);
            }

            var nextBounce = 0;
            for (var i = 1; i < _path.Count; i++)
            {
                var point = _path[i];
                if (nextBounce < _bounces.Count && _bounces[nextBounce].Point == point)
                {
                    var bounce = _bounces[nextBounce++];
                    flight.Turns.Add(new MirrorTurn(
                        MirrorTurnKind.Bumper, point, bounce.BumperId, bounce.Count, bounce.Reward,
                        bounce.LeavesPiercing));
                }
                else if (IsOnAWall(point))
                {
                    flight.Turns.Add(new MirrorTurn(MirrorTurnKind.Wall, point));
                }
                else if (i + 1 < _path.Count && Turns(_path[i - 1], point, _path[i + 1]))
                {
                    flight.Turns.Add(new MirrorTurn(MirrorTurnKind.Deflect, point));
                }
                else
                {
                    flight.PopPoints.Add(point);
                }
            }

            return flight;
        }

        private bool IsOnAWall(Vector2 point)
        {
            return Mathf.Abs(point.x - _walls.Left) < 1e-4f || Mathf.Abs(point.x - _walls.Right) < 1e-4f
                || Mathf.Abs(point.y - _walls.Bottom) < 1e-4f || Mathf.Abs(point.y - _walls.Top) < 1e-4f;
        }

        // A balloon the ball popped lies on its straight path; one it deflected off is a corner.
        private static bool Turns(Vector2 before, Vector2 at, Vector2 after)
        {
            var incoming = (at - before).normalized;
            var outgoing = (after - at).normalized;
            return Vector2.Dot(incoming, outgoing) < 1f - 1e-6f;
        }
    }

    /// <summary>
    ///     How far apart the two sides of the mirror may be at a turn. They reach every point by different
    ///     arithmetic (the game in fixed steps, the simulator event to event), so they start a rounding error
    ///     apart, and a curved surface multiplies that error at every bounce: about ten times, more on a graze
    ///     and far more on a graze (the dispersion the shot geometry plan measures). So the points are held to
    ///     a bound that grows with the flight, not to one number: each turn may be <see cref="Growth" /> times
    ///     as far apart as the widest turn before it, from <see cref="Start" />. The kinds, the mushrooms'
    ///     ids, the counts and the rewards are held exactly, always. Once the two sides are
    ///     <see cref="Ceiling" /> apart the flight is no longer held: from there a real graze can differ
    ///     between them, as it would between two devices.
    /// </summary>
    internal readonly struct MirrorTolerance
    {
        public readonly float Start;
        public readonly float Growth;
        public readonly float Ceiling;

        public MirrorTolerance(float start, float growth, float ceiling)
        {
            Start = start;
            Growth = growth;
            Ceiling = ceiling;
        }

        /// <summary>For flights over a board: held until rounding has grown to half a millimetre.</summary>
        internal static MirrorTolerance Board => new(1e-4f, 2000f, 5e-4f);

        /// <summary>For a hand-built flight with nothing on the board to graze: held to its last wall.</summary>
        internal static MirrorTolerance Open => new(1e-4f, 2000f, 0.2f);
    }

    /// <summary>Holds one flight of the game against the same flight of the simulator.</summary>
    internal static class MushroomMirror
    {
        // See IsAGraze.
        private const float GrazeGrowth = 50f;
        private const float MostGapAGrazeGrowsFrom = 1e-4f;

        /// <summary>
        ///     Null when the two flights agree turn for turn (kind, point within the tolerance's growing bound,
        ///     and for a mushroom its id, the chain's count, the reward and whether the ball left a lance) and,
        ///     where the whole flight was held, in how they ended and in which caps they cut as a lance. Otherwise
        ///     what differs first. <paramref name="held" /> is how many turns were held, and
        ///     <paramref name="whole" /> whether that was the whole flight: false when a lance plowed a tough
        ///     (<see cref="MirrorFlight.ComparableTurns" />), when rounding grew past the ceiling first, or at a
        ///     real graze, of a ball off a cap or of a lance past one.
        /// </summary>
        /// <param name="bumpers">
        ///     The board's caps, to tell a real graze by. The two sides are always a rounding error apart, so a ball
        ///     that passes a cap's edge by less than that meets it on one side and misses it on the other, and a
        ///     ball in the notch between two caps of a wall meets one cap on one side and its neighbour on the
        ///     other. Neither is a rule the two sides disagree on: the flight is held up to there and no further.
        ///     With none given, every turn must agree.
        /// </param>
        internal static string Difference(
            MirrorFlight game, MirrorFlight sim, in MirrorTolerance tolerance, out int held, out bool whole,
            IReadOnlyList<BumperCircle> bumpers = null)
        {
            whole = game.ComparableTurns == int.MaxValue;
            var turns = whole ? game.Turns.Count : game.ComparableTurns;
            var allowed = tolerance.Start;
            var widest = 0f;
            for (held = 0; held < turns; held++)
            {
                if (held >= sim.Turns.Count)
                {
                    if (IsAGraze(game, held, widest, bumpers, MushroomChainState.NoBumper))
                    {
                        whole = false;
                        return null;
                    }

                    return $"turn {held}: " + Describe(game, sim, held);
                }

                var a = game.Turns[held];
                var b = sim.Turns[held];
                var apart = Vector2.Distance(a.Point, b.Point);
                if (a.Kind != b.Kind || apart > allowed || a.BumperId != b.BumperId || a.Count != b.Count
                    || a.Reward != b.Reward || a.LeavesPiercing != b.LeavesPiercing)
                {
                    if ((a.Kind != b.Kind || a.BumperId != b.BumperId)
                        && (IsAGraze(game, held, widest, bumpers, CapOf(b))
                            || IsAGraze(sim, held, widest, bumpers, CapOf(a))))
                    {
                        whole = false;
                        return null;
                    }

                    return $"turn {held}: " + Describe(game, sim, held);
                }

                widest = Mathf.Max(widest, apart);

                if (apart > tolerance.Ceiling)
                {
                    whole = false;
                    return null;
                }

                allowed = Mathf.Max(allowed, apart * tolerance.Growth);
            }

            if (!whole)
            {
                return null;
            }

            if (sim.Turns.Count != turns)
            {
                return $"the game turned {turns} times, the simulator {sim.Turns.Count}: " + Describe(game, sim, turns);
            }

            if (game.Died != sim.Died || game.Pops != sim.Pops || game.EndsPiercing != sim.EndsPiercing
                || game.LancesArmed != sim.LancesArmed)
            {
                return $"the end: the game died {game.Died}, pops {game.Pops}, lance {game.EndsPiercing}, armed "
                    + $"{game.LancesArmed}; the simulator died {sim.Died}, pops "
                    + $"{sim.Pops}, lance {sim.EndsPiercing}, armed {sim.LancesArmed}. "
                    + $"The game popped at {string.Join(" ", game.PopPoints)}; the simulator at "
                    + $"{string.Join(" ", sim.PopPoints)}. " + Describe(game, sim, turns);
            }

            var cutByOneSide = CapCutByOneSideOnly(game, sim);
            if (cutByOneSide == MushroomChainState.NoBumper)
            {
                // The same caps cut, so the same charges banked: one a cap, on both sides.
                return game.ChargesBanked == sim.ChargesBanked && game.ChargesBanked == game.CapsCut.Count
                    ? null
                    : $"the charges banked: the game {game.ChargesBanked}, the simulator {sim.ChargesBanked}, for "
                    + $"{game.CapsCut.Count} caps cut. " + Describe(game, sim, turns);
            }

            if (PassesTheEdgeOf(game, cutByOneSide, widest, bumpers))
            {
                whole = false;
                return null;
            }

            return $"the caps cut: the game {string.Join(" ", game.CapsCut)}; the simulator "
                + $"{string.Join(" ", sim.CapsCut)}. " + Describe(game, sim, turns);
        }

        // A cap one side cut and the other did not, or none. A cap is cut once a flight at the most (nothing grows
        // back within one), so the two lists are held as sets: two caps entered a rounding error apart may be cut
        // in either order.
        private static int CapCutByOneSideOnly(MirrorFlight game, MirrorFlight sim)
        {
            foreach (var cap in game.CapsCut)
            {
                if (!sim.CapsCut.Contains(cap))
                {
                    return cap;
                }
            }

            foreach (var cap in sim.CapsCut)
            {
                if (!game.CapsCut.Contains(cap))
                {
                    return cap;
                }
            }

            return MushroomChainState.NoBumper;
        }

        // Whether some straight leg of the flight passes the edge of the cap within the reach of IsAGraze: a lance
        // whose line touches a cap's circle there cuts it on one side and passes it by on the other, which is
        // rounding and no rule.
        private static bool PassesTheEdgeOf(
            MirrorFlight flight, int capId, float widest, IReadOnlyList<BumperCircle> bumpers)
        {
            if (bumpers == null)
            {
                return false;
            }

            var reach = (GrazeGrowth * Mathf.Min(widest, MostGapAGrazeGrowsFrom)) + 1e-5f;
            foreach (var cap in bumpers)
            {
                if (cap.Id != capId)
                {
                    continue;
                }

                var from = flight.Origin;
                foreach (var turn in flight.Turns)
                {
                    var leg = turn.Point - from;
                    var length = leg.magnitude;
                    if (length > 1e-6f)
                    {
                        var heading = leg / length;
                        var toCentre = cap.Center - from;
                        var along = Vector2.Dot(toCentre, heading);
                        var miss = Mathf.Abs((toCentre.x * heading.y) - (toCentre.y * heading.x));
                        if (along > -cap.Radius && along < length + cap.Radius && Mathf.Abs(cap.Radius - miss) < reach)
                        {
                            return true;
                        }
                    }

                    from = turn.Point;
                }
            }

            return false;
        }

        // The cap a turn is on, or none for a turn that is not a mushroom's.
        private static int CapOf(in MirrorTurn turn)
        {
            return turn.Kind == MirrorTurnKind.Bumper ? turn.BumperId : MushroomChainState.NoBumper;
        }

        // Whether the flight's turn is a cap met where rounding alone decides it: at the cap's very edge (the other
        // side passed it by), or in the notch where its circle crosses that of the cap the other side met
        // (otherCap; none when the other side met no cap there). Only those two caps are looked at: a turn that
        // happens to lie near some third cap's rim explains nothing.
        //
        // How near counts: within fifty times how far apart the two sides have already been, since a bounce off a
        // round cap turns a sideways error into one some tens of times as wide by the next cap (about twice the
        // leg between them over the cap's radius). That gap is capped at a tenth of a millimetre before it is
        // multiplied, so the reach is never over half a centimetre however far a long flight has drifted, and it
        // is never under a hundredth of a millimetre.
        internal static bool IsAGraze(
            MirrorFlight flight, int index, float widest, IReadOnlyList<BumperCircle> bumpers, int otherCap)
        {
            if (bumpers == null || index <= 0 || index >= flight.Turns.Count
                || flight.Turns[index].Kind != MirrorTurnKind.Bumper)
            {
                return false;
            }

            var turn = flight.Turns[index];
            var reach = (GrazeGrowth * Mathf.Min(widest, MostGapAGrazeGrowsFrom)) + 1e-5f;
            var from = flight.Turns[index - 1].Point;
            var heading = (turn.Point - from).normalized;
            foreach (var cap in bumpers)
            {
                if (cap.Id == turn.BumperId)
                {
                    var toCentre = cap.Center - from;
                    var miss = Mathf.Abs((toCentre.x * heading.y) - (toCentre.y * heading.x));
                    if (cap.Radius - miss < reach)
                    {
                        return true;
                    }
                }
                else if (cap.Id == otherCap && Mathf.Abs(Vector2.Distance(turn.Point, cap.Center) - cap.Radius) < reach)
                {
                    return true;
                }
            }

            return false;
        }

        // The turn that differs, and every turn before it with how far apart the two sides were there.
        private static string Describe(MirrorFlight game, MirrorFlight sim, int index)
        {
            var a = index >= 0 && index < game.Turns.Count ? game.Turns[index].ToString() : "nothing";
            var b = index >= 0 && index < sim.Turns.Count ? sim.Turns[index].ToString() : "nothing";
            var text = new System.Text.StringBuilder($"the game {a}; the simulator {b}. Before it:");
            for (var i = 0; i < index && i < game.Turns.Count && i < sim.Turns.Count; i++)
            {
                text.Append($" [{i}] {game.Turns[i]} apart {Vector2.Distance(game.Turns[i].Point, sim.Turns[i].Point):E1}");
            }

            return text.ToString();
        }
    }
}
