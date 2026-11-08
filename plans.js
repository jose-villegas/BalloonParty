var plans =
[
    [ "Plans", "plans.html#autotoc_md790", null ],
    [ "Bubble Cluster Hit Feedback", "plan_bubble_cluster_hit_feedback.html", [
      [ "Bubble Cluster Hit Feedback", "plan_bubble_cluster_hit_feedback.html#autotoc_md321", [
        [ "Goals", "plan_bubble_cluster_hit_feedback.html#autotoc_md323", null ],
        [ "Root cause of the existing laser rotation bug", "plan_bubble_cluster_hit_feedback.html#autotoc_md325", null ],
        [ "New interface — IBalloonHitHandler", "plan_bubble_cluster_hit_feedback.html#autotoc_md327", null ],
        [ "Discovery and caching", "plan_bubble_cluster_hit_feedback.html#autotoc_md329", [
          [ "Root components — cache at Bind() time", "plan_bubble_cluster_hit_feedback.html#autotoc_md330", null ],
          [ "Item visual components — register via ItemDisplayService", "plan_bubble_cluster_hit_feedback.html#autotoc_md331", null ],
          [ "What goes away", "plan_bubble_cluster_hit_feedback.html#autotoc_md332", null ]
        ] ],
        [ "BalloonView changes", "plan_bubble_cluster_hit_feedback.html#autotoc_md334", [
          [ "New methods", "plan_bubble_cluster_hit_feedback.html#autotoc_md335", null ],
          [ "Removed", "plan_bubble_cluster_hit_feedback.html#autotoc_md336", null ],
          [ "Internal", "plan_bubble_cluster_hit_feedback.html#autotoc_md337", null ]
        ] ],
        [ "BalloonController changes", "plan_bubble_cluster_hit_feedback.html#autotoc_md339", null ],
        [ "SoapBubbleClusterVariant — implements IBalloonHitHandler", "plan_bubble_cluster_hit_feedback.html#autotoc_md341", [
          [ "C# mirror of shader bubble layouts", "plan_bubble_cluster_hit_feedback.html#autotoc_md342", null ],
          [ "GetVfxWorldPosition", "plan_bubble_cluster_hit_feedback.html#autotoc_md343", null ],
          [ "OnHit — spin impulse", "plan_bubble_cluster_hit_feedback.html#autotoc_md344", null ],
          [ "OnPrePop", "plan_bubble_cluster_hit_feedback.html#autotoc_md345", null ]
        ] ],
        [ "LaserItemRotation — migrates to IBalloonHitHandler", "plan_bubble_cluster_hit_feedback.html#autotoc_md347", null ],
        [ "File summary", "plan_bubble_cluster_hit_feedback.html#autotoc_md349", null ],
        [ "Open questions", "plan_bubble_cluster_hit_feedback.html#autotoc_md351", null ]
      ] ]
    ] ],
    [ "Grid Actor Expansion — Phase 8+", "plan_grid_actor_expansion.html", [
      [ "Grid Actor Expansion — Phase 8+", "plan_grid_actor_expansion.html#autotoc_md554", [
        [ "Orientation", "plan_grid_actor_expansion.html#autotoc_md556", null ],
        [ "Actor Vocabulary — Design Reference", "plan_grid_actor_expansion.html#autotoc_md558", [
          [ "Balloon archetypes", "plan_grid_actor_expansion.html#autotoc_md559", null ],
          [ "Grid actor archetypes", "plan_grid_actor_expansion.html#autotoc_md560", null ],
          [ "Hit controller pattern for non-balloon actors", "plan_grid_actor_expansion.html#autotoc_md561", null ]
        ] ],
        [ "Phases", "plan_grid_actor_expansion.html#autotoc_md563", [
          [ "✅ Phase 8.0 — Spawner Coordination", "plan_grid_actor_expansion.html#autotoc_md565", [
            [ "What was built", "plan_grid_actor_expansion.html#autotoc_md566", null ]
          ] ],
          [ "✅ Phase 8.1a — Absorb Routing", "plan_grid_actor_expansion.html#autotoc_md568", [
            [ "What was built", "plan_grid_actor_expansion.html#autotoc_md569", null ]
          ] ],
          [ "✅ Phase 8.1b — DamageContext API Migration", "plan_grid_actor_expansion.html#autotoc_md571", [
            [ "What was built", "plan_grid_actor_expansion.html#autotoc_md572", null ]
          ] ],
          [ "Phase 8.1c — UnbreakableBalloonModel + BalloonModelBase Cleanup", "plan_grid_actor_expansion.html#autotoc_md574", [
            [ "UnbreakableBalloonModel", "plan_grid_actor_expansion.html#autotoc_md575", null ],
            [ "ScoreValue on BalloonModelBase", "plan_grid_actor_expansion.html#autotoc_md576", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md577", null ]
          ] ],
          [ "Phase 8.2a — Structural Actors (Puff + Bush)", "plan_grid_actor_expansion.html#autotoc_md579", [
            [ "Folder structure", "plan_grid_actor_expansion.html#autotoc_md580", null ],
            [ "Files", "plan_grid_actor_expansion.html#autotoc_md581", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md582", null ]
          ] ],
          [ "Phase 8.2b — Indestructible Hitable Actors (Deflector + Absorber)", "plan_grid_actor_expansion.html#autotoc_md584", [
            [ "Files", "plan_grid_actor_expansion.html#autotoc_md585", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md586", null ]
          ] ],
          [ "Phase 8.2c — Gatekeeper + GridActorHitController", "plan_grid_actor_expansion.html#autotoc_md588", [
            [ "Files", "plan_grid_actor_expansion.html#autotoc_md589", null ],
            [ "GatekeeperActorModel", "plan_grid_actor_expansion.html#autotoc_md590", null ],
            [ "GridActorHitController", "plan_grid_actor_expansion.html#autotoc_md591", null ],
            [ "NudgeOverrides cleanup on BalloonModelBase", "plan_grid_actor_expansion.html#autotoc_md592", null ],
            [ "NudgeService decoupling (done alongside 8.2c)", "plan_grid_actor_expansion.html#autotoc_md593", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md594", null ]
          ] ],
          [ "Phase 8.3 — Procedural Placement Engine", "plan_grid_actor_expansion.html#autotoc_md596", [
            [ "Design", "plan_grid_actor_expansion.html#autotoc_md597", null ],
            [ "Migration path", "plan_grid_actor_expansion.html#autotoc_md598", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md599", null ]
          ] ],
          [ "Phase 8.4 — Difficulty + Level Coupling", "plan_grid_actor_expansion.html#autotoc_md601", [
            [ "Design", "plan_grid_actor_expansion.html#autotoc_md602", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md603", null ]
          ] ]
        ] ],
        [ "Open Questions", "plan_grid_actor_expansion.html#autotoc_md605", null ],
        [ "Current State", "plan_grid_actor_expansion.html#autotoc_md607", null ]
      ] ]
    ] ],
    [ "Content Production Plan — Pre-8.3 Assets", "plan_content_production.html", [
      [ "Content Production Plan — Pre-8.3 Assets", "plan_content_production.html#autotoc_md373", [
        [ "Context", "plan_content_production.html#autotoc_md375", null ],
        [ "Asset status overview", "plan_content_production.html#autotoc_md377", null ],
        [ "Shared infrastructure needed", "plan_content_production.html#autotoc_md379", [
          [ "IHasScoreColor — score attribution ✅ Complete", "plan_content_production.html#autotoc_md380", null ],
          [ "GridActorConfiguration ScriptableObject", "plan_content_production.html#autotoc_md382", null ],
          [ "GridActorView prefab root pattern", "plan_content_production.html#autotoc_md383", null ]
        ] ],
        [ "Per-actor detail", "plan_content_production.html#autotoc_md385", [
          [ "Soap Cluster ✅ Done", "plan_content_production.html#autotoc_md387", null ],
          [ "Puff ✅ Done", "plan_content_production.html#autotoc_md389", null ],
          [ "Bush ✅ Done", "plan_content_production.html#autotoc_md391", null ],
          [ "Deflector", "plan_content_production.html#autotoc_md393", null ],
          [ "Absorber", "plan_content_production.html#autotoc_md395", null ],
          [ "Gatekeeper", "plan_content_production.html#autotoc_md397", null ]
        ] ],
        [ "Suggested production order", "plan_content_production.html#autotoc_md399", null ],
        [ "Asset folder conventions", "plan_content_production.html#autotoc_md401", null ],
        [ "Open questions for art direction", "plan_content_production.html#autotoc_md403", null ]
      ] ]
    ] ],
    [ "Future Ideas & Improvements", "plan_future_ideas.html", [
      [ "Future Ideas & Improvements", "plan_future_ideas.html#autotoc_md422", [
        [ "1 — VFX Improvements", "plan_future_ideas.html#autotoc_md424", [
          [ "1.1 Unbreakable Pop VFX — Falling Debris", "plan_future_ideas.html#autotoc_md425", null ],
          [ "1.2 Soap Cluster Pop VFX", "plan_future_ideas.html#autotoc_md426", null ],
          [ "1.3 Deflector Bounce VFX", "plan_future_ideas.html#autotoc_md427", null ],
          [ "1.4 Absorber Consume VFX", "plan_future_ideas.html#autotoc_md428", null ],
          [ "1.5 Gatekeeper Hit + Break VFX", "plan_future_ideas.html#autotoc_md429", null ]
        ] ],
        [ "2 — Spawn Weights, Pity System & Streak Balancing", "plan_future_ideas.html#autotoc_md431", [
          [ "2.1 Per-Level Balloon & Item Weights", "plan_future_ideas.html#autotoc_md432", null ],
          [ "2.2 Pity System for Weight Randoms", "plan_future_ideas.html#autotoc_md433", null ],
          [ "2.3 Color Streak Balancing", "plan_future_ideas.html#autotoc_md434", null ],
          [ "2.4 Full Grid Clear-Out Calculation", "plan_future_ideas.html#autotoc_md435", null ]
        ] ],
        [ "3 — Custom Level Editor", "plan_future_ideas.html#autotoc_md437", [
          [ "3.1 Level Sequence Model", "plan_future_ideas.html#autotoc_md438", null ],
          [ "3.2 LevelDefinition ScriptableObject", "plan_future_ideas.html#autotoc_md439", null ],
          [ "3.3 Custom Level Editor Window (Unity Editor)", "plan_future_ideas.html#autotoc_md440", null ],
          [ "3.4 Level Sequence Integration", "plan_future_ideas.html#autotoc_md441", null ]
        ] ],
        [ "4 — Tutorial System & Pacing", "plan_future_ideas.html#autotoc_md443", [
          [ "4.1 Tutorial Trigger System", "plan_future_ideas.html#autotoc_md444", null ],
          [ "4.2 Tutorial Sequence Definition", "plan_future_ideas.html#autotoc_md445", null ],
          [ "4.3 Pacing — Suggested Actor Introduction Order", "plan_future_ideas.html#autotoc_md446", null ],
          [ "4.4 Tutorial UI", "plan_future_ideas.html#autotoc_md447", null ],
          [ "4.5 Persistence", "plan_future_ideas.html#autotoc_md448", null ]
        ] ],
        [ "5 — Relocated Future Ideas", "plan_future_ideas.html#autotoc_md450", [
          [ "5.1 Soap Cluster Merge (from PLAN-ContentProduction + PLAN-GridActorExpansion)", "plan_future_ideas.html#autotoc_md451", null ],
          [ "5.2 Unbreakable Roam (from PLAN-ContentProduction) — ✅ SHIPPED 2026-07-12", "plan_future_ideas.html#autotoc_md452", null ],
          [ "5.3 New Balloon Archetypes (from PLAN-GridActorExpansion)", "plan_future_ideas.html#autotoc_md453", null ],
          [ "5.4 New Grid Actor Archetypes (from PLAN-GridActorExpansion)", "plan_future_ideas.html#autotoc_md454", null ],
          [ "5.5 IPassThrough Behaviour Extensions (from PLAN-GridActorExpansion)", "plan_future_ideas.html#autotoc_md455", null ],
          [ "5.6 Spider Web Obstacle (from PLAN-SpiderWeb)", "plan_future_ideas.html#autotoc_md456", null ]
        ] ],
        [ "6 — Puff Cloud Polish & Performance", "plan_future_ideas.html#autotoc_md458", [
          [ "6.1 Visual Polish", "plan_future_ideas.html#autotoc_md459", null ],
          [ "6.2 Performance & Device Scaling", "plan_future_ideas.html#autotoc_md460", null ],
          [ "6.3 Edge Cases", "plan_future_ideas.html#autotoc_md461", null ]
        ] ],
        [ "7 — Vertical Cloud Drift", "plan_future_ideas.html#autotoc_md463", null ],
        [ "8 — Timed Release: Balloon Pass-Through Delay", "plan_future_ideas.html#autotoc_md465", [
          [ "8.1 Current Behaviour", "plan_future_ideas.html#autotoc_md466", null ],
          [ "8.2 Proposed Flow", "plan_future_ideas.html#autotoc_md467", null ],
          [ "8.3 Model Sketch", "plan_future_ideas.html#autotoc_md468", null ],
          [ "8.4 Balancer Changes", "plan_future_ideas.html#autotoc_md469", null ],
          [ "8.5 Visual Feedback", "plan_future_ideas.html#autotoc_md470", null ],
          [ "8.6 Relationship to Existing Pass-Through Ideas", "plan_future_ideas.html#autotoc_md471", null ],
          [ "8.7 Open Questions", "plan_future_ideas.html#autotoc_md472", null ]
        ] ],
        [ "Open Questions", "plan_future_ideas.html#autotoc_md474", null ],
        [ "9 — Quality Settings System", "plan_future_ideas.html#autotoc_md476", [
          [ "9.1 Architecture", "plan_future_ideas.html#autotoc_md477", null ],
          [ "9.2 Parameter Inventory", "plan_future_ideas.html#autotoc_md478", [
            [ "Shader Keywords (GPU cost)", "plan_future_ideas.html#autotoc_md479", null ],
            [ "Disturbance Field (GPU + memory)", "plan_future_ideas.html#autotoc_md480", null ],
            [ "Unbreakable Balloon GrabPass (GPU — single highest-cost operation)", "plan_future_ideas.html#autotoc_md481", null ],
            [ "Particle VFX (GPU fill rate + CPU simulation)", "plan_future_ideas.html#autotoc_md482", null ],
            [ "Projectile Trail (GPU fill rate)", "plan_future_ideas.html#autotoc_md483", null ],
            [ "Procedural Shader Octave Count", "plan_future_ideas.html#autotoc_md484", null ],
            [ "Animation Quality", "plan_future_ideas.html#autotoc_md485", null ],
            [ "Pool Sizes", "plan_future_ideas.html#autotoc_md486", null ]
          ] ],
          [ "9.3 Suggested Tier Thresholds", "plan_future_ideas.html#autotoc_md487", null ],
          [ "9.4 Implementation Priorities", "plan_future_ideas.html#autotoc_md488", null ],
          [ "9.5 Open Questions", "plan_future_ideas.html#autotoc_md489", null ]
        ] ],
        [ "10 — Baked Noise Texture for Puff Clouds", "plan_future_ideas.html#autotoc_md491", null ],
        [ "11 — Runtime Bush Baking at Preload", "plan_future_ideas.html#autotoc_md493", null ],
        [ "12 — Losing Conditions", "plan_future_ideas.html#autotoc_md495", [
          [ "12.1 Grid encroachment — the natural fit (Puzzle Bobble lineage)", "plan_future_ideas.html#autotoc_md496", null ],
          [ "12.2 Resource economy — make the existing Shield item matter", "plan_future_ideas.html#autotoc_md497", null ],
          [ "12.3 Clock / turn pressure — score-attack flavor", "plan_future_ideas.html#autotoc_md498", null ],
          [ "12.4 Lockout / soft-lock (safety net, not a headline mechanic)", "plan_future_ideas.html#autotoc_md499", null ],
          [ "12.5 How these compose", "plan_future_ideas.html#autotoc_md500", null ]
        ] ],
        [ "13 — Roguelike Run Modifiers (Unlockables)", "plan_future_ideas.html#autotoc_md502", [
          [ "13.1 Concept", "plan_future_ideas.html#autotoc_md503", null ],
          [ "13.2 When unlocks are offered", "plan_future_ideas.html#autotoc_md504", null ],
          [ "13.3 Model sketch", "plan_future_ideas.html#autotoc_md505", null ],
          [ "13.4 Passive modifier ideas", "plan_future_ideas.html#autotoc_md506", null ],
          [ "13.5 Active \"spawn now\" + grid-effect ideas", "plan_future_ideas.html#autotoc_md507", null ],
          [ "13.6 Integration points (existing systems)", "plan_future_ideas.html#autotoc_md508", null ],
          [ "13.7 Card presentation (the open visual question)", "plan_future_ideas.html#autotoc_md509", null ],
          [ "13.8 Open questions", "plan_future_ideas.html#autotoc_md510", null ]
        ] ],
        [ "14 — Level Pacing Follow-ups (post-ship)", "plan_future_ideas.html#autotoc_md512", [
          [ "14.1 Jump-to-level cheat — ✅ SHIPPED", "plan_future_ideas.html#autotoc_md513", null ],
          [ "14.2 Extract a shared LevelPacingValidator — ✅ SHIPPED", "plan_future_ideas.html#autotoc_md514", null ],
          [ "14.3 Escalate LevelDifficultyResolver.FallbackParameters to a hard failure", "plan_future_ideas.html#autotoc_md515", null ]
        ] ],
        [ "15 — Level-Up Point Carry-Over (2026-07-18)", "plan_future_ideas.html#autotoc_md516", null ],
        [ "16 — Grand Antiprism: the 100 denomination (2026-07-19)", "plan_future_ideas.html#autotoc_md517", null ],
        [ "Bush Visual Polish", "plan_future_ideas.html#autotoc_md519", null ]
      ] ]
    ] ],
    [ "HDR Color Pipeline", "plan_hdr_color_pipeline.html", [
      [ "HDR Color Pipeline — migration plan", "plan_hdr_color_pipeline.html#autotoc_md608", [
        [ "Decision & scope", "plan_hdr_color_pipeline.html#autotoc_md610", null ],
        [ "Verified inventory (2026-07-11)", "plan_hdr_color_pipeline.html#autotoc_md611", null ],
        [ "Task plan", "plan_hdr_color_pipeline.html#autotoc_md612", [
          [ "Dependency graph", "plan_hdr_color_pipeline.html#autotoc_md613", null ],
          [ "Wave A — Linear color space (the foundation, and the cost gate)", "plan_hdr_color_pipeline.html#autotoc_md614", [
            [ "A0 — Baseline captures · P0 · S", "plan_hdr_color_pipeline.html#autotoc_md615", null ],
            [ "A1 — Flip to Linear · P0 · S (mechanical) — do NOT merge without A2", "plan_hdr_color_pipeline.html#autotoc_md616", null ],
            [ "A2 — Look re-tune · P0 · L — art-driven, art-led", "plan_hdr_color_pipeline.html#autotoc_md617", null ],
            [ "A-gate — parity/quality sign-off + perf sanity", "plan_hdr_color_pipeline.html#autotoc_md618", null ]
          ] ],
          [ "Wave B — HDR rendering + post + bloom", "plan_hdr_color_pipeline.html#autotoc_md619", [
            [ "B1 — HDR target + post scaffold · P0 · S", "plan_hdr_color_pipeline.html#autotoc_md620", null ],
            [ "B2 — Tonemapping · P0 · S", "plan_hdr_color_pipeline.html#autotoc_md621", null ],
            [ "B3 — Emissive authoring path · P1 · M", "plan_hdr_color_pipeline.html#autotoc_md622", null ],
            [ "B4 — Bloom · P1 · S–M", "plan_hdr_color_pipeline.html#autotoc_md623", null ],
            [ "B5 — Tooling: Render Maps HDR view · P2 · S (parallel after B1)", "plan_hdr_color_pipeline.html#autotoc_md624", null ],
            [ "B6 — GI/capture chain under HDR · P1 · M", "plan_hdr_color_pipeline.html#autotoc_md625", null ],
            [ "B-gate — device sign-off", "plan_hdr_color_pipeline.html#autotoc_md626", null ]
          ] ],
          [ "Wave C — HDR display output · deferred, own trigger", "plan_hdr_color_pipeline.html#autotoc_md627", null ]
        ] ],
        [ "Open questions (answer at execution time)", "plan_hdr_color_pipeline.html#autotoc_md628", null ]
      ] ]
    ] ],
    [ "Terrain & Biomes", "plan_terrain_biomes.html", [
      [ "Terrain & Biomes — scenario ground generator", "plan_terrain_biomes.html#autotoc_md750", [
        [ "Vision & constraints", "plan_terrain_biomes.html#autotoc_md752", null ],
        [ "Verified inventory (2026-07-12)", "plan_terrain_biomes.html#autotoc_md753", null ],
        [ "Architecture", "plan_terrain_biomes.html#autotoc_md754", [
          [ "Data — TerrainMapService (plain C#, VContainer)", "plan_terrain_biomes.html#autotoc_md755", null ],
          [ "Bake — one blit per level (GPU, amortizable)", "plan_terrain_biomes.html#autotoc_md756", null ],
          [ "View — TerrainView (MonoBehaviour) + runtime shader", "plan_terrain_biomes.html#autotoc_md757", null ]
        ] ],
        [ "Shader design — options per stage", "plan_terrain_biomes.html#autotoc_md758", [
          [ "S1 — Zone blending (bake-time)", "plan_terrain_biomes.html#autotoc_md759", null ],
          [ "S2 — Grass (the flagship reaction)", "plan_terrain_biomes.html#autotoc_md760", null ],
          [ "S3 — Water", "plan_terrain_biomes.html#autotoc_md761", null ],
          [ "S4 — Sand / dirt / stone / lava", "plan_terrain_biomes.html#autotoc_md762", null ],
          [ "S5 — Data layout (bake outputs)", "plan_terrain_biomes.html#autotoc_md763", null ],
          [ "S6 — Cheap density & anti-repetition (mobile)", "plan_terrain_biomes.html#autotoc_md764", null ],
          [ "S7 — Runtime composition rules", "plan_terrain_biomes.html#autotoc_md765", null ]
        ] ],
        [ "Task plan", "plan_terrain_biomes.html#autotoc_md766", [
          [ "Dependency graph", "plan_terrain_biomes.html#autotoc_md767", null ],
          [ "Wave A — data + bake", "plan_terrain_biomes.html#autotoc_md768", [
            [ "A1 — TerrainMapService + BiomeProfile config + ITerrainQuery · P0 · M · opus", "plan_terrain_biomes.html#autotoc_md769", null ],
            [ "A2 — Index-map build/upload · P0 · S · sonnet", "plan_terrain_biomes.html#autotoc_md770", null ],
            [ "A3 — Bake blit shader + TerrainBaker · P0 · M · opus", "plan_terrain_biomes.html#autotoc_md771", null ]
          ] ],
          [ "Wave B — view + reactions + GI", "plan_terrain_biomes.html#autotoc_md772", [
            [ "B1 — TerrainView + runtime shader v1 · P0 · M · sonnet", "plan_terrain_biomes.html#autotoc_md773", null ],
            [ "B2 — Disturbance reactions · P1 · M–L · opus", "plan_terrain_biomes.html#autotoc_md774", null ],
            [ "B3 — GI integration · P1 · S · sonnet", "plan_terrain_biomes.html#autotoc_md775", null ]
          ] ],
          [ "Wave C — gameplay + tooling (parallel after A1)", "plan_terrain_biomes.html#autotoc_md776", [
            [ "C1 — Placement gating seam · P1 · S–M · sonnet", "plan_terrain_biomes.html#autotoc_md777", null ],
            [ "C2 — Seed plumbing + reproduction affordance · P2 · S · haiku", "plan_terrain_biomes.html#autotoc_md778", null ],
            [ "C3 — Game Render Maps entries · P2 · S · sonnet", "plan_terrain_biomes.html#autotoc_md779", null ]
          ] ]
        ] ],
        [ "Open questions (answer at execution time)", "plan_terrain_biomes.html#autotoc_md780", null ]
      ] ]
    ] ],
    [ "Gameplay Telemetry", "plan_gameplay_telemetry.html", [
      [ "Gameplay Metrics & Telemetry", "plan_gameplay_telemetry.html#autotoc_md520", [
        [ "Status", "plan_gameplay_telemetry.html#autotoc_md522", [
          [ "Where the JSON goes", "plan_gameplay_telemetry.html#autotoc_md523", null ]
        ] ],
        [ "Goals", "plan_gameplay_telemetry.html#autotoc_md525", null ],
        [ "Requirements", "plan_gameplay_telemetry.html#autotoc_md527", [
          [ "Scopes and boundaries", "plan_gameplay_telemetry.html#autotoc_md528", null ],
          [ "Vocabulary", "plan_gameplay_telemetry.html#autotoc_md529", null ],
          [ "State machine", "plan_gameplay_telemetry.html#autotoc_md530", null ],
          [ "Fields and derivation", "plan_gameplay_telemetry.html#autotoc_md531", null ],
          [ "Read model (in-game consumption)", "plan_gameplay_telemetry.html#autotoc_md532", null ],
          [ "Lifecycle and registration", "plan_gameplay_telemetry.html#autotoc_md533", null ],
          [ "Export", "plan_gameplay_telemetry.html#autotoc_md534", null ],
          [ "Performance", "plan_gameplay_telemetry.html#autotoc_md535", null ]
        ] ],
        [ "Separability — this becomes an external library", "plan_gameplay_telemetry.html#autotoc_md538", [
          [ "What the library owns", "plan_gameplay_telemetry.html#autotoc_md539", null ],
          [ "What the game supplies", "plan_gameplay_telemetry.html#autotoc_md540", null ],
          [ "What extraction actually costs", "plan_gameplay_telemetry.html#autotoc_md541", null ],
          [ "Why the JSON stays hand-rolled", "plan_gameplay_telemetry.html#autotoc_md542", null ]
        ] ],
        [ "Risk register", "plan_gameplay_telemetry.html#autotoc_md544", null ],
        [ "Not built", "plan_gameplay_telemetry.html#autotoc_md547", [
          [ "Export decorators", "plan_gameplay_telemetry.html#autotoc_md548", [
            [ "Cadence: one queue, many triggers", "plan_gameplay_telemetry.html#autotoc_md549", null ],
            [ "Offline is a queue problem, not a timestamp problem", "plan_gameplay_telemetry.html#autotoc_md550", null ]
          ] ],
          [ "HTTP analytics sink", "plan_gameplay_telemetry.html#autotoc_md551", null ]
        ] ],
        [ "Deferred, deliberately", "plan_gameplay_telemetry.html#autotoc_md553", null ]
      ] ]
    ] ],
    [ "Performance Recovery", "plan_performance_recovery.html", [
      [ "Performance Recovery Plan — BalloonParty (Phase 2, revised 2026-07-23)", "plan_performance_recovery.html#autotoc_md677", [
        [ "Execution model & delegation matrix", "plan_performance_recovery.html#autotoc_md679", null ],
        [ "Phase 1 — Completed ✅ (spot-checked 2026-07-23: all commits exist and match)", "plan_performance_recovery.html#autotoc_md681", null ],
        [ "Step 0: Diagnose before optimizing (mandatory, do first)", "plan_performance_recovery.html#autotoc_md683", [
          [ "Preconditions — record backend and build type", "plan_performance_recovery.html#autotoc_md684", null ],
          [ "Pacing / ARR check", "plan_performance_recovery.html#autotoc_md685", null ],
          [ "CPU vs GPU classification", "plan_performance_recovery.html#autotoc_md686", null ],
          [ "Overdraw look (editor)", "plan_performance_recovery.html#autotoc_md687", null ],
          [ "AGI — Mali fragment vs bandwidth", "plan_performance_recovery.html#autotoc_md688", null ],
          [ "Thermal baseline", "plan_performance_recovery.html#autotoc_md689", null ],
          [ "Findings — first pass, 2026-07-23 (cool device, release build, live gameplay)", "plan_performance_recovery.html#autotoc_md690", null ],
          [ "Findings — second pass, 2026-07-23 evening (90s perfetto capture during live play)", "plan_performance_recovery.html#autotoc_md691", null ],
          [ "Findings — third pass, 2026-07-23 night (20-min longitudinal monitor, 15s cadence)", "plan_performance_recovery.html#autotoc_md692", null ],
          [ "Findings — fourth pass, 2026-07-23 night (simpleperf callstacks, dev build)", "plan_performance_recovery.html#autotoc_md693", null ],
          [ "Decision tree", "plan_performance_recovery.html#autotoc_md694", null ]
        ] ],
        [ "Tier 0: Free / Near-Free Wins", "plan_performance_recovery.html#autotoc_md696", [
          [ "F2 — Remove the shrink Array.Resize in TransformRibbon · sonnet · ✅ DONE (a4581825)", "plan_performance_recovery.html#autotoc_md697", null ],
          [ "F3 — SetCharArray in RollingTextAnimator · haiku · ✅ DONE (a4581825)", "plan_performance_recovery.html#autotoc_md698", null ],
          [ "F4 — Shader variant warmup · sonnet (capture = José)", "plan_performance_recovery.html#autotoc_md699", null ],
          [ "C1 + C3 — Projectile cleanups · sonnet, one session (same file)", "plan_performance_recovery.html#autotoc_md700", null ]
        ] ],
        [ "Tier 1: UI arrival storm — RESOLVED 2026-07-23: U1 reverted, U2 kept", "plan_performance_recovery.html#autotoc_md702", [
          [ "U1 — De-Animator the progress-bar hit pulse · opus", "plan_performance_recovery.html#autotoc_md703", null ],
          [ "U2 — Eliminate ProgressNotice reparenting · sonnet (+ mandatory fable review of pool consumers)", "plan_performance_recovery.html#autotoc_md704", null ]
        ] ],
        [ "Tier 2: GPU / Shader (re-prioritized for Mali-G715)", "plan_performance_recovery.html#autotoc_md706", [
          [ "G6 — fp16 in ScreenSpaceLightSmear.shader · opus, same session as G3", "plan_performance_recovery.html#autotoc_md707", null ],
          [ "G3 — 4-tap bilinear blur (Pass 1) · with G6", "plan_performance_recovery.html#autotoc_md708", null ],
          [ "G4 — BackgroundField bake fine-octave skip · SHIPPED THEN REVERTED 2026-07-23", "plan_performance_recovery.html#autotoc_md709", null ],
          [ "G1 — Blit budget controller · opus, after Step 0 confirms spikes matter", "plan_performance_recovery.html#autotoc_md710", null ],
          [ "G5 — Gradient-skip · DEFERRED — riskier than it looks (opus if ever done)", "plan_performance_recovery.html#autotoc_md711", null ]
        ] ],
        [ "Tier 3: Architectural (gated on Step 0 evidence)", "plan_performance_recovery.html#autotoc_md713", [
          [ "A4 — Quality tiers + ADPF · opus — required for ship, promoted earlier", "plan_performance_recovery.html#autotoc_md714", null ],
          [ "A1 — Render Graph prototype · opus — go/no-go, only on Step-0 GPU evidence", "plan_performance_recovery.html#autotoc_md715", null ],
          [ "A2 — SceneCapture → Renderer Feature · parked behind A1", "plan_performance_recovery.html#autotoc_md716", null ]
        ] ],
        [ "Quality vetoes — 2026-07-23 device pass (José)", "plan_performance_recovery.html#autotoc_md718", null ],
        [ "Dropped in the 2026-07-23 revision", "plan_performance_recovery.html#autotoc_md719", null ],
        [ "Investigated and Skipped (carried over)", "plan_performance_recovery.html#autotoc_md720", null ],
        [ "Dependency map & execution waves", "plan_performance_recovery.html#autotoc_md722", null ],
        [ "Verification Protocol", "plan_performance_recovery.html#autotoc_md724", null ]
      ] ]
    ] ],
    [ "Audio / SFX", "plan_audio.html", [
      [ "Audio / SFX", "plan_audio.html#autotoc_md286", [
        [ "Principles", "plan_audio.html#autotoc_md288", null ],
        [ "Architecture", "plan_audio.html#autotoc_md290", [
          [ "Folder structure", "plan_audio.html#autotoc_md291", null ],
          [ "Class diagram", "plan_audio.html#autotoc_md292", null ],
          [ "Sequence diagram — event to sound", "plan_audio.html#autotoc_md293", null ],
          [ "Dependency graph — VContainer wiring", "plan_audio.html#autotoc_md294", null ],
          [ "Voice lifecycle", "plan_audio.html#autotoc_md295", null ],
          [ "Responsibility map", "plan_audio.html#autotoc_md296", null ]
        ] ],
        [ "Channels (the pause / context answer)", "plan_audio.html#autotoc_md298", null ],
        [ "Spatialization", "plan_audio.html#autotoc_md300", null ],
        [ "SFX moment inventory", "plan_audio.html#autotoc_md302", null ],
        [ "Melodic pops (streak-driven scale)", "plan_audio.html#autotoc_md304", null ],
        [ "Phase 1 — Core loop, no message changes — SHIPPED (Steps 1-6)", "plan_audio.html#autotoc_md306", [
          [ "First-pass sounds to author", "plan_audio.html#autotoc_md307", null ],
          [ "Voice management", "plan_audio.html#autotoc_md308", null ],
          [ "Return scheduling", "plan_audio.html#autotoc_md309", null ],
          [ "Teardown", "plan_audio.html#autotoc_md310", null ],
          [ "Clip import settings (device)", "plan_audio.html#autotoc_md311", null ],
          [ "GC / hot-path", "plan_audio.html#autotoc_md312", null ]
        ] ],
        [ "Phase 2 — Fill-out + optional signals", "plan_audio.html#autotoc_md314", null ],
        [ "Phase 3 — Deferred", "plan_audio.html#autotoc_md316", null ],
        [ "Test strategy", "plan_audio.html#autotoc_md318", null ],
        [ "Open questions", "plan_audio.html#autotoc_md320", null ]
      ] ]
    ] ],
    [ "Shot Solver Accuracy", "plan_shot_solver_accuracy.html", [
      [ "Shot Solver Accuracy", "plan_shot_solver_accuracy.html#autotoc_md725", [
        [ "Diagnostic", "plan_shot_solver_accuracy.html#autotoc_md727", null ],
        [ "Goals & non-goals", "plan_shot_solver_accuracy.html#autotoc_md728", null ],
        [ "Architecture decisions (settled)", "plan_shot_solver_accuracy.html#autotoc_md729", null ],
        [ "Phases", "plan_shot_solver_accuracy.html#autotoc_md730", [
          [ "Phase 0 — Prerequisite refactors (no behavior change; the existing 22-test suite stays green)", "plan_shot_solver_accuracy.html#autotoc_md731", null ],
          [ "Phase A — Interactive static geometry (G1) — depends on 0b", "plan_shot_solver_accuracy.html#autotoc_md732", null ],
          [ "Phase B — Weight-system fidelity (G2) — depends on 0b; parallel to A", "plan_shot_solver_accuracy.html#autotoc_md733", null ],
          [ "Phase D-core — Rainbow scoring + in-sim buff state (G4-scoring, G8) — depends on 0a", "plan_shot_solver_accuracy.html#autotoc_md734", null ],
          [ "✅ Phase C — Item carriers (G3) — depends on B + D-core", "plan_shot_solver_accuracy.html#autotoc_md735", null ],
          [ "Phase E — Flight residuals (G5, G6, G7 + E4) — depends on 0a; E2 folds C6", "plan_shot_solver_accuracy.html#autotoc_md736", null ],
          [ "Phase F — Nondeterminism policy + instrumentation (G9) — last", "plan_shot_solver_accuracy.html#autotoc_md737", null ],
          [ "Phase G — Headless level diagnostics (follow-up tier; scoped 2026-07-25)", "plan_shot_solver_accuracy.html#autotoc_md738", null ]
        ] ],
        [ "Test plan (per test-everything; full detail in the review transcript)", "plan_shot_solver_accuracy.html#autotoc_md739", null ],
        [ "Verification workflow", "plan_shot_solver_accuracy.html#autotoc_md740", null ],
        [ "Open decisions", "plan_shot_solver_accuracy.html#autotoc_md741", null ],
        [ "Remaining work — detailed status (2026-07-26)", "plan_shot_solver_accuracy.html#autotoc_md742", [
          [ "E — flight residuals (next; ONE architect memo for all four, they interact)", "plan_shot_solver_accuracy.html#autotoc_md743", null ],
          [ "F — instrumentation + acceptance (after E)", "plan_shot_solver_accuracy.html#autotoc_md744", null ],
          [ "Live repoint track (any time; separable commits)", "plan_shot_solver_accuracy.html#autotoc_md745", null ],
          [ "José's gates (accumulated)", "plan_shot_solver_accuracy.html#autotoc_md746", null ],
          [ "Deferred code follow-ups (small, none blocking)", "plan_shot_solver_accuracy.html#autotoc_md747", null ],
          [ "G — headless level diagnostics (follow-up tier; unchanged spec in §4 Phase G)", "plan_shot_solver_accuracy.html#autotoc_md748", null ],
          [ "Design questions parked for José", "plan_shot_solver_accuracy.html#autotoc_md749", null ]
        ] ]
      ] ]
    ] ],
    [ "Web Demo Hosting", "plan_web_demo_hosting.html", [
      [ "Web Demo Hosting", "plan_web_demo_hosting.html#autotoc_md781", [
        [ "Why keep it", "plan_web_demo_hosting.html#autotoc_md783", null ],
        [ "What survived", "plan_web_demo_hosting.html#autotoc_md784", null ],
        [ "The recipe, if resumed", "plan_web_demo_hosting.html#autotoc_md785", null ],
        [ "The blocker", "plan_web_demo_hosting.html#autotoc_md786", null ],
        [ "Residue from the attempt", "plan_web_demo_hosting.html#autotoc_md787", null ],
        [ "If web stays dead", "plan_web_demo_hosting.html#autotoc_md788", null ],
        [ "Decision log", "plan_web_demo_hosting.html#autotoc_md789", null ]
      ] ]
    ] ],
    [ "Level-Up Timing", "plan_level_up_timing.html", [
      [ "Level-Up Timing", "plan_level_up_timing.html#autotoc_md656", [
        [ "The bug this fixes", "plan_level_up_timing.html#autotoc_md658", null ],
        [ "The model", "plan_level_up_timing.html#autotoc_md659", null ],
        [ "2.1 The signals, by name", "plan_level_up_timing.html#autotoc_md660", null ],
        [ "What this deletes", "plan_level_up_timing.html#autotoc_md661", null ],
        [ "Detect on projected progress; keep tipping identity as presentation", "plan_level_up_timing.html#autotoc_md662", null ],
        [ "The orchestrator IS the existing phase machine", "plan_level_up_timing.html#autotoc_md663", null ],
        [ "The edits", "plan_level_up_timing.html#autotoc_md664", [
          [ "6.1 TimeScaleService — exclusivity", "plan_level_up_timing.html#autotoc_md665", null ],
          [ "6.2 The two windows", "plan_level_up_timing.html#autotoc_md666", null ],
          [ "6.3 LevelController — the exact edits", "plan_level_up_timing.html#autotoc_md667", null ],
          [ "6.4 New message: LevelUpAbandonedMessage", "plan_level_up_timing.html#autotoc_md668", null ],
          [ "6.5 The holds — predicate is Phase != Playing, NEVER == Completing", "plan_level_up_timing.html#autotoc_md669", null ],
          [ "6.6 ColorProgressBar must keep drawing during Completing", "plan_level_up_timing.html#autotoc_md670", null ],
          [ "6.7 The pan-in stops pausing; the camera follows the shot", "plan_level_up_timing.html#autotoc_md671", null ]
        ] ],
        [ "Tests", "plan_level_up_timing.html#autotoc_md672", null ],
        [ "Sequencing", "plan_level_up_timing.html#autotoc_md673", null ],
        [ "Risks — playtest, not compile", "plan_level_up_timing.html#autotoc_md674", null ],
        [ "Known residual (scoped, cosmetic)", "plan_level_up_timing.html#autotoc_md675", null ],
        [ "Keep the watchdog regardless", "plan_level_up_timing.html#autotoc_md676", null ]
      ] ]
    ] ],
    [ "Item Range Preview", "plan_item_range_preview.html", [
      [ "Item Range Preview", "plan_item_range_preview.html#autotoc_md643", [
        [ "Orientation", "plan_item_range_preview.html#autotoc_md645", null ],
        [ "The seam", "plan_item_range_preview.html#autotoc_md647", null ],
        [ "Driving the pens", "plan_item_range_preview.html#autotoc_md649", null ],
        [ "Where the trigger lives", "plan_item_range_preview.html#autotoc_md651", null ],
        [ "Phases", "plan_item_range_preview.html#autotoc_md653", null ],
        [ "Open questions", "plan_item_range_preview.html#autotoc_md655", null ]
      ] ]
    ] ],
    [ "Fast-Paced Endless Mode", "plan_fast_paced.html", [
      [ "Fast-Paced Endless Mode", "plan_fast_paced.html#autotoc_md404", [
        [ "Why", "plan_fast_paced.html#autotoc_md406", null ],
        [ "The game after this plan", "plan_fast_paced.html#autotoc_md407", null ],
        [ "Phases", "plan_fast_paced.html#autotoc_md408", null ],
        [ "Phase 1: projectile identity (no behaviour change)", "plan_fast_paced.html#autotoc_md410", null ],
        [ "Phase 2: rapid fire", "plan_fast_paced.html#autotoc_md412", null ],
        [ "Phase 3: spawn clock", "plan_fast_paced.html#autotoc_md414", null ],
        [ "Phase 4: generated levels", "plan_fast_paced.html#autotoc_md416", null ],
        [ "Phase 5: checkpoint and upgrades", "plan_fast_paced.html#autotoc_md418", null ],
        [ "Risks", "plan_fast_paced.html#autotoc_md420", null ],
        [ "Open questions", "plan_fast_paced.html#autotoc_md421", null ]
      ] ]
    ] ],
    [ "Handwritten Heads-Ups", "plan_headsup.html", [
      [ "Handwritten Heads-Ups", "plan_headsup.html#autotoc_md629", [
        [ "Why", "plan_headsup.html#autotoc_md631", null ],
        [ "What the player sees", "plan_headsup.html#autotoc_md632", null ],
        [ "Architecture", "plan_headsup.html#autotoc_md633", null ],
        [ "Rendering", "plan_headsup.html#autotoc_md634", [
          [ "4.1 Write-on text", "plan_headsup.html#autotoc_md635", null ],
          [ "4.2 Pencil lines", "plan_headsup.html#autotoc_md636", null ]
        ] ],
        [ "New signals", "plan_headsup.html#autotoc_md637", null ],
        [ "What gets a heads-up", "plan_headsup.html#autotoc_md638", null ],
        [ "Phases", "plan_headsup.html#autotoc_md639", null ],
        [ "Tests", "plan_headsup.html#autotoc_md640", null ],
        [ "Risks", "plan_headsup.html#autotoc_md641", null ],
        [ "Open questions", "plan_headsup.html#autotoc_md642", null ]
      ] ]
    ] ],
    [ "Coins and the Card Shop", "plan_coins.html", [
      [ "Coins and the Card Shop", "plan_coins.html#autotoc_md362", [
        [ "Why", "plan_coins.html#autotoc_md364", null ],
        [ "The loop", "plan_coins.html#autotoc_md365", null ],
        [ "Rules", "plan_coins.html#autotoc_md366", null ],
        [ "Architecture", "plan_coins.html#autotoc_md367", null ],
        [ "Phases", "plan_coins.html#autotoc_md368", null ],
        [ "Edge cases", "plan_coins.html#autotoc_md369", null ],
        [ "Assets", "plan_coins.html#autotoc_md370", null ],
        [ "Tuning targets", "plan_coins.html#autotoc_md371", null ],
        [ "Open questions", "plan_coins.html#autotoc_md372", null ]
      ] ]
    ] ],
    [ "The Card Reveal", "plan_card_reveal.html", [
      [ "The Card Reveal", "plan_card_reveal.html#autotoc_md352", [
        [ "Why", "plan_card_reveal.html#autotoc_md354", null ],
        [ "What the player sees", "plan_card_reveal.html#autotoc_md355", null ],
        [ "Rules", "plan_card_reveal.html#autotoc_md356", null ],
        [ "Architecture", "plan_card_reveal.html#autotoc_md357", null ],
        [ "How the pops are made", "plan_card_reveal.html#autotoc_md358", null ],
        [ "Assets", "plan_card_reveal.html#autotoc_md359", null ],
        [ "Phases", "plan_card_reveal.html#autotoc_md360", null ],
        [ "Open questions", "plan_card_reveal.html#autotoc_md361", null ]
      ] ]
    ] ]
];