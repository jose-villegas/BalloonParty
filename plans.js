var plans =
[
    [ "Plans", "plans.html#autotoc_md675", null ],
    [ "Bubble Cluster Hit Feedback", "plan_bubble_cluster_hit_feedback.html", [
      [ "Bubble Cluster Hit Feedback", "plan_bubble_cluster_hit_feedback.html#autotoc_md284", [
        [ "Goals", "plan_bubble_cluster_hit_feedback.html#autotoc_md286", null ],
        [ "Root cause of the existing laser rotation bug", "plan_bubble_cluster_hit_feedback.html#autotoc_md288", null ],
        [ "New interface — IBalloonHitHandler", "plan_bubble_cluster_hit_feedback.html#autotoc_md290", null ],
        [ "Discovery and caching", "plan_bubble_cluster_hit_feedback.html#autotoc_md292", [
          [ "Root components — cache at Bind() time", "plan_bubble_cluster_hit_feedback.html#autotoc_md293", null ],
          [ "Item visual components — register via ItemDisplayService", "plan_bubble_cluster_hit_feedback.html#autotoc_md294", null ],
          [ "What goes away", "plan_bubble_cluster_hit_feedback.html#autotoc_md295", null ]
        ] ],
        [ "BalloonView changes", "plan_bubble_cluster_hit_feedback.html#autotoc_md297", [
          [ "New methods", "plan_bubble_cluster_hit_feedback.html#autotoc_md298", null ],
          [ "Removed", "plan_bubble_cluster_hit_feedback.html#autotoc_md299", null ],
          [ "Internal", "plan_bubble_cluster_hit_feedback.html#autotoc_md300", null ]
        ] ],
        [ "BalloonController changes", "plan_bubble_cluster_hit_feedback.html#autotoc_md302", null ],
        [ "SoapBubbleClusterVariant — implements IBalloonHitHandler", "plan_bubble_cluster_hit_feedback.html#autotoc_md304", [
          [ "C# mirror of shader bubble layouts", "plan_bubble_cluster_hit_feedback.html#autotoc_md305", null ],
          [ "GetVfxWorldPosition", "plan_bubble_cluster_hit_feedback.html#autotoc_md306", null ],
          [ "OnHit — spin impulse", "plan_bubble_cluster_hit_feedback.html#autotoc_md307", null ],
          [ "OnPrePop", "plan_bubble_cluster_hit_feedback.html#autotoc_md308", null ]
        ] ],
        [ "LaserItemRotation — migrates to IBalloonHitHandler", "plan_bubble_cluster_hit_feedback.html#autotoc_md310", null ],
        [ "File summary", "plan_bubble_cluster_hit_feedback.html#autotoc_md312", null ],
        [ "Open questions", "plan_bubble_cluster_hit_feedback.html#autotoc_md314", null ]
      ] ]
    ] ],
    [ "Grid Actor Expansion — Phase 8+", "plan_grid_actor_expansion.html", [
      [ "Grid Actor Expansion — Phase 8+", "plan_grid_actor_expansion.html#autotoc_md487", [
        [ "Orientation", "plan_grid_actor_expansion.html#autotoc_md489", null ],
        [ "Actor Vocabulary — Design Reference", "plan_grid_actor_expansion.html#autotoc_md491", [
          [ "Balloon archetypes", "plan_grid_actor_expansion.html#autotoc_md492", null ],
          [ "Grid actor archetypes", "plan_grid_actor_expansion.html#autotoc_md493", null ],
          [ "Hit controller pattern for non-balloon actors", "plan_grid_actor_expansion.html#autotoc_md494", null ]
        ] ],
        [ "Phases", "plan_grid_actor_expansion.html#autotoc_md496", [
          [ "✅ Phase 8.0 — Spawner Coordination", "plan_grid_actor_expansion.html#autotoc_md498", [
            [ "What was built", "plan_grid_actor_expansion.html#autotoc_md499", null ]
          ] ],
          [ "✅ Phase 8.1a — Absorb Routing", "plan_grid_actor_expansion.html#autotoc_md501", [
            [ "What was built", "plan_grid_actor_expansion.html#autotoc_md502", null ]
          ] ],
          [ "✅ Phase 8.1b — DamageContext API Migration", "plan_grid_actor_expansion.html#autotoc_md504", [
            [ "What was built", "plan_grid_actor_expansion.html#autotoc_md505", null ]
          ] ],
          [ "Phase 8.1c — UnbreakableBalloonModel + BalloonModelBase Cleanup", "plan_grid_actor_expansion.html#autotoc_md507", [
            [ "UnbreakableBalloonModel", "plan_grid_actor_expansion.html#autotoc_md508", null ],
            [ "ScoreValue on BalloonModelBase", "plan_grid_actor_expansion.html#autotoc_md509", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md510", null ]
          ] ],
          [ "Phase 8.2a — Structural Actors (Puff + Bush)", "plan_grid_actor_expansion.html#autotoc_md512", [
            [ "Folder structure", "plan_grid_actor_expansion.html#autotoc_md513", null ],
            [ "Files", "plan_grid_actor_expansion.html#autotoc_md514", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md515", null ]
          ] ],
          [ "Phase 8.2b — Indestructible Hitable Actors (Deflector + Absorber)", "plan_grid_actor_expansion.html#autotoc_md517", [
            [ "Files", "plan_grid_actor_expansion.html#autotoc_md518", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md519", null ]
          ] ],
          [ "Phase 8.2c — Gatekeeper + GridActorHitController", "plan_grid_actor_expansion.html#autotoc_md521", [
            [ "Files", "plan_grid_actor_expansion.html#autotoc_md522", null ],
            [ "GatekeeperActorModel", "plan_grid_actor_expansion.html#autotoc_md523", null ],
            [ "GridActorHitController", "plan_grid_actor_expansion.html#autotoc_md524", null ],
            [ "NudgeOverrides cleanup on BalloonModelBase", "plan_grid_actor_expansion.html#autotoc_md525", null ],
            [ "NudgeService decoupling (done alongside 8.2c)", "plan_grid_actor_expansion.html#autotoc_md526", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md527", null ]
          ] ],
          [ "Phase 8.3 — Procedural Placement Engine", "plan_grid_actor_expansion.html#autotoc_md529", [
            [ "Design", "plan_grid_actor_expansion.html#autotoc_md530", null ],
            [ "Migration path", "plan_grid_actor_expansion.html#autotoc_md531", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md532", null ]
          ] ],
          [ "Phase 8.4 — Difficulty + Level Coupling", "plan_grid_actor_expansion.html#autotoc_md534", [
            [ "Design", "plan_grid_actor_expansion.html#autotoc_md535", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md536", null ]
          ] ]
        ] ],
        [ "Open Questions", "plan_grid_actor_expansion.html#autotoc_md538", null ],
        [ "Current State", "plan_grid_actor_expansion.html#autotoc_md540", null ]
      ] ]
    ] ],
    [ "Content Production Plan — Pre-8.3 Assets", "plan_content_production.html", [
      [ "Content Production Plan — Pre-8.3 Assets", "plan_content_production.html#autotoc_md315", [
        [ "Context", "plan_content_production.html#autotoc_md317", null ],
        [ "Asset status overview", "plan_content_production.html#autotoc_md319", null ],
        [ "Shared infrastructure needed", "plan_content_production.html#autotoc_md321", [
          [ "IHasScoreColor — score attribution ✅ Complete", "plan_content_production.html#autotoc_md322", null ],
          [ "GridActorConfiguration ScriptableObject", "plan_content_production.html#autotoc_md324", null ],
          [ "GridActorView prefab root pattern", "plan_content_production.html#autotoc_md325", null ]
        ] ],
        [ "Per-actor detail", "plan_content_production.html#autotoc_md327", [
          [ "Soap Cluster ✅ Done", "plan_content_production.html#autotoc_md329", null ],
          [ "Puff ✅ Done", "plan_content_production.html#autotoc_md331", null ],
          [ "Bush ✅ Done", "plan_content_production.html#autotoc_md333", null ],
          [ "Deflector", "plan_content_production.html#autotoc_md335", null ],
          [ "Absorber", "plan_content_production.html#autotoc_md337", null ],
          [ "Gatekeeper", "plan_content_production.html#autotoc_md339", null ]
        ] ],
        [ "Suggested production order", "plan_content_production.html#autotoc_md341", null ],
        [ "Asset folder conventions", "plan_content_production.html#autotoc_md343", null ],
        [ "Open questions for art direction", "plan_content_production.html#autotoc_md345", null ]
      ] ]
    ] ],
    [ "Future Ideas & Improvements", "plan_future_ideas.html", [
      [ "Future Ideas & Improvements", "plan_future_ideas.html#autotoc_md346", [
        [ "1 — VFX Improvements", "plan_future_ideas.html#autotoc_md348", [
          [ "1.1 Unbreakable Pop VFX — Falling Debris", "plan_future_ideas.html#autotoc_md349", null ],
          [ "1.2 Soap Cluster Pop VFX", "plan_future_ideas.html#autotoc_md350", null ],
          [ "1.3 Deflector Bounce VFX", "plan_future_ideas.html#autotoc_md351", null ],
          [ "1.4 Absorber Consume VFX", "plan_future_ideas.html#autotoc_md352", null ],
          [ "1.5 Gatekeeper Hit + Break VFX", "plan_future_ideas.html#autotoc_md353", null ]
        ] ],
        [ "2 — Spawn Weights, Pity System & Streak Balancing", "plan_future_ideas.html#autotoc_md355", [
          [ "2.1 Per-Level Balloon & Item Weights", "plan_future_ideas.html#autotoc_md356", null ],
          [ "2.2 Pity System for Weight Randoms", "plan_future_ideas.html#autotoc_md357", null ],
          [ "2.3 Color Streak Balancing", "plan_future_ideas.html#autotoc_md358", null ],
          [ "2.4 Full Grid Clear-Out Calculation", "plan_future_ideas.html#autotoc_md359", null ]
        ] ],
        [ "3 — Custom Level Editor", "plan_future_ideas.html#autotoc_md361", [
          [ "3.1 Level Sequence Model", "plan_future_ideas.html#autotoc_md362", null ],
          [ "3.2 LevelDefinition ScriptableObject", "plan_future_ideas.html#autotoc_md363", null ],
          [ "3.3 Custom Level Editor Window (Unity Editor)", "plan_future_ideas.html#autotoc_md364", null ],
          [ "3.4 Level Sequence Integration", "plan_future_ideas.html#autotoc_md365", null ]
        ] ],
        [ "4 — Tutorial System & Pacing", "plan_future_ideas.html#autotoc_md367", [
          [ "4.1 Tutorial Trigger System", "plan_future_ideas.html#autotoc_md368", null ],
          [ "4.2 Tutorial Sequence Definition", "plan_future_ideas.html#autotoc_md369", null ],
          [ "4.3 Pacing — Suggested Actor Introduction Order", "plan_future_ideas.html#autotoc_md370", null ],
          [ "4.4 Tutorial UI", "plan_future_ideas.html#autotoc_md371", null ],
          [ "4.5 Persistence", "plan_future_ideas.html#autotoc_md372", null ]
        ] ],
        [ "5 — Relocated Future Ideas", "plan_future_ideas.html#autotoc_md374", [
          [ "5.1 Soap Cluster Merge (from PLAN-ContentProduction + PLAN-GridActorExpansion)", "plan_future_ideas.html#autotoc_md375", null ],
          [ "5.2 Unbreakable Roam (from PLAN-ContentProduction) — ✅ SHIPPED 2026-07-12", "plan_future_ideas.html#autotoc_md376", null ],
          [ "5.3 New Balloon Archetypes (from PLAN-GridActorExpansion)", "plan_future_ideas.html#autotoc_md377", null ],
          [ "5.4 New Grid Actor Archetypes (from PLAN-GridActorExpansion)", "plan_future_ideas.html#autotoc_md378", null ],
          [ "5.5 IPassThrough Behaviour Extensions (from PLAN-GridActorExpansion)", "plan_future_ideas.html#autotoc_md379", null ],
          [ "5.6 Spider Web Obstacle (from PLAN-SpiderWeb)", "plan_future_ideas.html#autotoc_md380", null ]
        ] ],
        [ "6 — Puff Cloud Polish & Performance", "plan_future_ideas.html#autotoc_md382", [
          [ "6.1 Visual Polish", "plan_future_ideas.html#autotoc_md383", null ],
          [ "6.2 Performance & Device Scaling", "plan_future_ideas.html#autotoc_md384", null ],
          [ "6.3 Edge Cases", "plan_future_ideas.html#autotoc_md385", null ]
        ] ],
        [ "7 — Vertical Cloud Drift", "plan_future_ideas.html#autotoc_md387", null ],
        [ "8 — Timed Release: Balloon Pass-Through Delay", "plan_future_ideas.html#autotoc_md389", [
          [ "8.1 Current Behaviour", "plan_future_ideas.html#autotoc_md390", null ],
          [ "8.2 Proposed Flow", "plan_future_ideas.html#autotoc_md391", null ],
          [ "8.3 Model Sketch", "plan_future_ideas.html#autotoc_md392", null ],
          [ "8.4 Balancer Changes", "plan_future_ideas.html#autotoc_md393", null ],
          [ "8.5 Visual Feedback", "plan_future_ideas.html#autotoc_md394", null ],
          [ "8.6 Relationship to Existing Pass-Through Ideas", "plan_future_ideas.html#autotoc_md395", null ],
          [ "8.7 Open Questions", "plan_future_ideas.html#autotoc_md396", null ]
        ] ],
        [ "Open Questions", "plan_future_ideas.html#autotoc_md398", null ],
        [ "9 — Quality Settings System", "plan_future_ideas.html#autotoc_md400", [
          [ "9.1 Architecture", "plan_future_ideas.html#autotoc_md401", null ],
          [ "9.2 Parameter Inventory", "plan_future_ideas.html#autotoc_md402", [
            [ "Shader Keywords (GPU cost)", "plan_future_ideas.html#autotoc_md403", null ],
            [ "Disturbance Field (GPU + memory)", "plan_future_ideas.html#autotoc_md404", null ],
            [ "Unbreakable Balloon GrabPass (GPU — single highest-cost operation)", "plan_future_ideas.html#autotoc_md405", null ],
            [ "Particle VFX (GPU fill rate + CPU simulation)", "plan_future_ideas.html#autotoc_md406", null ],
            [ "Projectile Trail (GPU fill rate)", "plan_future_ideas.html#autotoc_md407", null ],
            [ "Procedural Shader Octave Count", "plan_future_ideas.html#autotoc_md408", null ],
            [ "Animation Quality", "plan_future_ideas.html#autotoc_md409", null ],
            [ "Pool Sizes", "plan_future_ideas.html#autotoc_md410", null ]
          ] ],
          [ "9.3 Suggested Tier Thresholds", "plan_future_ideas.html#autotoc_md411", null ],
          [ "9.4 Implementation Priorities", "plan_future_ideas.html#autotoc_md412", null ],
          [ "9.5 Open Questions", "plan_future_ideas.html#autotoc_md413", null ]
        ] ],
        [ "10 — Baked Noise Texture for Puff Clouds", "plan_future_ideas.html#autotoc_md415", null ],
        [ "11 — Runtime Bush Baking at Preload", "plan_future_ideas.html#autotoc_md417", null ],
        [ "12 — Losing Conditions", "plan_future_ideas.html#autotoc_md419", [
          [ "12.1 Grid encroachment — the natural fit (Puzzle Bobble lineage)", "plan_future_ideas.html#autotoc_md420", null ],
          [ "12.2 Resource economy — make the existing Shield item matter", "plan_future_ideas.html#autotoc_md421", null ],
          [ "12.3 Clock / turn pressure — score-attack flavor", "plan_future_ideas.html#autotoc_md422", null ],
          [ "12.4 Lockout / soft-lock (safety net, not a headline mechanic)", "plan_future_ideas.html#autotoc_md423", null ],
          [ "12.5 How these compose", "plan_future_ideas.html#autotoc_md424", null ]
        ] ],
        [ "13 — Roguelike Run Modifiers (Unlockables)", "plan_future_ideas.html#autotoc_md426", [
          [ "13.1 Concept", "plan_future_ideas.html#autotoc_md427", null ],
          [ "13.2 When unlocks are offered", "plan_future_ideas.html#autotoc_md428", null ],
          [ "13.3 Model sketch", "plan_future_ideas.html#autotoc_md429", null ],
          [ "13.4 Passive modifier ideas", "plan_future_ideas.html#autotoc_md430", null ],
          [ "13.5 Active \"spawn now\" + grid-effect ideas", "plan_future_ideas.html#autotoc_md431", null ],
          [ "13.6 Integration points (existing systems)", "plan_future_ideas.html#autotoc_md432", null ],
          [ "13.7 Card presentation (the open visual question)", "plan_future_ideas.html#autotoc_md433", null ],
          [ "13.8 Open questions", "plan_future_ideas.html#autotoc_md434", null ]
        ] ],
        [ "14 — Level Pacing Follow-ups (post-ship)", "plan_future_ideas.html#autotoc_md436", [
          [ "14.1 Jump-to-level cheat", "plan_future_ideas.html#autotoc_md437", null ],
          [ "14.2 Extract a shared LevelPacingValidator", "plan_future_ideas.html#autotoc_md438", null ],
          [ "14.3 Escalate LevelDifficultyResolver.FallbackParameters to a hard failure", "plan_future_ideas.html#autotoc_md439", null ]
        ] ],
        [ "15 — Level-Up Point Carry-Over (2026-07-18)", "plan_future_ideas.html#autotoc_md440", null ],
        [ "16 — Grand Antiprism: the 100 denomination (2026-07-19)", "plan_future_ideas.html#autotoc_md441", null ],
        [ "Bush Visual Polish", "plan_future_ideas.html#autotoc_md443", null ]
      ] ]
    ] ],
    [ "HDR Color Pipeline", "plan_hdr_color_pipeline.html", [
      [ "HDR Color Pipeline — migration plan", "plan_hdr_color_pipeline.html#autotoc_md541", [
        [ "Decision & scope", "plan_hdr_color_pipeline.html#autotoc_md543", null ],
        [ "Verified inventory (2026-07-11)", "plan_hdr_color_pipeline.html#autotoc_md544", null ],
        [ "Task plan", "plan_hdr_color_pipeline.html#autotoc_md545", [
          [ "Dependency graph", "plan_hdr_color_pipeline.html#autotoc_md546", null ],
          [ "Wave A — Linear color space (the foundation, and the cost gate)", "plan_hdr_color_pipeline.html#autotoc_md547", [
            [ "A0 — Baseline captures · P0 · S", "plan_hdr_color_pipeline.html#autotoc_md548", null ],
            [ "A1 — Flip to Linear · P0 · S (mechanical) — do NOT merge without A2", "plan_hdr_color_pipeline.html#autotoc_md549", null ],
            [ "A2 — Look re-tune · P0 · L — art-driven, art-led", "plan_hdr_color_pipeline.html#autotoc_md550", null ],
            [ "A-gate — parity/quality sign-off + perf sanity", "plan_hdr_color_pipeline.html#autotoc_md551", null ]
          ] ],
          [ "Wave B — HDR rendering + post + bloom", "plan_hdr_color_pipeline.html#autotoc_md552", [
            [ "B1 — HDR target + post scaffold · P0 · S", "plan_hdr_color_pipeline.html#autotoc_md553", null ],
            [ "B2 — Tonemapping · P0 · S", "plan_hdr_color_pipeline.html#autotoc_md554", null ],
            [ "B3 — Emissive authoring path · P1 · M", "plan_hdr_color_pipeline.html#autotoc_md555", null ],
            [ "B4 — Bloom · P1 · S–M", "plan_hdr_color_pipeline.html#autotoc_md556", null ],
            [ "B5 — Tooling: Render Maps HDR view · P2 · S (parallel after B1)", "plan_hdr_color_pipeline.html#autotoc_md557", null ],
            [ "B6 — GI/capture chain under HDR · P1 · M", "plan_hdr_color_pipeline.html#autotoc_md558", null ],
            [ "B-gate — device sign-off", "plan_hdr_color_pipeline.html#autotoc_md559", null ]
          ] ],
          [ "Wave C — HDR display output · deferred, own trigger", "plan_hdr_color_pipeline.html#autotoc_md560", null ]
        ] ],
        [ "Open questions (answer at execution time)", "plan_hdr_color_pipeline.html#autotoc_md561", null ]
      ] ]
    ] ],
    [ "Terrain & Biomes", "plan_terrain_biomes.html", [
      [ "Terrain & Biomes — scenario ground generator", "plan_terrain_biomes.html#autotoc_md635", [
        [ "Vision & constraints", "plan_terrain_biomes.html#autotoc_md637", null ],
        [ "Verified inventory (2026-07-12)", "plan_terrain_biomes.html#autotoc_md638", null ],
        [ "Architecture", "plan_terrain_biomes.html#autotoc_md639", [
          [ "Data — TerrainMapService (plain C#, VContainer)", "plan_terrain_biomes.html#autotoc_md640", null ],
          [ "Bake — one blit per level (GPU, amortizable)", "plan_terrain_biomes.html#autotoc_md641", null ],
          [ "View — TerrainView (MonoBehaviour) + runtime shader", "plan_terrain_biomes.html#autotoc_md642", null ]
        ] ],
        [ "Shader design — options per stage", "plan_terrain_biomes.html#autotoc_md643", [
          [ "S1 — Zone blending (bake-time)", "plan_terrain_biomes.html#autotoc_md644", null ],
          [ "S2 — Grass (the flagship reaction)", "plan_terrain_biomes.html#autotoc_md645", null ],
          [ "S3 — Water", "plan_terrain_biomes.html#autotoc_md646", null ],
          [ "S4 — Sand / dirt / stone / lava", "plan_terrain_biomes.html#autotoc_md647", null ],
          [ "S5 — Data layout (bake outputs)", "plan_terrain_biomes.html#autotoc_md648", null ],
          [ "S6 — Cheap density & anti-repetition (mobile)", "plan_terrain_biomes.html#autotoc_md649", null ],
          [ "S7 — Runtime composition rules", "plan_terrain_biomes.html#autotoc_md650", null ]
        ] ],
        [ "Task plan", "plan_terrain_biomes.html#autotoc_md651", [
          [ "Dependency graph", "plan_terrain_biomes.html#autotoc_md652", null ],
          [ "Wave A — data + bake", "plan_terrain_biomes.html#autotoc_md653", [
            [ "A1 — TerrainMapService + BiomeProfile config + ITerrainQuery · P0 · M · opus", "plan_terrain_biomes.html#autotoc_md654", null ],
            [ "A2 — Index-map build/upload · P0 · S · sonnet", "plan_terrain_biomes.html#autotoc_md655", null ],
            [ "A3 — Bake blit shader + TerrainBaker · P0 · M · opus", "plan_terrain_biomes.html#autotoc_md656", null ]
          ] ],
          [ "Wave B — view + reactions + GI", "plan_terrain_biomes.html#autotoc_md657", [
            [ "B1 — TerrainView + runtime shader v1 · P0 · M · sonnet", "plan_terrain_biomes.html#autotoc_md658", null ],
            [ "B2 — Disturbance reactions · P1 · M–L · opus", "plan_terrain_biomes.html#autotoc_md659", null ],
            [ "B3 — GI integration · P1 · S · sonnet", "plan_terrain_biomes.html#autotoc_md660", null ]
          ] ],
          [ "Wave C — gameplay + tooling (parallel after A1)", "plan_terrain_biomes.html#autotoc_md661", [
            [ "C1 — Placement gating seam · P1 · S–M · sonnet", "plan_terrain_biomes.html#autotoc_md662", null ],
            [ "C2 — Seed plumbing + reproduction affordance · P2 · S · haiku", "plan_terrain_biomes.html#autotoc_md663", null ],
            [ "C3 — Game Render Maps entries · P2 · S · sonnet", "plan_terrain_biomes.html#autotoc_md664", null ]
          ] ]
        ] ],
        [ "Open questions (answer at execution time)", "plan_terrain_biomes.html#autotoc_md665", null ]
      ] ]
    ] ],
    [ "Gameplay Telemetry", "plan_gameplay_telemetry.html", [
      [ "Gameplay Telemetry", "plan_gameplay_telemetry.html#autotoc_md444", [
        [ "Principles", "plan_gameplay_telemetry.html#autotoc_md446", null ],
        [ "Architecture", "plan_gameplay_telemetry.html#autotoc_md448", [
          [ "Folder structure", "plan_gameplay_telemetry.html#autotoc_md449", null ],
          [ "Class diagram", "plan_gameplay_telemetry.html#autotoc_md450", null ],
          [ "Flush state machine", "plan_gameplay_telemetry.html#autotoc_md451", null ],
          [ "Sequence diagram — level flush", "plan_gameplay_telemetry.html#autotoc_md452", null ],
          [ "Service", "plan_gameplay_telemetry.html#autotoc_md453", null ],
          [ "Internal helpers", "plan_gameplay_telemetry.html#autotoc_md454", null ],
          [ "Sink", "plan_gameplay_telemetry.html#autotoc_md455", null ],
          [ "Serialization", "plan_gameplay_telemetry.html#autotoc_md456", null ]
        ] ],
        [ "Phase 1 — Passive Counters", "plan_gameplay_telemetry.html#autotoc_md458", [
          [ "Flush boundaries", "plan_gameplay_telemetry.html#autotoc_md459", null ],
          [ "LevelRecord fields", "plan_gameplay_telemetry.html#autotoc_md460", [
            [ "Typed breakdown entries", "plan_gameplay_telemetry.html#autotoc_md461", null ]
          ] ],
          [ "RunRecord fields", "plan_gameplay_telemetry.html#autotoc_md462", null ],
          [ "Message subscriptions", "plan_gameplay_telemetry.html#autotoc_md463", null ],
          [ "Color derivation", "plan_gameplay_telemetry.html#autotoc_md464", null ],
          [ "Item derivation", "plan_gameplay_telemetry.html#autotoc_md465", null ],
          [ "Time tracking", "plan_gameplay_telemetry.html#autotoc_md466", null ],
          [ "Pause semantics", "plan_gameplay_telemetry.html#autotoc_md467", null ],
          [ "Cheat tagging", "plan_gameplay_telemetry.html#autotoc_md468", null ],
          [ "Performance notes", "plan_gameplay_telemetry.html#autotoc_md469", null ]
        ] ],
        [ "Phase 2 — Small Extensions", "plan_gameplay_telemetry.html#autotoc_md471", null ],
        [ "Phase 3 — Deferred", "plan_gameplay_telemetry.html#autotoc_md473", null ],
        [ "Test Strategy", "plan_gameplay_telemetry.html#autotoc_md475", [
          [ "File structure", "plan_gameplay_telemetry.html#autotoc_md476", null ],
          [ "Named test cases", "plan_gameplay_telemetry.html#autotoc_md477", null ]
        ] ],
        [ "Implementer guardrails", "plan_gameplay_telemetry.html#autotoc_md479", null ],
        [ "Analytics Notes", "plan_gameplay_telemetry.html#autotoc_md481", [
          [ "Minimum viable fields", "plan_gameplay_telemetry.html#autotoc_md482", null ],
          [ "Key analyses enabled", "plan_gameplay_telemetry.html#autotoc_md483", null ],
          [ "Sample-size guidance", "plan_gameplay_telemetry.html#autotoc_md484", null ]
        ] ],
        [ "Resolved questions (from the original draft)", "plan_gameplay_telemetry.html#autotoc_md486", null ]
      ] ]
    ] ],
    [ "Performance Recovery", "plan_performance_recovery.html", [
      [ "Performance Recovery Plan — BalloonParty (Phase 2, revised 2026-07-23)", "plan_performance_recovery.html#autotoc_md562", [
        [ "Execution model & delegation matrix", "plan_performance_recovery.html#autotoc_md564", null ],
        [ "Phase 1 — Completed ✅ (spot-checked 2026-07-23: all commits exist and match)", "plan_performance_recovery.html#autotoc_md566", null ],
        [ "Step 0: Diagnose before optimizing (mandatory, do first)", "plan_performance_recovery.html#autotoc_md568", [
          [ "Preconditions — record backend and build type", "plan_performance_recovery.html#autotoc_md569", null ],
          [ "Pacing / ARR check", "plan_performance_recovery.html#autotoc_md570", null ],
          [ "CPU vs GPU classification", "plan_performance_recovery.html#autotoc_md571", null ],
          [ "Overdraw look (editor)", "plan_performance_recovery.html#autotoc_md572", null ],
          [ "AGI — Mali fragment vs bandwidth", "plan_performance_recovery.html#autotoc_md573", null ],
          [ "Thermal baseline", "plan_performance_recovery.html#autotoc_md574", null ],
          [ "Findings — first pass, 2026-07-23 (cool device, release build, live gameplay)", "plan_performance_recovery.html#autotoc_md575", null ],
          [ "Findings — second pass, 2026-07-23 evening (90s perfetto capture during live play)", "plan_performance_recovery.html#autotoc_md576", null ],
          [ "Findings — third pass, 2026-07-23 night (20-min longitudinal monitor, 15s cadence)", "plan_performance_recovery.html#autotoc_md577", null ],
          [ "Findings — fourth pass, 2026-07-23 night (simpleperf callstacks, dev build)", "plan_performance_recovery.html#autotoc_md578", null ],
          [ "Decision tree", "plan_performance_recovery.html#autotoc_md579", null ]
        ] ],
        [ "Tier 0: Free / Near-Free Wins", "plan_performance_recovery.html#autotoc_md581", [
          [ "F2 — Remove the shrink Array.Resize in TransformRibbon · sonnet", "plan_performance_recovery.html#autotoc_md582", null ],
          [ "F3 — SetCharArray in RollingTextAnimator · haiku", "plan_performance_recovery.html#autotoc_md583", null ],
          [ "F4 — Shader variant warmup · sonnet (capture = José)", "plan_performance_recovery.html#autotoc_md584", null ],
          [ "C1 + C3 — Projectile cleanups · sonnet, one session (same file)", "plan_performance_recovery.html#autotoc_md585", null ]
        ] ],
        [ "Tier 1: UI arrival storm — RESOLVED 2026-07-23: U1 reverted, U2 kept", "plan_performance_recovery.html#autotoc_md587", [
          [ "U1 — De-Animator the progress-bar hit pulse · opus", "plan_performance_recovery.html#autotoc_md588", null ],
          [ "U2 — Eliminate ProgressNotice reparenting · sonnet (+ mandatory fable review of pool consumers)", "plan_performance_recovery.html#autotoc_md589", null ]
        ] ],
        [ "Tier 2: GPU / Shader (re-prioritized for Mali-G715)", "plan_performance_recovery.html#autotoc_md591", [
          [ "G6 — fp16 in ScreenSpaceLightSmear.shader · opus, same session as G3", "plan_performance_recovery.html#autotoc_md592", null ],
          [ "G3 — 4-tap bilinear blur (Pass 1) · with G6", "plan_performance_recovery.html#autotoc_md593", null ],
          [ "G4 — BackgroundField bake fine-octave skip · SHIPPED THEN REVERTED 2026-07-23", "plan_performance_recovery.html#autotoc_md594", null ],
          [ "G1 — Blit budget controller · opus, after Step 0 confirms spikes matter", "plan_performance_recovery.html#autotoc_md595", null ],
          [ "G5 — Gradient-skip · DEFERRED — riskier than it looks (opus if ever done)", "plan_performance_recovery.html#autotoc_md596", null ]
        ] ],
        [ "Tier 3: Architectural (gated on Step 0 evidence)", "plan_performance_recovery.html#autotoc_md598", [
          [ "A4 — Quality tiers + ADPF · opus — required for ship, promoted earlier", "plan_performance_recovery.html#autotoc_md599", null ],
          [ "A1 — Render Graph prototype · opus — go/no-go, only on Step-0 GPU evidence", "plan_performance_recovery.html#autotoc_md600", null ],
          [ "A2 — SceneCapture → Renderer Feature · parked behind A1", "plan_performance_recovery.html#autotoc_md601", null ]
        ] ],
        [ "Quality vetoes — 2026-07-23 device pass (José)", "plan_performance_recovery.html#autotoc_md603", null ],
        [ "Dropped in the 2026-07-23 revision", "plan_performance_recovery.html#autotoc_md604", null ],
        [ "Investigated and Skipped (carried over)", "plan_performance_recovery.html#autotoc_md605", null ],
        [ "Dependency map & execution waves", "plan_performance_recovery.html#autotoc_md607", null ],
        [ "Verification Protocol", "plan_performance_recovery.html#autotoc_md609", null ]
      ] ]
    ] ],
    [ "Audio / SFX", "plan_audio.html", [
      [ "Audio / SFX", "plan_audio.html#autotoc_md249", [
        [ "Principles", "plan_audio.html#autotoc_md251", null ],
        [ "Architecture", "plan_audio.html#autotoc_md253", [
          [ "Folder structure", "plan_audio.html#autotoc_md254", null ],
          [ "Class diagram", "plan_audio.html#autotoc_md255", null ],
          [ "Sequence diagram — event to sound", "plan_audio.html#autotoc_md256", null ],
          [ "Dependency graph — VContainer wiring", "plan_audio.html#autotoc_md257", null ],
          [ "Voice lifecycle", "plan_audio.html#autotoc_md258", null ],
          [ "Responsibility map", "plan_audio.html#autotoc_md259", null ]
        ] ],
        [ "Channels (the pause / context answer)", "plan_audio.html#autotoc_md261", null ],
        [ "Spatialization", "plan_audio.html#autotoc_md263", null ],
        [ "SFX moment inventory", "plan_audio.html#autotoc_md265", null ],
        [ "Melodic pops (streak-driven scale)", "plan_audio.html#autotoc_md267", null ],
        [ "Phase 1 — Core loop, no message changes — SHIPPED (Steps 1-6)", "plan_audio.html#autotoc_md269", [
          [ "First-pass sounds to author", "plan_audio.html#autotoc_md270", null ],
          [ "Voice management", "plan_audio.html#autotoc_md271", null ],
          [ "Return scheduling", "plan_audio.html#autotoc_md272", null ],
          [ "Teardown", "plan_audio.html#autotoc_md273", null ],
          [ "Clip import settings (device)", "plan_audio.html#autotoc_md274", null ],
          [ "GC / hot-path", "plan_audio.html#autotoc_md275", null ]
        ] ],
        [ "Phase 2 — Fill-out + optional signals", "plan_audio.html#autotoc_md277", null ],
        [ "Phase 3 — Deferred", "plan_audio.html#autotoc_md279", null ],
        [ "Test strategy", "plan_audio.html#autotoc_md281", null ],
        [ "Open questions", "plan_audio.html#autotoc_md283", null ]
      ] ]
    ] ],
    [ "Shot Solver Accuracy", "plan_shot_solver_accuracy.html", [
      [ "Shot Solver Accuracy", "plan_shot_solver_accuracy.html#autotoc_md610", [
        [ "Diagnostic", "plan_shot_solver_accuracy.html#autotoc_md612", null ],
        [ "Goals & non-goals", "plan_shot_solver_accuracy.html#autotoc_md613", null ],
        [ "Architecture decisions (settled)", "plan_shot_solver_accuracy.html#autotoc_md614", null ],
        [ "Phases", "plan_shot_solver_accuracy.html#autotoc_md615", [
          [ "Phase 0 — Prerequisite refactors (no behavior change; the existing 22-test suite stays green)", "plan_shot_solver_accuracy.html#autotoc_md616", null ],
          [ "Phase A — Interactive static geometry (G1) — depends on 0b", "plan_shot_solver_accuracy.html#autotoc_md617", null ],
          [ "Phase B — Weight-system fidelity (G2) — depends on 0b; parallel to A", "plan_shot_solver_accuracy.html#autotoc_md618", null ],
          [ "Phase D-core — Rainbow scoring + in-sim buff state (G4-scoring, G8) — depends on 0a", "plan_shot_solver_accuracy.html#autotoc_md619", null ],
          [ "✅ Phase C — Item carriers (G3) — depends on B + D-core", "plan_shot_solver_accuracy.html#autotoc_md620", null ],
          [ "Phase E — Flight residuals (G5, G6, G7 + E4) — depends on 0a; E2 folds C6", "plan_shot_solver_accuracy.html#autotoc_md621", null ],
          [ "Phase F — Nondeterminism policy + instrumentation (G9) — last", "plan_shot_solver_accuracy.html#autotoc_md622", null ],
          [ "Phase G — Headless level diagnostics (follow-up tier; scoped 2026-07-25)", "plan_shot_solver_accuracy.html#autotoc_md623", null ]
        ] ],
        [ "Test plan (per test-everything; full detail in the review transcript)", "plan_shot_solver_accuracy.html#autotoc_md624", null ],
        [ "Verification workflow", "plan_shot_solver_accuracy.html#autotoc_md625", null ],
        [ "Open decisions", "plan_shot_solver_accuracy.html#autotoc_md626", null ],
        [ "Remaining work — detailed status (2026-07-26)", "plan_shot_solver_accuracy.html#autotoc_md627", [
          [ "E — flight residuals (next; ONE architect memo for all four, they interact)", "plan_shot_solver_accuracy.html#autotoc_md628", null ],
          [ "F — instrumentation + acceptance (after E)", "plan_shot_solver_accuracy.html#autotoc_md629", null ],
          [ "Live repoint track (any time; separable commits)", "plan_shot_solver_accuracy.html#autotoc_md630", null ],
          [ "José's gates (accumulated)", "plan_shot_solver_accuracy.html#autotoc_md631", null ],
          [ "Deferred code follow-ups (small, none blocking)", "plan_shot_solver_accuracy.html#autotoc_md632", null ],
          [ "G — headless level diagnostics (follow-up tier; unchanged spec in §4 Phase G)", "plan_shot_solver_accuracy.html#autotoc_md633", null ],
          [ "Design questions parked for José", "plan_shot_solver_accuracy.html#autotoc_md634", null ]
        ] ]
      ] ]
    ] ],
    [ "Web Demo Hosting", "plan_web_demo_hosting.html", [
      [ "Web Demo Hosting", "plan_web_demo_hosting.html#autotoc_md666", [
        [ "Why keep it", "plan_web_demo_hosting.html#autotoc_md668", null ],
        [ "What survived", "plan_web_demo_hosting.html#autotoc_md669", null ],
        [ "The recipe, if resumed", "plan_web_demo_hosting.html#autotoc_md670", null ],
        [ "The blocker", "plan_web_demo_hosting.html#autotoc_md671", null ],
        [ "Residue from the attempt", "plan_web_demo_hosting.html#autotoc_md672", null ],
        [ "If web stays dead", "plan_web_demo_hosting.html#autotoc_md673", null ],
        [ "Decision log", "plan_web_demo_hosting.html#autotoc_md674", null ]
      ] ]
    ] ]
];