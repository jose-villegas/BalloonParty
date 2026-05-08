# Debug

Runtime cheat console for testing and debugging without modifying game state manually.

Press **backtick (`)** in Play Mode to toggle the console window. The console GameObject is created automatically at runtime — no scene setup required. The console discovers all registered cheats automatically and organises them by section and tag.

## Build visibility

The entire `Debug/` system — console, `ICheat`, and all cheat implementations — is wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. It compiles out completely in release builds. Registration in `GameLifetimeScope` is guarded by the same directive.

## Adding a new cheat

1. Create a class implementing `ICheat` — give it a `Name`, a `Section`, and one or more `Tags`.
2. Inject whatever publishers or services it needs via the constructor.
3. Register it in `GameLifetimeScope`:
   ```csharp
   builder.Register<YourCheat>(Lifetime.Singleton).AsImplementedInterfaces();
   ```
That's all. The console picks it up automatically.

## Console features

- **Search** — filters cheats by name as you type
- **Tag pills** — click a tag to filter to only cheats with that tag; click again or "All" to clear
- **Sections** — cheats are grouped by their `Section` property
- **Favorites** — click ☆ next to any cheat to pin it to the top of the list

