using System;
using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Projectile
{
    /// <summary>
    ///     Which shots exist right now: the one held in the thrower, and every one in flight (oldest
    ///     first). The single source for "is anything flying" and "where is it" — never cache the
    ///     last-loaded model as "the" shot, since a fresh one can load while earlier ones still fly.
    ///     A ball leaves in two steps. <b>Land</b>: it stops being loaded or in flight, so every query
    ///     already sees it gone while its <c>ProjectileDestroyedMessage</c> is dispatched. <b>Retire</b>:
    ///     after that dispatch, <see cref="Retired" /> fires and per-ball stores drop its state. Every
    ///     exit retires, including the ones that publish no Destroyed (game over, reset, a replaced held
    ///     ball).
    /// </summary>
    internal interface IProjectileRegistry
    {
        /// <summary>The shot held in the thrower, or null while it's empty.</summary>
        IProjectileModel Loaded { get; }

        int InFlightCount { get; }

        IProjectileModel InFlightAt(int index);

        Vector3 InFlightPositionAt(int index);

        bool IsInFlight(IProjectileModel projectile);

        /// <summary>
        ///     Fires once per ball, when it retires. The one place per-ball state is dropped, so no store
        ///     invents its own lifetime.
        /// </summary>
        IObservable<IProjectileModel> Retired { get; }

        /// <summary>Loaded or in flight.</summary>
        bool IsAlive(IProjectileModel projectile);

        /// <summary>Loaded, in flight, or landed and not yet retired.</summary>
        bool IsTracked(IProjectileModel projectile);

        /// <summary>
        ///     The shot an effect should land on. A given shot is returned only while it is alive, never
        ///     swapped for another ball. With no shot (an effect no ball set off), the latest in flight,
        ///     else the loaded one.
        /// </summary>
        IProjectileModel ShotOrCurrent(IProjectileModel shot);

        /// <summary>The most recently fired shot still in flight — what single-target readers (the
        /// level-up camera, item facing) follow.</summary>
        bool TryGetLatestInFlight(out IProjectileModel projectile, out Vector3 position);
    }
}
