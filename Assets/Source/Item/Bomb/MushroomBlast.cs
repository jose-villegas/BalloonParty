using BalloonParty.Configuration.GridActors;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UnityEngine;

namespace BalloonParty.Item.Bomb
{
    /// <summary>
    ///     What a blast does to the meadow's mushrooms (@ref plan_meadow_mushrooms): a bomb blows the caps off the
    ///     ones next to it and sways the others it reaches, and a nuke blows off every cap its blast reaches.
    ///     Told by the bomb's handler and by the nuke's blast as they go off; it has no tick and keeps nothing.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Which caps a bomb takes</b> is the bomb's own rule for what it breaks outright, the one that
    ///         breaks an unbreakable balloon (<see cref="BombBlast.Pierces" />): the mushrooms in the six slots
    ///         next to the bomb's, and for a rainbow bomb the ones whose centre is within its kill radius, which
    ///         on the shipped board are the same six. Nothing of it is a number of the mushrooms' own.
    ///     </para>
    ///     <para>
    ///         <b>Which caps sway.</b> Every other whole cap the blast reaches as it reaches a balloon: its
    ///         circle overlaps the kill radius (<see cref="MushroomBlastRule.SwayReach" />, widened or switched
    ///         off by <see cref="IMushroomSettings.Blast" />). The sway is 1 at the distance of the slots next to
    ///         the bomb and falls to 0 at the reach.
    ///     </para>
    ///     <para>
    ///         A cap that is blown off is cut out of the bumper field (<see cref="IBumperField.Cut" />) exactly
    ///         as a lance's cut takes it out, and announced with <see cref="MushroomCutCause.Blast" />. So
    ///         everything that follows a cut follows a blast with nothing more to build: nothing bounces off the
    ///         stem, the kick and the lookahead no longer see the cap, and it grows back on the board's beat, the
    ///         fifth after it went, a beat later while a ball is in its way.
    ///     </para>
    ///     <para>
    ///         A blast is no hit of anyone's chain: no flight is told, so a ball in flight keeps the count it
    ///         has and simply finds the caps gone. A cap already cut is not in the field, so a blast leaves it
    ///         as it is: its wait is not started again, and it is not swayed.
    ///     </para>
    /// </remarks>
    internal sealed class MushroomBlast : IMushroomBlast
    {
        private readonly IBumperField _field;
        private readonly SlotGrid _grid;
        private readonly IMushroomSettings _settings;
        private readonly IPublisher<MushroomCutMessage> _cutPublisher;
        private readonly IPublisher<MushroomSwayedMessage> _swayedPublisher;

        internal MushroomBlast(
            IBumperField field, SlotGrid grid, IMushroomSettings settings,
            IPublisher<MushroomCutMessage> cutPublisher, IPublisher<MushroomSwayedMessage> swayedPublisher)
        {
            _field = field;
            _grid = grid;
            _settings = settings;
            _cutPublisher = cutPublisher;
            _swayedPublisher = swayedPublisher;
        }

        public void Bomb(Vector2 centre, Vector2Int bombSlot, bool isRainbow, float killRadius)
        {
            var bumpers = _field.Bumpers;
            if (bumpers.Count == 0)
            {
                return;
            }

            var blast = new BombBlastParams(killRadius, 0f);
            var reach = MushroomBlastRule.SwayReach(killRadius, _settings.Radius, _settings.Blast);
            var nearest = Vector2.Distance(
                _grid.IndexToWorldPosition(bombSlot),
                _grid.IndexToWorldPosition(new Vector2Int(bombSlot.x + 1, bombSlot.y)));

            // Backwards: a cap that is blown off leaves the list, and the ones before it keep their place.
            for (var i = bumpers.Count - 1; i >= 0; i--)
            {
                var cap = bumpers[i];
                var slot = _field.SlotAt(i);

                // By the cap's centre, with no radius of its own: a bumper's circle carries the ball's radius
                // too, which is no part of the mushroom, and a neighbour's centre is well inside the blast.
                if (BombBlast.Pierces(centre, bombSlot, isRainbow, in blast, slot, cap.Center, 0f))
                {
                    BlowOffAt(i, centre);
                    continue;
                }

                var strength = MushroomBlastRule.SwayStrength(Vector2.Distance(centre, cap.Center), nearest, reach);
                if (strength > 0f)
                {
                    _swayedPublisher.Publish(
                        new MushroomSwayedMessage(slot, MushroomBlastRule.Away(centre, cap.Center), strength));
                }
            }
        }

        public void BlowOff(Vector2Int slot, Vector2 centre)
        {
            var bumpers = _field.Bumpers;
            for (var i = 0; i < bumpers.Count; i++)
            {
                if (_field.SlotAt(i) == slot)
                {
                    BlowOffAt(i, centre);
                    return;
                }
            }
        }

        // Out of the field first, then told: whoever hears of the cap finds it already gone, as with a lance's
        // cut. A field that kept the cap (it could not cut it) is not announced.
        private void BlowOffAt(int index, Vector2 centre)
        {
            var bumpers = _field.Bumpers;
            var whole = bumpers.Count;
            var cap = bumpers[index];
            var slot = _field.SlotAt(index);
            _field.Cut(index);
            if (bumpers.Count >= whole)
            {
                return;
            }

            _cutPublisher.Publish(new MushroomCutMessage(
                slot, cap.Center, MushroomBlastRule.Away(centre, cap.Center), MushroomCutCause.Blast));
        }
    }
}
