using System.Collections.Generic;
using BalloonParty.Balloon;
using BalloonParty.Balloon.Type;
using BalloonParty.Shared;
using BalloonParty.Slots.Grid;
using BalloonParty.Thrower;
using UnityEngine;

namespace BalloonParty.Item.Shield
{
    /// <summary>
    ///     Which slots an affordable shot can still reach, so the balancer can prefer keeping a shield
    ///     somewhere collectable as the board settles.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A chain is planned once, at spawn, against the board as it will settle. Then balloons
    ///         move: they rise into gaps, get shoved, and the shot that collected the chain stops
    ///         working. This is the counter-pressure — not a re-plan, just a nudge that makes a
    ///         shield-carrying balloon prefer slots a shot still passes through.
    ///     </para>
    ///     <para>
    ///         Costs one fan sweep per grid mutation, and each leg of each flight tests every slot on
    ///         the board — so roughly <c>fan x legs x columns x rows</c> ray-circle tests per mutation,
    ///         bounded by board area, not by the fan alone. Cheap at a 6x11 grid; the thing to watch if
    ///         either the board or the fan grows.
    ///     </para>
    ///     <para>
    ///         Bounces off walls only, unlike <c>ShieldChainPlanner</c>, which also deflects off toughs.
    ///         Deliberate: the table is cached on <see cref="SlotGrid.MutationVersion" />, and walls are
    ///         the only reflectors that hold still between mutations — a deflector's geometry comes from
    ///         its view, which drifts as the board settles with nothing to invalidate on. So the nudge
    ///         under-values slots reachable only via a deflection, and that is the accepted cost of it
    ///         being a cached bias rather than a per-frame solve. Add deflectors here and you are
    ///         caching positions that are already wrong.
    ///     </para>
    /// </remarks>
    internal sealed class ShieldReachabilityField
    {
        // Unreached slots read as this, so callers can treat "far" and "impossible" the same way.
        internal const int Unreachable = int.MaxValue;

        private const float SurfaceEpsilon = 1e-3f;
        private const int MaxLegs = 12;

        private readonly SlotGrid _grid;
        private readonly IProjectileFlightConfig _flightConfig;
        private readonly BalloonContactRadii _radii;
        private readonly ThrowerOriginProvider _origin;
        private readonly IShieldChainSettings _settings;

        private readonly List<Vector2> _fan = new();

        private int[,] _reflectionsToReach;
        private int _builtAtVersion = -1;

        internal ShieldReachabilityField(
            SlotGrid grid, IProjectileFlightConfig flightConfig, BalloonContactRadii radii,
            ThrowerOriginProvider origin, IRunConfig runConfig)
        {
            _grid = grid;
            _flightConfig = flightConfig;
            _radii = radii;
            _origin = origin;
            _settings = runConfig.ShieldChain;
        }

        /// <summary>
        ///     Fewest reflections any sampled shot needs to cross <paramref name="slot" />, or
        ///     <see cref="Unreachable" />. Zero means a straight shot reaches it.
        /// </summary>
        internal int ReflectionsToReach(Vector2Int slot)
        {
            EnsureBuilt();
            if (_reflectionsToReach == null
                || slot.x < 0 || slot.x >= _reflectionsToReach.GetLength(0)
                || slot.y < 0 || slot.y >= _reflectionsToReach.GetLength(1))
            {
                return Unreachable;
            }

            return _reflectionsToReach[slot.x, slot.y];
        }

        private void EnsureBuilt()
        {
            // Rebuilt per grid mutation, the same trigger MoveWeightEvaluator memoises on: every slot
            // a flight crosses depends on where the balloons currently are.
            if (_builtAtVersion == _grid.MutationVersion || !_origin.IsAvailable)
            {
                return;
            }

            _builtAtVersion = _grid.MutationVersion;
            EnsureBuffers();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    _reflectionsToReach[col, row] = Unreachable;
                }
            }

            BuildFan();

            // A shield sits on an ordinary balloon far more often than not, and this field asks
            // where a shot CAN go rather than what it hits — one representative circle is enough, and
            // it is the authored one rather than the slot pitch.
            var walls = new WallLimits(_flightConfig.LimitsClockwise);
            var radius = _radii.For(BalloonType.Simple) + _origin.ProjectileContactRadius;
            for (var i = 0; i < _fan.Count; i++)
            {
                MarkFlight(walls, _origin.Origin, _fan[i], radius);
            }
        }

        private void EnsureBuffers()
        {
            if (_reflectionsToReach == null
                || _reflectionsToReach.GetLength(0) != _grid.Columns
                || _reflectionsToReach.GetLength(1) != _grid.Rows)
            {
                _reflectionsToReach = new int[_grid.Columns, _grid.Rows];
            }
        }

        // Walks one flight and stamps every slot whose circle it crosses, cheapest reflection count
        // wins. Shields are not credited here: this asks where a shot CAN go, not how far a planned
        // chain carries it.
        private void MarkFlight(in WallLimits walls, Vector2 origin, Vector2 heading, float radius)
        {
            var position = origin;
            var direction = heading.normalized;
            var reflections = 0;

            for (var leg = 0; leg < MaxLegs && reflections <= _settings.ReachabilityMaxReflections; leg++)
            {
                if (!walls.TryFindCrossing(position, direction, out var crossing, out var wallNormal))
                {
                    return;
                }

                var legLength = Vector2.Distance(position, crossing);
                MarkSlotsOnLeg(position, direction, legLength, radius, reflections);

                position = walls.ClampInside(crossing) + (wallNormal.normalized * SurfaceEpsilon);
                direction = Vector2.Reflect(direction, wallNormal.normalized);
                reflections++;
            }
        }

        private void MarkSlotsOnLeg(
            Vector2 position, Vector2 direction, float legLength, float radius, int reflections)
        {
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    if (_reflectionsToReach[col, row] <= reflections)
                    {
                        continue;
                    }

                    var centre = (Vector2)_grid.IndexToWorldPosition(new Vector2Int(col, row));
                    if (CircleContact.TryFindEntry(position, direction, centre, radius, out _, out var entry)
                        && entry <= legLength)
                    {
                        _reflectionsToReach[col, row] = reflections;
                    }
                }
            }
        }

        private void BuildFan()
        {
            _fan.Clear();
            var samples = _settings.ReachabilityFanSamples;
            for (var i = 0; i < samples; i++)
            {
                var t = (float)i / (samples - 1);
                var degrees = Mathf.Lerp(_settings.FanMinDegrees, _settings.FanMaxDegrees, t);
                _fan.Add(new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)));
            }
        }
    }
}
