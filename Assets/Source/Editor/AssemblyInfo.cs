using System.Runtime.CompilerServices;

// Mirrors Assets/Source/AssemblyInfo.cs (BalloonParty.Runtime) — lets EditMode tests reach
// internal editor-only types, currently just the shot solver's pure simulator core
// (BalloonParty.Editor.ShotSolver.ShotSimulator and friends).
[assembly: InternalsVisibleTo("BalloonParty.Tests.EditMode")]
