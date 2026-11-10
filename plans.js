var plans =
[
    [ "Plans", "plans.html#autotoc_md792", null ],
    [ "Bubble Cluster Hit Feedback", "plan_bubble_cluster_hit_feedback.html", [
      [ "Bubble Cluster Hit Feedback", "plan_bubble_cluster_hit_feedback.html#autotoc_md325", [
        [ "Goals", "plan_bubble_cluster_hit_feedback.html#autotoc_md327", null ],
        [ "Root cause of the existing laser rotation bug", "plan_bubble_cluster_hit_feedback.html#autotoc_md329", null ],
        [ "New interface — IBalloonHitHandler", "plan_bubble_cluster_hit_feedback.html#autotoc_md331", null ],
        [ "Discovery and caching", "plan_bubble_cluster_hit_feedback.html#autotoc_md333", [
          [ "Root components — cache at Bind() time", "plan_bubble_cluster_hit_feedback.html#autotoc_md334", null ],
          [ "Item visual components — register via ItemDisplayService", "plan_bubble_cluster_hit_feedback.html#autotoc_md335", null ],
          [ "What goes away", "plan_bubble_cluster_hit_feedback.html#autotoc_md336", null ]
        ] ],
        [ "BalloonView changes", "plan_bubble_cluster_hit_feedback.html#autotoc_md338", [
          [ "New methods", "plan_bubble_cluster_hit_feedback.html#autotoc_md339", null ],
          [ "Removed", "plan_bubble_cluster_hit_feedback.html#autotoc_md340", null ],
          [ "Internal", "plan_bubble_cluster_hit_feedback.html#autotoc_md341", null ]
        ] ],
        [ "BalloonController changes", "plan_bubble_cluster_hit_feedback.html#autotoc_md343", null ],
        [ "SoapBubbleClusterVariant — implements IBalloonHitHandler", "plan_bubble_cluster_hit_feedback.html#autotoc_md345", [
          [ "C# mirror of shader bubble layouts", "plan_bubble_cluster_hit_feedback.html#autotoc_md346", null ],
          [ "GetVfxWorldPosition", "plan_bubble_cluster_hit_feedback.html#autotoc_md347", null ],
          [ "OnHit — spin impulse", "plan_bubble_cluster_hit_feedback.html#autotoc_md348", null ],
          [ "OnPrePop", "plan_bubble_cluster_hit_feedback.html#autotoc_md349", null ]
        ] ],
        [ "LaserItemRotation — migrates to IBalloonHitHandler", "plan_bubble_cluster_hit_feedback.html#autotoc_md351", null ],
        [ "File summary", "plan_bubble_cluster_hit_feedback.html#autotoc_md353", null ],
        [ "Open questions", "plan_bubble_cluster_hit_feedback.html#autotoc_md355", null ]
      ] ]
    ] ],
    [ "Grid Actor Expansion — Phase 8+", "plan_grid_actor_expansion.html", [
      [ "Grid Actor Expansion — Phase 8+", "plan_grid_actor_expansion.html#autotoc_md559", [
        [ "Orientation", "plan_grid_actor_expansion.html#autotoc_md561", null ],
        [ "Actor Vocabulary — Design Reference", "plan_grid_actor_expansion.html#autotoc_md563", [
          [ "Balloon archetypes", "plan_grid_actor_expansion.html#autotoc_md564", null ],
          [ "Grid actor archetypes", "plan_grid_actor_expansion.html#autotoc_md565", null ],
          [ "Hit controller pattern for non-balloon actors", "plan_grid_actor_expansion.html#autotoc_md566", null ]
        ] ],
        [ "Phases", "plan_grid_actor_expansion.html#autotoc_md568", [
          [ "✅ Phase 8.0 — Spawner Coordination", "plan_grid_actor_expansion.html#autotoc_md570", [
            [ "What was built", "plan_grid_actor_expansion.html#autotoc_md571", null ]
          ] ],
          [ "✅ Phase 8.1a — Absorb Routing", "plan_grid_actor_expansion.html#autotoc_md573", [
            [ "What was built", "plan_grid_actor_expansion.html#autotoc_md574", null ]
          ] ],
          [ "✅ Phase 8.1b — DamageContext API Migration", "plan_grid_actor_expansion.html#autotoc_md576", [
            [ "What was built", "plan_grid_actor_expansion.html#autotoc_md577", null ]
          ] ],
          [ "Phase 8.1c — UnbreakableBalloonModel + BalloonModelBase Cleanup", "plan_grid_actor_expansion.html#autotoc_md579", [
            [ "UnbreakableBalloonModel", "plan_grid_actor_expansion.html#autotoc_md580", null ],
            [ "ScoreValue on BalloonModelBase", "plan_grid_actor_expansion.html#autotoc_md581", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md582", null ]
          ] ],
          [ "Phase 8.2a — Structural Actors (Puff + Bush)", "plan_grid_actor_expansion.html#autotoc_md584", [
            [ "Folder structure", "plan_grid_actor_expansion.html#autotoc_md585", null ],
            [ "Files", "plan_grid_actor_expansion.html#autotoc_md586", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md587", null ]
          ] ],
          [ "Phase 8.2b — Indestructible Hitable Actors (Deflector + Absorber)", "plan_grid_actor_expansion.html#autotoc_md589", [
            [ "Files", "plan_grid_actor_expansion.html#autotoc_md590", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md591", null ]
          ] ],
          [ "Phase 8.2c — Gatekeeper + GridActorHitController", "plan_grid_actor_expansion.html#autotoc_md593", [
            [ "Files", "plan_grid_actor_expansion.html#autotoc_md594", null ],
            [ "GatekeeperActorModel", "plan_grid_actor_expansion.html#autotoc_md595", null ],
            [ "GridActorHitController", "plan_grid_actor_expansion.html#autotoc_md596", null ],
            [ "NudgeOverrides cleanup on BalloonModelBase", "plan_grid_actor_expansion.html#autotoc_md597", null ],
            [ "NudgeService decoupling (done alongside 8.2c)", "plan_grid_actor_expansion.html#autotoc_md598", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md599", null ]
          ] ],
          [ "Phase 8.3 — Procedural Placement Engine", "plan_grid_actor_expansion.html#autotoc_md601", [
            [ "Design", "plan_grid_actor_expansion.html#autotoc_md602", null ],
            [ "Migration path", "plan_grid_actor_expansion.html#autotoc_md603", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md604", null ]
          ] ],
          [ "Phase 8.4 — Difficulty + Level Coupling", "plan_grid_actor_expansion.html#autotoc_md606", [
            [ "Design", "plan_grid_actor_expansion.html#autotoc_md607", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md608", null ]
          ] ]
        ] ],
        [ "Open Questions", "plan_grid_actor_expansion.html#autotoc_md610", null ],
        [ "Current State", "plan_grid_actor_expansion.html#autotoc_md612", null ]
      ] ]
    ] ],
    [ "Content Production Plan — Pre-8.3 Assets", "plan_content_production.html", [
      [ "Content Production Plan — Pre-8.3 Assets", "plan_content_production.html#autotoc_md378", [
        [ "Context", "plan_content_production.html#autotoc_md380", null ],
        [ "Asset status overview", "plan_content_production.html#autotoc_md382", null ],
        [ "Shared infrastructure needed", "plan_content_production.html#autotoc_md384", [
          [ "IHasScoreColor — score attribution ✅ Complete", "plan_content_production.html#autotoc_md385", null ],
          [ "GridActorConfiguration ScriptableObject", "plan_content_production.html#autotoc_md387", null ],
          [ "GridActorView prefab root pattern", "plan_content_production.html#autotoc_md388", null ]
        ] ],
        [ "Per-actor detail", "plan_content_production.html#autotoc_md390", [
          [ "Soap Cluster ✅ Done", "plan_content_production.html#autotoc_md392", null ],
          [ "Puff ✅ Done", "plan_content_production.html#autotoc_md394", null ],
          [ "Bush ✅ Done", "plan_content_production.html#autotoc_md396", null ],
          [ "Deflector", "plan_content_production.html#autotoc_md398", null ],
          [ "Absorber", "plan_content_production.html#autotoc_md400", null ],
          [ "Gatekeeper", "plan_content_production.html#autotoc_md402", null ]
        ] ],
        [ "Suggested production order", "plan_content_production.html#autotoc_md404", null ],
        [ "Asset folder conventions", "plan_content_production.html#autotoc_md406", null ],
        [ "Open questions for art direction", "plan_content_production.html#autotoc_md408", null ]
      ] ]
    ] ],
    [ "Future Ideas & Improvements", "plan_future_ideas.html", [
      [ "Future Ideas & Improvements", "plan_future_ideas.html#autotoc_md427", [
        [ "1 — VFX Improvements", "plan_future_ideas.html#autotoc_md429", [
          [ "1.1 Unbreakable Pop VFX — Falling Debris", "plan_future_ideas.html#autotoc_md430", null ],
          [ "1.2 Soap Cluster Pop VFX", "plan_future_ideas.html#autotoc_md431", null ],
          [ "1.3 Deflector Bounce VFX", "plan_future_ideas.html#autotoc_md432", null ],
          [ "1.4 Absorber Consume VFX", "plan_future_ideas.html#autotoc_md433", null ],
          [ "1.5 Gatekeeper Hit + Break VFX", "plan_future_ideas.html#autotoc_md434", null ]
        ] ],
        [ "2 — Spawn Weights, Pity System & Streak Balancing", "plan_future_ideas.html#autotoc_md436", [
          [ "2.1 Per-Level Balloon & Item Weights", "plan_future_ideas.html#autotoc_md437", null ],
          [ "2.2 Pity System for Weight Randoms", "plan_future_ideas.html#autotoc_md438", null ],
          [ "2.3 Color Streak Balancing", "plan_future_ideas.html#autotoc_md439", null ],
          [ "2.4 Full Grid Clear-Out Calculation", "plan_future_ideas.html#autotoc_md440", null ]
        ] ],
        [ "3 — Custom Level Editor", "plan_future_ideas.html#autotoc_md442", [
          [ "3.1 Level Sequence Model", "plan_future_ideas.html#autotoc_md443", null ],
          [ "3.2 LevelDefinition ScriptableObject", "plan_future_ideas.html#autotoc_md444", null ],
          [ "3.3 Custom Level Editor Window (Unity Editor)", "plan_future_ideas.html#autotoc_md445", null ],
          [ "3.4 Level Sequence Integration", "plan_future_ideas.html#autotoc_md446", null ]
        ] ],
        [ "4 — Tutorial System & Pacing", "plan_future_ideas.html#autotoc_md448", [
          [ "4.1 Tutorial Trigger System", "plan_future_ideas.html#autotoc_md449", null ],
          [ "4.2 Tutorial Sequence Definition", "plan_future_ideas.html#autotoc_md450", null ],
          [ "4.3 Pacing — Suggested Actor Introduction Order", "plan_future_ideas.html#autotoc_md451", null ],
          [ "4.4 Tutorial UI", "plan_future_ideas.html#autotoc_md452", null ],
          [ "4.5 Persistence", "plan_future_ideas.html#autotoc_md453", null ]
        ] ],
        [ "5 — Relocated Future Ideas", "plan_future_ideas.html#autotoc_md455", [
          [ "5.1 Soap Cluster Merge (from PLAN-ContentProduction + PLAN-GridActorExpansion)", "plan_future_ideas.html#autotoc_md456", null ],
          [ "5.2 Unbreakable Roam (from PLAN-ContentProduction) — ✅ SHIPPED 2026-07-12", "plan_future_ideas.html#autotoc_md457", null ],
          [ "5.3 New Balloon Archetypes (from PLAN-GridActorExpansion)", "plan_future_ideas.html#autotoc_md458", null ],
          [ "5.4 New Grid Actor Archetypes (from PLAN-GridActorExpansion)", "plan_future_ideas.html#autotoc_md459", null ],
          [ "5.5 IPassThrough Behaviour Extensions (from PLAN-GridActorExpansion)", "plan_future_ideas.html#autotoc_md460", null ],
          [ "5.6 Spider Web Obstacle (from PLAN-SpiderWeb)", "plan_future_ideas.html#autotoc_md461", null ]
        ] ],
        [ "6 — Puff Cloud Polish & Performance", "plan_future_ideas.html#autotoc_md463", [
          [ "6.1 Visual Polish", "plan_future_ideas.html#autotoc_md464", null ],
          [ "6.2 Performance & Device Scaling", "plan_future_ideas.html#autotoc_md465", null ],
          [ "6.3 Edge Cases", "plan_future_ideas.html#autotoc_md466", null ]
        ] ],
        [ "7 — Vertical Cloud Drift", "plan_future_ideas.html#autotoc_md468", null ],
        [ "8 — Timed Release: Balloon Pass-Through Delay", "plan_future_ideas.html#autotoc_md470", [
          [ "8.1 Current Behaviour", "plan_future_ideas.html#autotoc_md471", null ],
          [ "8.2 Proposed Flow", "plan_future_ideas.html#autotoc_md472", null ],
          [ "8.3 Model Sketch", "plan_future_ideas.html#autotoc_md473", null ],
          [ "8.4 Balancer Changes", "plan_future_ideas.html#autotoc_md474", null ],
          [ "8.5 Visual Feedback", "plan_future_ideas.html#autotoc_md475", null ],
          [ "8.6 Relationship to Existing Pass-Through Ideas", "plan_future_ideas.html#autotoc_md476", null ],
          [ "8.7 Open Questions", "plan_future_ideas.html#autotoc_md477", null ]
        ] ],
        [ "Open Questions", "plan_future_ideas.html#autotoc_md479", null ],
        [ "9 — Quality Settings System", "plan_future_ideas.html#autotoc_md481", [
          [ "9.1 Architecture", "plan_future_ideas.html#autotoc_md482", null ],
          [ "9.2 Parameter Inventory", "plan_future_ideas.html#autotoc_md483", [
            [ "Shader Keywords (GPU cost)", "plan_future_ideas.html#autotoc_md484", null ],
            [ "Disturbance Field (GPU + memory)", "plan_future_ideas.html#autotoc_md485", null ],
            [ "Unbreakable Balloon GrabPass (GPU — single highest-cost operation)", "plan_future_ideas.html#autotoc_md486", null ],
            [ "Particle VFX (GPU fill rate + CPU simulation)", "plan_future_ideas.html#autotoc_md487", null ],
            [ "Projectile Trail (GPU fill rate)", "plan_future_ideas.html#autotoc_md488", null ],
            [ "Procedural Shader Octave Count", "plan_future_ideas.html#autotoc_md489", null ],
            [ "Animation Quality", "plan_future_ideas.html#autotoc_md490", null ],
            [ "Pool Sizes", "plan_future_ideas.html#autotoc_md491", null ]
          ] ],
          [ "9.3 Suggested Tier Thresholds", "plan_future_ideas.html#autotoc_md492", null ],
          [ "9.4 Implementation Priorities", "plan_future_ideas.html#autotoc_md493", null ],
          [ "9.5 Open Questions", "plan_future_ideas.html#autotoc_md494", null ]
        ] ],
        [ "10 — Baked Noise Texture for Puff Clouds", "plan_future_ideas.html#autotoc_md496", null ],
        [ "11 — Runtime Bush Baking at Preload", "plan_future_ideas.html#autotoc_md498", null ],
        [ "12 — Losing Conditions", "plan_future_ideas.html#autotoc_md500", [
          [ "12.1 Grid encroachment — the natural fit (Puzzle Bobble lineage)", "plan_future_ideas.html#autotoc_md501", null ],
          [ "12.2 Resource economy — make the existing Shield item matter", "plan_future_ideas.html#autotoc_md502", null ],
          [ "12.3 Clock / turn pressure — score-attack flavor", "plan_future_ideas.html#autotoc_md503", null ],
          [ "12.4 Lockout / soft-lock (safety net, not a headline mechanic)", "plan_future_ideas.html#autotoc_md504", null ],
          [ "12.5 How these compose", "plan_future_ideas.html#autotoc_md505", null ]
        ] ],
        [ "13 — Roguelike Run Modifiers (Unlockables)", "plan_future_ideas.html#autotoc_md507", [
          [ "13.1 Concept", "plan_future_ideas.html#autotoc_md508", null ],
          [ "13.2 When unlocks are offered", "plan_future_ideas.html#autotoc_md509", null ],
          [ "13.3 Model sketch", "plan_future_ideas.html#autotoc_md510", null ],
          [ "13.4 Passive modifier ideas", "plan_future_ideas.html#autotoc_md511", null ],
          [ "13.5 Active \"spawn now\" + grid-effect ideas", "plan_future_ideas.html#autotoc_md512", null ],
          [ "13.6 Integration points (existing systems)", "plan_future_ideas.html#autotoc_md513", null ],
          [ "13.7 Card presentation (the open visual question)", "plan_future_ideas.html#autotoc_md514", null ],
          [ "13.8 Open questions", "plan_future_ideas.html#autotoc_md515", null ]
        ] ],
        [ "14 — Level Pacing Follow-ups (post-ship)", "plan_future_ideas.html#autotoc_md517", [
          [ "14.1 Jump-to-level cheat — ✅ SHIPPED", "plan_future_ideas.html#autotoc_md518", null ],
          [ "14.2 Extract a shared LevelPacingValidator — ✅ SHIPPED", "plan_future_ideas.html#autotoc_md519", null ],
          [ "14.3 Escalate LevelDifficultyResolver.FallbackParameters to a hard failure", "plan_future_ideas.html#autotoc_md520", null ]
        ] ],
        [ "15 — Level-Up Point Carry-Over (2026-07-18)", "plan_future_ideas.html#autotoc_md521", null ],
        [ "16 — Grand Antiprism: the 100 denomination (2026-07-19)", "plan_future_ideas.html#autotoc_md522", null ],
        [ "Bush Visual Polish", "plan_future_ideas.html#autotoc_md524", null ]
      ] ]
    ] ],
    [ "HDR Color Pipeline", "plan_hdr_color_pipeline.html", [
      [ "HDR Color Pipeline — migration plan", "plan_hdr_color_pipeline.html#autotoc_md613", [
        [ "Decision & scope", "plan_hdr_color_pipeline.html#autotoc_md615", null ],
        [ "Verified inventory (2026-07-11)", "plan_hdr_color_pipeline.html#autotoc_md616", null ],
        [ "Task plan", "plan_hdr_color_pipeline.html#autotoc_md617", [
          [ "Dependency graph", "plan_hdr_color_pipeline.html#autotoc_md618", null ],
          [ "Wave A — Linear color space (the foundation, and the cost gate)", "plan_hdr_color_pipeline.html#autotoc_md619", [
            [ "A0 — Baseline captures · P0 · S", "plan_hdr_color_pipeline.html#autotoc_md620", null ],
            [ "A1 — Flip to Linear · P0 · S (mechanical) — do NOT merge without A2", "plan_hdr_color_pipeline.html#autotoc_md621", null ],
            [ "A2 — Look re-tune · P0 · L — art-driven, art-led", "plan_hdr_color_pipeline.html#autotoc_md622", null ],
            [ "A-gate — parity/quality sign-off + perf sanity", "plan_hdr_color_pipeline.html#autotoc_md623", null ]
          ] ],
          [ "Wave B — HDR rendering + post + bloom", "plan_hdr_color_pipeline.html#autotoc_md624", [
            [ "B1 — HDR target + post scaffold · P0 · S", "plan_hdr_color_pipeline.html#autotoc_md625", null ],
            [ "B2 — Tonemapping · P0 · S", "plan_hdr_color_pipeline.html#autotoc_md626", null ],
            [ "B3 — Emissive authoring path · P1 · M", "plan_hdr_color_pipeline.html#autotoc_md627", null ],
            [ "B4 — Bloom · P1 · S–M", "plan_hdr_color_pipeline.html#autotoc_md628", null ],
            [ "B5 — Tooling: Render Maps HDR view · P2 · S (parallel after B1)", "plan_hdr_color_pipeline.html#autotoc_md629", null ],
            [ "B6 — GI/capture chain under HDR · P1 · M", "plan_hdr_color_pipeline.html#autotoc_md630", null ],
            [ "B-gate — device sign-off", "plan_hdr_color_pipeline.html#autotoc_md631", null ]
          ] ],
          [ "Wave C — HDR display output · deferred, own trigger", "plan_hdr_color_pipeline.html#autotoc_md632", null ]
        ] ],
        [ "Open questions (answer at execution time)", "plan_hdr_color_pipeline.html#autotoc_md633", null ]
      ] ]
    ] ],
    [ "Terrain & Biomes", "plan_terrain_biomes.html", [
      [ "Terrain & Biomes: sky and ground scenarios", "plan_terrain_biomes.html#autotoc_md755", [
        [ "Why now", "plan_terrain_biomes.html#autotoc_md757", null ],
        [ "Verified inventory (2026-11-08)", "plan_terrain_biomes.html#autotoc_md758", null ],
        [ "The smallest version", "plan_terrain_biomes.html#autotoc_md759", [
          [ "Which levels are ground", "plan_terrain_biomes.html#autotoc_md760", null ],
          [ "How it looks", "plan_terrain_biomes.html#autotoc_md761", null ],
          [ "Performance (mobile, 120 Hz = 8.33 ms frame)", "plan_terrain_biomes.html#autotoc_md762", null ],
          [ "What it reuses", "plan_terrain_biomes.html#autotoc_md763", null ],
          [ "Shape (MVC)", "plan_terrain_biomes.html#autotoc_md764", null ],
          [ "Phases", "plan_terrain_biomes.html#autotoc_md765", null ]
        ] ],
        [ "Decisions (signed off 2026-11-08)", "plan_terrain_biomes.html#autotoc_md766", null ],
        [ "In-editor checks for phase 1", "plan_terrain_biomes.html#autotoc_md767", null ],
        [ "In-editor checks for phase 2", "plan_terrain_biomes.html#autotoc_md768", null ],
        [ "Follow-ups from the phase 1 optimizer review", "plan_terrain_biomes.html#autotoc_md769", null ],
        [ "Later: the full biome system (unscheduled)", "plan_terrain_biomes.html#autotoc_md771", null ]
      ] ]
    ] ],
    [ "Water Scenario & Bubble Merge", "plan_water_scenario.html", [
      [ "Water scenario, rising bubbles and the bubble merge", "plan_water_scenario.html#autotoc_md772", [
        [ "Owner direction (2026-11-10)", "plan_water_scenario.html#autotoc_md774", null ],
        [ "What exists (verified 2026-11-10)", "plan_water_scenario.html#autotoc_md775", null ],
        [ "Design", "plan_water_scenario.html#autotoc_md776", [
          [ "Water in the schedule", "plan_water_scenario.html#autotoc_md777", null ],
          [ "The water backdrop", "plan_water_scenario.html#autotoc_md778", null ],
          [ "Bubbles that build up on water", "plan_water_scenario.html#autotoc_md779", null ],
          [ "The merge", "plan_water_scenario.html#autotoc_md780", null ]
        ] ],
        [ "Owner decisions (2026-11-10)", "plan_water_scenario.html#autotoc_md781", null ],
        [ "Phases", "plan_water_scenario.html#autotoc_md782", null ]
      ] ]
    ] ],
    [ "Gameplay Telemetry", "plan_gameplay_telemetry.html", [
      [ "Gameplay Metrics & Telemetry", "plan_gameplay_telemetry.html#autotoc_md525", [
        [ "Status", "plan_gameplay_telemetry.html#autotoc_md527", [
          [ "Where the JSON goes", "plan_gameplay_telemetry.html#autotoc_md528", null ]
        ] ],
        [ "Goals", "plan_gameplay_telemetry.html#autotoc_md530", null ],
        [ "Requirements", "plan_gameplay_telemetry.html#autotoc_md532", [
          [ "Scopes and boundaries", "plan_gameplay_telemetry.html#autotoc_md533", null ],
          [ "Vocabulary", "plan_gameplay_telemetry.html#autotoc_md534", null ],
          [ "State machine", "plan_gameplay_telemetry.html#autotoc_md535", null ],
          [ "Fields and derivation", "plan_gameplay_telemetry.html#autotoc_md536", null ],
          [ "Read model (in-game consumption)", "plan_gameplay_telemetry.html#autotoc_md537", null ],
          [ "Lifecycle and registration", "plan_gameplay_telemetry.html#autotoc_md538", null ],
          [ "Export", "plan_gameplay_telemetry.html#autotoc_md539", null ],
          [ "Performance", "plan_gameplay_telemetry.html#autotoc_md540", null ]
        ] ],
        [ "Separability — this becomes an external library", "plan_gameplay_telemetry.html#autotoc_md543", [
          [ "What the library owns", "plan_gameplay_telemetry.html#autotoc_md544", null ],
          [ "What the game supplies", "plan_gameplay_telemetry.html#autotoc_md545", null ],
          [ "What extraction actually costs", "plan_gameplay_telemetry.html#autotoc_md546", null ],
          [ "Why the JSON stays hand-rolled", "plan_gameplay_telemetry.html#autotoc_md547", null ]
        ] ],
        [ "Risk register", "plan_gameplay_telemetry.html#autotoc_md549", null ],
        [ "Not built", "plan_gameplay_telemetry.html#autotoc_md552", [
          [ "Export decorators", "plan_gameplay_telemetry.html#autotoc_md553", [
            [ "Cadence: one queue, many triggers", "plan_gameplay_telemetry.html#autotoc_md554", null ],
            [ "Offline is a queue problem, not a timestamp problem", "plan_gameplay_telemetry.html#autotoc_md555", null ]
          ] ],
          [ "HTTP analytics sink", "plan_gameplay_telemetry.html#autotoc_md556", null ]
        ] ],
        [ "Deferred, deliberately", "plan_gameplay_telemetry.html#autotoc_md558", null ]
      ] ]
    ] ],
    [ "Performance Recovery", "plan_performance_recovery.html", [
      [ "Performance Recovery Plan — BalloonParty (Phase 2, revised 2026-07-23)", "plan_performance_recovery.html#autotoc_md682", [
        [ "Execution model & delegation matrix", "plan_performance_recovery.html#autotoc_md684", null ],
        [ "Phase 1 — Completed ✅ (spot-checked 2026-07-23: all commits exist and match)", "plan_performance_recovery.html#autotoc_md686", null ],
        [ "Step 0: Diagnose before optimizing (mandatory, do first)", "plan_performance_recovery.html#autotoc_md688", [
          [ "Preconditions — record backend and build type", "plan_performance_recovery.html#autotoc_md689", null ],
          [ "Pacing / ARR check", "plan_performance_recovery.html#autotoc_md690", null ],
          [ "CPU vs GPU classification", "plan_performance_recovery.html#autotoc_md691", null ],
          [ "Overdraw look (editor)", "plan_performance_recovery.html#autotoc_md692", null ],
          [ "AGI — Mali fragment vs bandwidth", "plan_performance_recovery.html#autotoc_md693", null ],
          [ "Thermal baseline", "plan_performance_recovery.html#autotoc_md694", null ],
          [ "Findings — first pass, 2026-07-23 (cool device, release build, live gameplay)", "plan_performance_recovery.html#autotoc_md695", null ],
          [ "Findings — second pass, 2026-07-23 evening (90s perfetto capture during live play)", "plan_performance_recovery.html#autotoc_md696", null ],
          [ "Findings — third pass, 2026-07-23 night (20-min longitudinal monitor, 15s cadence)", "plan_performance_recovery.html#autotoc_md697", null ],
          [ "Findings — fourth pass, 2026-07-23 night (simpleperf callstacks, dev build)", "plan_performance_recovery.html#autotoc_md698", null ],
          [ "Decision tree", "plan_performance_recovery.html#autotoc_md699", null ]
        ] ],
        [ "Tier 0: Free / Near-Free Wins", "plan_performance_recovery.html#autotoc_md701", [
          [ "F2 — Remove the shrink Array.Resize in TransformRibbon · sonnet · ✅ DONE (a4581825)", "plan_performance_recovery.html#autotoc_md702", null ],
          [ "F3 — SetCharArray in RollingTextAnimator · haiku · ✅ DONE (a4581825)", "plan_performance_recovery.html#autotoc_md703", null ],
          [ "F4 — Shader variant warmup · sonnet (capture = José)", "plan_performance_recovery.html#autotoc_md704", null ],
          [ "C1 + C3 — Projectile cleanups · sonnet, one session (same file)", "plan_performance_recovery.html#autotoc_md705", null ]
        ] ],
        [ "Tier 1: UI arrival storm — RESOLVED 2026-07-23: U1 reverted, U2 kept", "plan_performance_recovery.html#autotoc_md707", [
          [ "U1 — De-Animator the progress-bar hit pulse · opus", "plan_performance_recovery.html#autotoc_md708", null ],
          [ "U2 — Eliminate ProgressNotice reparenting · sonnet (+ mandatory fable review of pool consumers)", "plan_performance_recovery.html#autotoc_md709", null ]
        ] ],
        [ "Tier 2: GPU / Shader (re-prioritized for Mali-G715)", "plan_performance_recovery.html#autotoc_md711", [
          [ "G6 — fp16 in ScreenSpaceLightSmear.shader · opus, same session as G3", "plan_performance_recovery.html#autotoc_md712", null ],
          [ "G3 — 4-tap bilinear blur (Pass 1) · with G6", "plan_performance_recovery.html#autotoc_md713", null ],
          [ "G4 — BackgroundField bake fine-octave skip · SHIPPED THEN REVERTED 2026-07-23", "plan_performance_recovery.html#autotoc_md714", null ],
          [ "G1 — Blit budget controller · opus, after Step 0 confirms spikes matter", "plan_performance_recovery.html#autotoc_md715", null ],
          [ "G5 — Gradient-skip · DEFERRED — riskier than it looks (opus if ever done)", "plan_performance_recovery.html#autotoc_md716", null ]
        ] ],
        [ "Tier 3: Architectural (gated on Step 0 evidence)", "plan_performance_recovery.html#autotoc_md718", [
          [ "A4 — Quality tiers + ADPF · opus — required for ship, promoted earlier", "plan_performance_recovery.html#autotoc_md719", null ],
          [ "A1 — Render Graph prototype · opus — go/no-go, only on Step-0 GPU evidence", "plan_performance_recovery.html#autotoc_md720", null ],
          [ "A2 — SceneCapture → Renderer Feature · parked behind A1", "plan_performance_recovery.html#autotoc_md721", null ]
        ] ],
        [ "Quality vetoes — 2026-07-23 device pass (José)", "plan_performance_recovery.html#autotoc_md723", null ],
        [ "Dropped in the 2026-07-23 revision", "plan_performance_recovery.html#autotoc_md724", null ],
        [ "Investigated and Skipped (carried over)", "plan_performance_recovery.html#autotoc_md725", null ],
        [ "Dependency map & execution waves", "plan_performance_recovery.html#autotoc_md727", null ],
        [ "Verification Protocol", "plan_performance_recovery.html#autotoc_md729", null ]
      ] ]
    ] ],
    [ "Audio / SFX", "plan_audio.html", [
      [ "Audio / SFX", "plan_audio.html#autotoc_md290", [
        [ "Principles", "plan_audio.html#autotoc_md292", null ],
        [ "Architecture", "plan_audio.html#autotoc_md294", [
          [ "Folder structure", "plan_audio.html#autotoc_md295", null ],
          [ "Class diagram", "plan_audio.html#autotoc_md296", null ],
          [ "Sequence diagram — event to sound", "plan_audio.html#autotoc_md297", null ],
          [ "Dependency graph — VContainer wiring", "plan_audio.html#autotoc_md298", null ],
          [ "Voice lifecycle", "plan_audio.html#autotoc_md299", null ],
          [ "Responsibility map", "plan_audio.html#autotoc_md300", null ]
        ] ],
        [ "Channels (the pause / context answer)", "plan_audio.html#autotoc_md302", null ],
        [ "Spatialization", "plan_audio.html#autotoc_md304", null ],
        [ "SFX moment inventory", "plan_audio.html#autotoc_md306", null ],
        [ "Melodic pops (streak-driven scale)", "plan_audio.html#autotoc_md308", null ],
        [ "Phase 1 — Core loop, no message changes — SHIPPED (Steps 1-6)", "plan_audio.html#autotoc_md310", [
          [ "First-pass sounds to author", "plan_audio.html#autotoc_md311", null ],
          [ "Voice management", "plan_audio.html#autotoc_md312", null ],
          [ "Return scheduling", "plan_audio.html#autotoc_md313", null ],
          [ "Teardown", "plan_audio.html#autotoc_md314", null ],
          [ "Clip import settings (device)", "plan_audio.html#autotoc_md315", null ],
          [ "GC / hot-path", "plan_audio.html#autotoc_md316", null ]
        ] ],
        [ "Phase 2 — Fill-out + optional signals", "plan_audio.html#autotoc_md318", null ],
        [ "Phase 3 — Deferred", "plan_audio.html#autotoc_md320", null ],
        [ "Test strategy", "plan_audio.html#autotoc_md322", null ],
        [ "Open questions", "plan_audio.html#autotoc_md324", null ]
      ] ]
    ] ],
    [ "Shot Solver Accuracy", "plan_shot_solver_accuracy.html", [
      [ "Shot Solver Accuracy", "plan_shot_solver_accuracy.html#autotoc_md730", [
        [ "Diagnostic", "plan_shot_solver_accuracy.html#autotoc_md732", null ],
        [ "Goals & non-goals", "plan_shot_solver_accuracy.html#autotoc_md733", null ],
        [ "Architecture decisions (settled)", "plan_shot_solver_accuracy.html#autotoc_md734", null ],
        [ "Phases", "plan_shot_solver_accuracy.html#autotoc_md735", [
          [ "Phase 0 — Prerequisite refactors (no behavior change; the existing 22-test suite stays green)", "plan_shot_solver_accuracy.html#autotoc_md736", null ],
          [ "Phase A — Interactive static geometry (G1) — depends on 0b", "plan_shot_solver_accuracy.html#autotoc_md737", null ],
          [ "Phase B — Weight-system fidelity (G2) — depends on 0b; parallel to A", "plan_shot_solver_accuracy.html#autotoc_md738", null ],
          [ "Phase D-core — Rainbow scoring + in-sim buff state (G4-scoring, G8) — depends on 0a", "plan_shot_solver_accuracy.html#autotoc_md739", null ],
          [ "✅ Phase C — Item carriers (G3) — depends on B + D-core", "plan_shot_solver_accuracy.html#autotoc_md740", null ],
          [ "Phase E — Flight residuals (G5, G6, G7 + E4) — depends on 0a; E2 folds C6", "plan_shot_solver_accuracy.html#autotoc_md741", null ],
          [ "Phase F — Nondeterminism policy + instrumentation (G9) — last", "plan_shot_solver_accuracy.html#autotoc_md742", null ],
          [ "Phase G — Headless level diagnostics (follow-up tier; scoped 2026-07-25)", "plan_shot_solver_accuracy.html#autotoc_md743", null ]
        ] ],
        [ "Test plan (per test-everything; full detail in the review transcript)", "plan_shot_solver_accuracy.html#autotoc_md744", null ],
        [ "Verification workflow", "plan_shot_solver_accuracy.html#autotoc_md745", null ],
        [ "Open decisions", "plan_shot_solver_accuracy.html#autotoc_md746", null ],
        [ "Remaining work — detailed status (2026-07-26)", "plan_shot_solver_accuracy.html#autotoc_md747", [
          [ "E — flight residuals (next; ONE architect memo for all four, they interact)", "plan_shot_solver_accuracy.html#autotoc_md748", null ],
          [ "F — instrumentation + acceptance (after E)", "plan_shot_solver_accuracy.html#autotoc_md749", null ],
          [ "Live repoint track (any time; separable commits)", "plan_shot_solver_accuracy.html#autotoc_md750", null ],
          [ "José's gates (accumulated)", "plan_shot_solver_accuracy.html#autotoc_md751", null ],
          [ "Deferred code follow-ups (small, none blocking)", "plan_shot_solver_accuracy.html#autotoc_md752", null ],
          [ "G — headless level diagnostics (follow-up tier; unchanged spec in §4 Phase G)", "plan_shot_solver_accuracy.html#autotoc_md753", null ],
          [ "Design questions parked for José", "plan_shot_solver_accuracy.html#autotoc_md754", null ]
        ] ]
      ] ]
    ] ],
    [ "Web Demo Hosting", "plan_web_demo_hosting.html", [
      [ "Web Demo Hosting", "plan_web_demo_hosting.html#autotoc_md783", [
        [ "Why keep it", "plan_web_demo_hosting.html#autotoc_md785", null ],
        [ "What survived", "plan_web_demo_hosting.html#autotoc_md786", null ],
        [ "The recipe, if resumed", "plan_web_demo_hosting.html#autotoc_md787", null ],
        [ "The blocker", "plan_web_demo_hosting.html#autotoc_md788", null ],
        [ "Residue from the attempt", "plan_web_demo_hosting.html#autotoc_md789", null ],
        [ "If web stays dead", "plan_web_demo_hosting.html#autotoc_md790", null ],
        [ "Decision log", "plan_web_demo_hosting.html#autotoc_md791", null ]
      ] ]
    ] ],
    [ "Level-Up Timing", "plan_level_up_timing.html", [
      [ "Level-Up Timing", "plan_level_up_timing.html#autotoc_md661", [
        [ "The bug this fixes", "plan_level_up_timing.html#autotoc_md663", null ],
        [ "The model", "plan_level_up_timing.html#autotoc_md664", null ],
        [ "2.1 The signals, by name", "plan_level_up_timing.html#autotoc_md665", null ],
        [ "What this deletes", "plan_level_up_timing.html#autotoc_md666", null ],
        [ "Detect on projected progress; keep tipping identity as presentation", "plan_level_up_timing.html#autotoc_md667", null ],
        [ "The orchestrator IS the existing phase machine", "plan_level_up_timing.html#autotoc_md668", null ],
        [ "The edits", "plan_level_up_timing.html#autotoc_md669", [
          [ "6.1 TimeScaleService — exclusivity", "plan_level_up_timing.html#autotoc_md670", null ],
          [ "6.2 The two windows", "plan_level_up_timing.html#autotoc_md671", null ],
          [ "6.3 LevelController — the exact edits", "plan_level_up_timing.html#autotoc_md672", null ],
          [ "6.4 New message: LevelUpAbandonedMessage", "plan_level_up_timing.html#autotoc_md673", null ],
          [ "6.5 The holds — predicate is Phase != Playing, NEVER == Completing", "plan_level_up_timing.html#autotoc_md674", null ],
          [ "6.6 ColorProgressBar must keep drawing during Completing", "plan_level_up_timing.html#autotoc_md675", null ],
          [ "6.7 The pan-in stops pausing; the camera follows the shot", "plan_level_up_timing.html#autotoc_md676", null ]
        ] ],
        [ "Tests", "plan_level_up_timing.html#autotoc_md677", null ],
        [ "Sequencing", "plan_level_up_timing.html#autotoc_md678", null ],
        [ "Risks — playtest, not compile", "plan_level_up_timing.html#autotoc_md679", null ],
        [ "Known residual (scoped, cosmetic)", "plan_level_up_timing.html#autotoc_md680", null ],
        [ "Keep the watchdog regardless", "plan_level_up_timing.html#autotoc_md681", null ]
      ] ]
    ] ],
    [ "Item Range Preview", "plan_item_range_preview.html", [
      [ "Item Range Preview", "plan_item_range_preview.html#autotoc_md648", [
        [ "Orientation", "plan_item_range_preview.html#autotoc_md650", null ],
        [ "The seam", "plan_item_range_preview.html#autotoc_md652", null ],
        [ "Driving the pens", "plan_item_range_preview.html#autotoc_md654", null ],
        [ "Where the trigger lives", "plan_item_range_preview.html#autotoc_md656", null ],
        [ "Phases", "plan_item_range_preview.html#autotoc_md658", null ],
        [ "Open questions", "plan_item_range_preview.html#autotoc_md660", null ]
      ] ]
    ] ],
    [ "Fast-Paced Endless Mode", "plan_fast_paced.html", [
      [ "Fast-Paced Endless Mode", "plan_fast_paced.html#autotoc_md409", [
        [ "Why", "plan_fast_paced.html#autotoc_md411", null ],
        [ "The game after this plan", "plan_fast_paced.html#autotoc_md412", null ],
        [ "Phases", "plan_fast_paced.html#autotoc_md413", null ],
        [ "Phase 1: projectile identity (no behaviour change)", "plan_fast_paced.html#autotoc_md415", null ],
        [ "Phase 2: rapid fire", "plan_fast_paced.html#autotoc_md417", null ],
        [ "Phase 3: spawn clock", "plan_fast_paced.html#autotoc_md419", null ],
        [ "Phase 4: generated levels", "plan_fast_paced.html#autotoc_md421", null ],
        [ "Phase 5: checkpoint and upgrades", "plan_fast_paced.html#autotoc_md423", null ],
        [ "Risks", "plan_fast_paced.html#autotoc_md425", null ],
        [ "Open questions", "plan_fast_paced.html#autotoc_md426", null ]
      ] ]
    ] ],
    [ "Handwritten Heads-Ups", "plan_headsup.html", [
      [ "Handwritten Heads-Ups", "plan_headsup.html#autotoc_md634", [
        [ "Why", "plan_headsup.html#autotoc_md636", null ],
        [ "What the player sees", "plan_headsup.html#autotoc_md637", null ],
        [ "Architecture", "plan_headsup.html#autotoc_md638", null ],
        [ "Rendering", "plan_headsup.html#autotoc_md639", [
          [ "4.1 Write-on text", "plan_headsup.html#autotoc_md640", null ],
          [ "4.2 Pencil lines", "plan_headsup.html#autotoc_md641", null ]
        ] ],
        [ "New signals", "plan_headsup.html#autotoc_md642", null ],
        [ "What gets a heads-up", "plan_headsup.html#autotoc_md643", null ],
        [ "Phases", "plan_headsup.html#autotoc_md644", null ],
        [ "Tests", "plan_headsup.html#autotoc_md645", null ],
        [ "Risks", "plan_headsup.html#autotoc_md646", null ],
        [ "Open questions", "plan_headsup.html#autotoc_md647", null ]
      ] ]
    ] ],
    [ "Coins and the Card Shop", "plan_coins.html", [
      [ "Coins and the Card Shop", "plan_coins.html#autotoc_md366", [
        [ "Why", "plan_coins.html#autotoc_md368", null ],
        [ "The loop", "plan_coins.html#autotoc_md369", null ],
        [ "Rules", "plan_coins.html#autotoc_md370", null ],
        [ "Architecture", "plan_coins.html#autotoc_md371", null ],
        [ "Phases", "plan_coins.html#autotoc_md372", null ],
        [ "Edge cases", "plan_coins.html#autotoc_md373", null ],
        [ "Assets", "plan_coins.html#autotoc_md374", null ],
        [ "Tuning targets", "plan_coins.html#autotoc_md375", null ],
        [ "Open questions", "plan_coins.html#autotoc_md376", null ],
        [ "Luck levels (proposal, after phase 2)", "plan_coins.html#autotoc_md377", null ]
      ] ]
    ] ],
    [ "The Card Reveal", "plan_card_reveal.html", [
      [ "The Card Reveal", "plan_card_reveal.html#autotoc_md356", [
        [ "Why", "plan_card_reveal.html#autotoc_md358", null ],
        [ "What the player sees", "plan_card_reveal.html#autotoc_md359", null ],
        [ "Rules", "plan_card_reveal.html#autotoc_md360", null ],
        [ "Architecture", "plan_card_reveal.html#autotoc_md361", null ],
        [ "How the pops are made", "plan_card_reveal.html#autotoc_md362", null ],
        [ "Assets", "plan_card_reveal.html#autotoc_md363", null ],
        [ "Phases", "plan_card_reveal.html#autotoc_md364", null ],
        [ "Open questions", "plan_card_reveal.html#autotoc_md365", null ]
      ] ]
    ] ]
];