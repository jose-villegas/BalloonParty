# Shared

Types and utilities used across multiple features.

`IGameConfiguration` is the single source of truth for all game data — slot dimensions, balloon colors, timing values, spawn counts. Any system that needs game data gets it from here, never from hardcoded values or duplicated fields.

Messages are the signals that systems use to communicate with each other without being directly coupled. A balloon is hit, a projectile is fired, a power-up is activated — each is a message that any interested system can react to.

Extensions are small helpers that make common operations read naturally at the call site — things like converting a slot index to a world position.


