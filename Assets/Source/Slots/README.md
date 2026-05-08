# Slots

The grid that holds all balloons in play.

The grid is a two-dimensional space of slots arranged in a staggered pattern (odd rows offset by half a column). Each slot is either empty or occupied by a balloon. The grid knows how to convert a slot coordinate into a world position, which slots are unbalanced (missing support from above), and what the best empty slot is for a balloon to move into.

Other systems talk to the grid to place or remove balloons, to query neighbours for power-up propagation, and to find spawn positions for new balloon lines.


