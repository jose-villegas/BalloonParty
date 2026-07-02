var plans =
[
    [ "Plans", "plans.html#autotoc_md590", null ],
    [ "Bubble Cluster Hit Feedback", "plan_bubble_cluster_hit_feedback.html", [
      [ "Bubble Cluster Hit Feedback", "plan_bubble_cluster_hit_feedback.html#autotoc_md189", [
        [ "Goals", "plan_bubble_cluster_hit_feedback.html#autotoc_md191", null ],
        [ "Root cause of the existing laser rotation bug", "plan_bubble_cluster_hit_feedback.html#autotoc_md193", null ],
        [ "New interface — <span class=\"tt\">IBalloonHitHandler</span>", "plan_bubble_cluster_hit_feedback.html#autotoc_md195", null ],
        [ "Discovery and caching", "plan_bubble_cluster_hit_feedback.html#autotoc_md197", [
          [ "Root components — cache at <span class=\"tt\">Bind()</span> time", "plan_bubble_cluster_hit_feedback.html#autotoc_md198", null ],
          [ "Item visual components — register via <span class=\"tt\">ItemDisplayService</span>", "plan_bubble_cluster_hit_feedback.html#autotoc_md199", null ],
          [ "What goes away", "plan_bubble_cluster_hit_feedback.html#autotoc_md200", null ]
        ] ],
        [ "<span class=\"tt\">BalloonView</span> changes", "plan_bubble_cluster_hit_feedback.html#autotoc_md202", [
          [ "New methods", "plan_bubble_cluster_hit_feedback.html#autotoc_md203", null ],
          [ "Removed", "plan_bubble_cluster_hit_feedback.html#autotoc_md204", null ],
          [ "Internal", "plan_bubble_cluster_hit_feedback.html#autotoc_md205", null ]
        ] ],
        [ "<span class=\"tt\">BalloonController</span> changes", "plan_bubble_cluster_hit_feedback.html#autotoc_md207", null ],
        [ "<span class=\"tt\">SoapBubbleClusterVariant</span> — implements <span class=\"tt\">IBalloonHitHandler</span>", "plan_bubble_cluster_hit_feedback.html#autotoc_md209", [
          [ "C# mirror of shader bubble layouts", "plan_bubble_cluster_hit_feedback.html#autotoc_md210", null ],
          [ "<span class=\"tt\">GetVfxWorldPosition</span>", "plan_bubble_cluster_hit_feedback.html#autotoc_md211", null ],
          [ "<span class=\"tt\">OnHit</span> — spin impulse", "plan_bubble_cluster_hit_feedback.html#autotoc_md212", null ],
          [ "<span class=\"tt\">OnPrePop</span>", "plan_bubble_cluster_hit_feedback.html#autotoc_md213", null ]
        ] ],
        [ "<span class=\"tt\">LaserItemRotation</span> — migrates to <span class=\"tt\">IBalloonHitHandler</span>", "plan_bubble_cluster_hit_feedback.html#autotoc_md215", null ],
        [ "File summary", "plan_bubble_cluster_hit_feedback.html#autotoc_md217", null ],
        [ "Open questions", "plan_bubble_cluster_hit_feedback.html#autotoc_md219", null ]
      ] ]
    ] ],
    [ "Grid Actor Expansion — Phase 8+", "plan_grid_actor_expansion.html", [
      [ "Grid Actor Expansion — Phase 8+", "plan_grid_actor_expansion.html#autotoc_md419", [
        [ "Orientation", "plan_grid_actor_expansion.html#autotoc_md421", null ],
        [ "Actor Vocabulary — Design Reference", "plan_grid_actor_expansion.html#autotoc_md423", [
          [ "Balloon archetypes", "plan_grid_actor_expansion.html#autotoc_md424", null ],
          [ "Grid actor archetypes", "plan_grid_actor_expansion.html#autotoc_md425", null ],
          [ "Hit controller pattern for non-balloon actors", "plan_grid_actor_expansion.html#autotoc_md426", null ]
        ] ],
        [ "Phases", "plan_grid_actor_expansion.html#autotoc_md428", [
          [ "✅ Phase 8.0 — Spawner Coordination", "plan_grid_actor_expansion.html#autotoc_md430", [
            [ "What was built", "plan_grid_actor_expansion.html#autotoc_md431", null ]
          ] ],
          [ "✅ Phase 8.1a — Absorb Routing", "plan_grid_actor_expansion.html#autotoc_md433", [
            [ "What was built", "plan_grid_actor_expansion.html#autotoc_md434", null ]
          ] ],
          [ "✅ Phase 8.1b — DamageContext API Migration", "plan_grid_actor_expansion.html#autotoc_md436", [
            [ "What was built", "plan_grid_actor_expansion.html#autotoc_md437", null ]
          ] ],
          [ "Phase 8.1c — UnbreakableBalloonModel + BalloonModelBase Cleanup", "plan_grid_actor_expansion.html#autotoc_md439", [
            [ "<span class=\"tt\">UnbreakableBalloonModel</span>", "plan_grid_actor_expansion.html#autotoc_md440", null ],
            [ "<span class=\"tt\">ScoreValue</span> on <span class=\"tt\">BalloonModelBase</span>", "plan_grid_actor_expansion.html#autotoc_md441", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md442", null ]
          ] ],
          [ "Phase 8.2a — Structural Actors (Puff + Bush)", "plan_grid_actor_expansion.html#autotoc_md444", [
            [ "Folder structure", "plan_grid_actor_expansion.html#autotoc_md445", null ],
            [ "Files", "plan_grid_actor_expansion.html#autotoc_md446", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md447", null ]
          ] ],
          [ "Phase 8.2b — Indestructible Hitable Actors (Deflector + Absorber)", "plan_grid_actor_expansion.html#autotoc_md449", [
            [ "Files", "plan_grid_actor_expansion.html#autotoc_md450", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md451", null ]
          ] ],
          [ "Phase 8.2c — Gatekeeper + GridActorHitController", "plan_grid_actor_expansion.html#autotoc_md453", [
            [ "Files", "plan_grid_actor_expansion.html#autotoc_md454", null ],
            [ "<span class=\"tt\">GatekeeperActorModel</span>", "plan_grid_actor_expansion.html#autotoc_md455", null ],
            [ "<span class=\"tt\">GridActorHitController</span>", "plan_grid_actor_expansion.html#autotoc_md456", null ],
            [ "<span class=\"tt\">NudgeOverrides</span> cleanup on <span class=\"tt\">BalloonModelBase</span>", "plan_grid_actor_expansion.html#autotoc_md457", null ],
            [ "NudgeService decoupling (done alongside 8.2c)", "plan_grid_actor_expansion.html#autotoc_md458", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md459", null ]
          ] ],
          [ "Phase 8.3 — Procedural Placement Engine", "plan_grid_actor_expansion.html#autotoc_md461", [
            [ "Design", "plan_grid_actor_expansion.html#autotoc_md462", null ],
            [ "Migration path", "plan_grid_actor_expansion.html#autotoc_md463", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md464", null ]
          ] ],
          [ "Phase 8.4 — Difficulty + Level Coupling", "plan_grid_actor_expansion.html#autotoc_md466", [
            [ "Design", "plan_grid_actor_expansion.html#autotoc_md467", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md468", null ]
          ] ]
        ] ],
        [ "Open Questions", "plan_grid_actor_expansion.html#autotoc_md470", null ],
        [ "Current State", "plan_grid_actor_expansion.html#autotoc_md472", null ]
      ] ]
    ] ],
    [ "Grid Actor System — Foundation Reference (Phases 1–7.5)", "plan_grid_actor_foundation.html", [
      [ "Grid Actor System — Foundation Reference (Phases 1–7.5)", "plan_grid_actor_foundation.html#autotoc_md473", [
        [ "Key Design Decisions — Summary", "plan_grid_actor_foundation.html#autotoc_md475", [
          [ "Mobility axis: <span class=\"tt\">SlotActorKind</span>", "plan_grid_actor_foundation.html#autotoc_md476", null ],
          [ "Durability axis: <span class=\"tt\">IHitable</span> + <span class=\"tt\">IHasDurability</span>", "plan_grid_actor_foundation.html#autotoc_md477", null ],
          [ "Item slot axis: <span class=\"tt\">IHasItemSlot</span>", "plan_grid_actor_foundation.html#autotoc_md478", null ],
          [ "Spawner type selection", "plan_grid_actor_foundation.html#autotoc_md479", null ],
          [ "Capability interfaces in <span class=\"tt\">Slots/</span>", "plan_grid_actor_foundation.html#autotoc_md480", null ]
        ] ],
        [ "Phase Status", "plan_grid_actor_foundation.html#autotoc_md482", [
          [ "Phase 7 — key changes", "plan_grid_actor_foundation.html#autotoc_md483", null ],
          [ "Phase 7.5 — key changes", "plan_grid_actor_foundation.html#autotoc_md484", null ]
        ] ]
      ] ]
    ] ],
    [ "Test Gap — Pre-Bush Audit", "plan_test_gap.html", [
      [ "Test Gap — Pre-Bush Audit", "plan_test_gap.html#autotoc_md564", [
        [ "General test gaps (address before Bush)", "plan_test_gap.html#autotoc_md566", [
          [ "High priority", "plan_test_gap.html#autotoc_md567", [
            [ "<span class=\"tt\">BubbleClusterModelTests.cs</span>", "plan_test_gap.html#autotoc_md568", null ],
            [ "<span class=\"tt\">ColorStreakTrackerTests.cs</span>", "plan_test_gap.html#autotoc_md569", null ],
            [ "<span class=\"tt\">ClusterSlotSelectionStrategyTests.cs</span>", "plan_test_gap.html#autotoc_md570", null ],
            [ "<span class=\"tt\">WeightedPickTests.cs</span>", "plan_test_gap.html#autotoc_md571", null ],
            [ "<span class=\"tt\">PauseServiceTests.cs</span>", "plan_test_gap.html#autotoc_md572", null ]
          ] ],
          [ "Medium priority", "plan_test_gap.html#autotoc_md573", [
            [ "<span class=\"tt\">VectorMathHelperTests.cs</span>", "plan_test_gap.html#autotoc_md574", null ],
            [ "<span class=\"tt\">PathHelperTests.cs</span>", "plan_test_gap.html#autotoc_md575", null ]
          ] ],
          [ "Low priority (defer)", "plan_test_gap.html#autotoc_md576", null ]
        ] ],
        [ "Bush-plan-specific test gaps (address during Bush plan)", "plan_test_gap.html#autotoc_md578", null ],
        [ "Execution order", "plan_test_gap.html#autotoc_md580", null ],
        [ "Tests README update", "plan_test_gap.html#autotoc_md582", null ]
      ] ]
    ] ],
    [ "Bush — 2D Skeletal Plant System", "plan_bush_sprite_baking.html", [
      [ "Bush — 2D Skeletal Plant System", "plan_bush_sprite_baking.html#autotoc_md220", [
        [ "Status &amp; Phase Tracker", "plan_bush_sprite_baking.html#autotoc_md222", null ],
        [ "Core Idea", "plan_bush_sprite_baking.html#autotoc_md224", null ],
        [ "Architecture", "plan_bush_sprite_baking.html#autotoc_md226", null ],
        [ "Phase 0 — Cluster Infrastructure ✅ Done", "plan_bush_sprite_baking.html#autotoc_md228", null ],
        [ "Phase 1 — Leaf Baking ✅ Done", "plan_bush_sprite_baking.html#autotoc_md230", [
          [ "What exists now", "plan_bush_sprite_baking.html#autotoc_md231", null ],
          [ "Implemented leaf features", "plan_bush_sprite_baking.html#autotoc_md232", null ],
          [ "Leaf feature backlog (add one at a time)", "plan_bush_sprite_baking.html#autotoc_md233", null ],
          [ "Bake pipeline", "plan_bush_sprite_baking.html#autotoc_md234", null ],
          [ "Editor window layout", "plan_bush_sprite_baking.html#autotoc_md235", null ],
          [ "Session context for Phase 1", "plan_bush_sprite_baking.html#autotoc_md236", null ]
        ] ],
        [ "Phase 2 — Branch Map Baking + Leaf Extraction + Rendering ✅ Done", "plan_bush_sprite_baking.html#autotoc_md238", [
          [ "Overview", "plan_bush_sprite_baking.html#autotoc_md239", null ],
          [ "Task Breakdown", "plan_bush_sprite_baking.html#autotoc_md241", null ],
          [ "2.1 — Branch Bake Settings", "plan_bush_sprite_baking.html#autotoc_md243", null ],
          [ "2.2 — Fractal Branch Generator", "plan_bush_sprite_baking.html#autotoc_md245", null ],
          [ "2.3 — Branch Bake Shader", "plan_bush_sprite_baking.html#autotoc_md247", null ],
          [ "2.4 — Branch Baker", "plan_bush_sprite_baking.html#autotoc_md249", null ],
          [ "2.5 — Leaf Extractor", "plan_bush_sprite_baking.html#autotoc_md251", null ],
          [ "2.6 — BushVariantData ScriptableObject", "plan_bush_sprite_baking.html#autotoc_md253", null ],
          [ "2.7 — Editor Window: Branch Section + Export", "plan_bush_sprite_baking.html#autotoc_md255", null ],
          [ "2.8 — Runtime Branch Shader", "plan_bush_sprite_baking.html#autotoc_md257", null ],
          [ "2.8b — Runtime Leaf Shader", "plan_bush_sprite_baking.html#autotoc_md259", null ],
          [ "2.9 — Runtime BushView Refactor", "plan_bush_sprite_baking.html#autotoc_md261", null ],
          [ "2.10 — IBushSettings Extension", "plan_bush_sprite_baking.html#autotoc_md263", null ],
          [ "2.11 — Integration Test", "plan_bush_sprite_baking.html#autotoc_md265", null ],
          [ "Implementation Order (recommended)", "plan_bush_sprite_baking.html#autotoc_md267", null ],
          [ "What exists now (Phase 2)", "plan_bush_sprite_baking.html#autotoc_md269", null ],
          [ "Shared editor components created", "plan_bush_sprite_baking.html#autotoc_md270", null ],
          [ "Session context for Phase 2", "plan_bush_sprite_baking.html#autotoc_md272", null ],
          [ "Leaf Attachment Investigation (June 8 2026)", "plan_bush_sprite_baking.html#autotoc_md274", null ]
        ] ],
        [ "Phase 3 — Wind Animation (Idle) ✅ Done — GPU Vertex Shader", "plan_bush_sprite_baking.html#autotoc_md276", [
          [ "Implementation", "plan_bush_sprite_baking.html#autotoc_md277", null ],
          [ "Key design decisions", "plan_bush_sprite_baking.html#autotoc_md278", null ]
        ] ],
        [ "Phase 4 — Rattle (Disturbed State) ✅ Done — GPU Vertex Shader", "plan_bush_sprite_baking.html#autotoc_md280", [
          [ "How it works", "plan_bush_sprite_baking.html#autotoc_md281", null ],
          [ "Why this works without CPU spring physics", "plan_bush_sprite_baking.html#autotoc_md282", null ],
          [ "Settings (<span class=\"tt\">IBushSettings</span> / <span class=\"tt\">BushSettings</span>)", "plan_bush_sprite_baking.html#autotoc_md283", null ],
          [ "<span class=\"tt\">BushAnimator</span> removed", "plan_bush_sprite_baking.html#autotoc_md284", null ]
        ] ],
        [ "<span class=\"tt\">GameLifetimeScope</span> has been removed.", "plan_bush_sprite_baking.html#autotoc_md285", null ],
        [ "Phase 5 — Visual Polish", "plan_bush_sprite_baking.html#autotoc_md286", null ],
        [ "Performance Budget", "plan_bush_sprite_baking.html#autotoc_md288", null ],
        [ "What we salvage / discard", "plan_bush_sprite_baking.html#autotoc_md290", [
          [ "Salvage", "plan_bush_sprite_baking.html#autotoc_md291", null ],
          [ "Discard", "plan_bush_sprite_baking.html#autotoc_md292", null ]
        ] ]
      ] ]
    ] ],
    [ "Spider Web Obstacle — Ideation Plan", "plan_spider_web.html", [
      [ "Spider Web Obstacle — Ideation Plan", "plan_spider_web.html#autotoc_md541", [
        [ "Context", "plan_spider_web.html#autotoc_md543", null ],
        [ "What it is", "plan_spider_web.html#autotoc_md545", null ],
        [ "Gameplay role", "plan_spider_web.html#autotoc_md547", null ],
        [ "Model design", "plan_spider_web.html#autotoc_md549", null ],
        [ "Art direction — Ideas", "plan_spider_web.html#autotoc_md551", [
          [ "Option A — Classic Cobweb", "plan_spider_web.html#autotoc_md552", null ],
          [ "Option B — Messy Garden Web", "plan_spider_web.html#autotoc_md553", null ],
          [ "Option C — Procedural Shader Web", "plan_spider_web.html#autotoc_md554", null ],
          [ "Option D — Silk Curtain", "plan_spider_web.html#autotoc_md555", null ]
        ] ],
        [ "Visual degradation", "plan_spider_web.html#autotoc_md557", null ],
        [ "Required code changes", "plan_spider_web.html#autotoc_md559", null ],
        [ "Tests", "plan_spider_web.html#autotoc_md561", null ],
        [ "Open questions", "plan_spider_web.html#autotoc_md563", null ]
      ] ]
    ] ],
    [ "Content Production Plan — Pre-8.3 Assets", "plan_content_production.html", [
      [ "Content Production Plan — Pre-8.3 Assets", "plan_content_production.html#autotoc_md310", [
        [ "Context", "plan_content_production.html#autotoc_md312", null ],
        [ "Asset status overview", "plan_content_production.html#autotoc_md314", null ],
        [ "Shared infrastructure needed", "plan_content_production.html#autotoc_md316", [
          [ "<span class=\"tt\">IHasScoreColor</span> — score attribution ✅ Complete", "plan_content_production.html#autotoc_md317", null ],
          [ "<span class=\"tt\">GridActorConfiguration</span> ScriptableObject", "plan_content_production.html#autotoc_md319", null ],
          [ "<span class=\"tt\">GridActorView</span> prefab root pattern", "plan_content_production.html#autotoc_md320", null ]
        ] ],
        [ "Per-actor detail", "plan_content_production.html#autotoc_md322", [
          [ "Soap Cluster ✅ Done", "plan_content_production.html#autotoc_md324", null ],
          [ "Puff ✅ Done", "plan_content_production.html#autotoc_md326", null ],
          [ "Bush ✅ Done", "plan_content_production.html#autotoc_md328", null ],
          [ "Deflector", "plan_content_production.html#autotoc_md330", null ],
          [ "Absorber", "plan_content_production.html#autotoc_md332", null ],
          [ "Gatekeeper", "plan_content_production.html#autotoc_md334", null ]
        ] ],
        [ "Suggested production order", "plan_content_production.html#autotoc_md336", null ],
        [ "Asset folder conventions", "plan_content_production.html#autotoc_md338", null ],
        [ "Open questions for art direction", "plan_content_production.html#autotoc_md340", null ]
      ] ]
    ] ],
    [ "Loss Condition &amp; Pacing", "plan_loss_condition_pacing.html", [
      [ "Loss Condition &amp; Pacing", "plan_loss_condition_pacing.html#autotoc_md485", [
        [ "Orientation — start here", "plan_loss_condition_pacing.html#autotoc_md487", null ],
        [ "Current state (session handoff)", "plan_loss_condition_pacing.html#autotoc_md489", null ],
        [ "Why now", "plan_loss_condition_pacing.html#autotoc_md491", null ],
        [ "⚠️ Decision that gates everything: runs vs. persistent progression", "plan_loss_condition_pacing.html#autotoc_md493", null ],
        [ "Current mechanics (grounding)", "plan_loss_condition_pacing.html#autotoc_md495", null ],
        [ "Part A — Loss condition: grid encroachment", "plan_loss_condition_pacing.html#autotoc_md497", null ],
        [ "Part B — Per-level-range difficulty configuration", "plan_loss_condition_pacing.html#autotoc_md499", [
          [ "Data model", "plan_loss_condition_pacing.html#autotoc_md500", null ],
          [ "Resolver / mediator — single source of the live mix", "plan_loss_condition_pacing.html#autotoc_md501", null ],
          [ "Resolved decisions", "plan_loss_condition_pacing.html#autotoc_md502", null ]
        ] ],
        [ "Part C — Allowed colors (tutorialization)", "plan_loss_condition_pacing.html#autotoc_md504", null ],
        [ "Phasing", "plan_loss_condition_pacing.html#autotoc_md506", null ],
        [ "Phase 1 — detailed breakdown", "plan_loss_condition_pacing.html#autotoc_md508", [
          [ "Testability seams (build first)", "plan_loss_condition_pacing.html#autotoc_md509", null ],
          [ "Block A — core state + run-scoped save (pure C#, headless-verifiable)", "plan_loss_condition_pacing.html#autotoc_md510", null ],
          [ "Block B — in-place reset harness (built up front)", "plan_loss_condition_pacing.html#autotoc_md511", null ],
          [ "Block C — trigger + UI (in-editor)", "plan_loss_condition_pacing.html#autotoc_md512", null ],
          [ "Deferred cinematics (loss + danger) — handoff &amp; build recipe", "plan_loss_condition_pacing.html#autotoc_md513", null ],
          [ "Test strategy", "plan_loss_condition_pacing.html#autotoc_md514", null ]
        ] ],
        [ "Phase 2 — detailed breakdown (player health + spawn-saturation damage)", "plan_loss_condition_pacing.html#autotoc_md516", [
          [ "Why this is simpler than the deadline approach", "plan_loss_condition_pacing.html#autotoc_md517", null ],
          [ "Damage source — the \"rejected balloon\" pop (visual feedback)", "plan_loss_condition_pacing.html#autotoc_md518", null ],
          [ "Tasks", "plan_loss_condition_pacing.html#autotoc_md519", null ],
          [ "Integration / safety", "plan_loss_condition_pacing.html#autotoc_md520", null ],
          [ "Decision (supersedes Open question #2)", "plan_loss_condition_pacing.html#autotoc_md521", null ]
        ] ],
        [ "Phase 2.5 — detailed breakdown (pressure balance — soften the HP bleed)", "plan_loss_condition_pacing.html#autotoc_md523", [
          [ "How it works", "plan_loss_condition_pacing.html#autotoc_md524", null ],
          [ "Remaining (separate steps, as intended)", "plan_loss_condition_pacing.html#autotoc_md525", null ]
        ] ],
        [ "Risks &amp; interactions", "plan_loss_condition_pacing.html#autotoc_md526", null ],
        [ "Open questions", "plan_loss_condition_pacing.html#autotoc_md528", null ]
      ] ]
    ] ],
    [ "PlayMode Test Coverage", "plan_playmode_coverage.html", [
      [ "PlayMode Test Coverage", "plan_playmode_coverage.html#autotoc_md529", [
        [ "Why PlayMode (and why sparingly)", "plan_playmode_coverage.html#autotoc_md531", null ],
        [ "Current PlayMode coverage (3 tests)", "plan_playmode_coverage.html#autotoc_md532", null ],
        [ "Harness", "plan_playmode_coverage.html#autotoc_md533", null ],
        [ "Conventions for PlayMode tests", "plan_playmode_coverage.html#autotoc_md534", null ],
        [ "Candidate coverage (tiered by value × risk)", "plan_playmode_coverage.html#autotoc_md535", [
          [ "Tier 1 — zero coverage today, highest gameplay risk", "plan_playmode_coverage.html#autotoc_md536", null ],
          [ "Tier 2 — async / pooling / animation", "plan_playmode_coverage.html#autotoc_md537", null ],
          [ "Tier 3 — valuable, harder to assert", "plan_playmode_coverage.html#autotoc_md538", null ]
        ] ],
        [ "Sequencing", "plan_playmode_coverage.html#autotoc_md539", null ],
        [ "Open questions / constraints", "plan_playmode_coverage.html#autotoc_md540", null ]
      ] ]
    ] ],
    [ "Cinematics Architecture", "plan_cinematics_architecture.html", [
      [ "Cinematics Architecture", "plan_cinematics_architecture.html#autotoc_md293", [
        [ "Orientation — start here", "plan_cinematics_architecture.html#autotoc_md295", null ],
        [ "Current state (inventory)", "plan_cinematics_architecture.html#autotoc_md297", null ],
        [ "Part A — Traits instead of per-trait booleans", "plan_cinematics_architecture.html#autotoc_md299", null ],
        [ "Part B — Settings out of the scene", "plan_cinematics_architecture.html#autotoc_md300", [
          [ "Value recovery — authored values ARE NOT the code defaults", "plan_cinematics_architecture.html#autotoc_md301", null ]
        ] ],
        [ "Part C — The camera-rig cinematic abstraction", "plan_cinematics_architecture.html#autotoc_md302", null ],
        [ "Part D — Director lifecycle hardening", "plan_cinematics_architecture.html#autotoc_md303", null ],
        [ "Part E — Camera effects (deferred, name the seam now)", "plan_cinematics_architecture.html#autotoc_md304", null ],
        [ "Part F — Time-scale ownership (REQUIRED — usage is enforced)", "plan_cinematics_architecture.html#autotoc_md305", null ],
        [ "Phasing (each phase ships green: build + audit + tests + playtest)", "plan_cinematics_architecture.html#autotoc_md307", null ],
        [ "Verification", "plan_cinematics_architecture.html#autotoc_md308", null ],
        [ "Open questions (decide before the relevant phase)", "plan_cinematics_architecture.html#autotoc_md309", null ]
      ] ]
    ] ],
    [ "Audit Remediation", "plan_audit_remediation.html", [
      [ "Audit Remediation", "plan_audit_remediation.html#autotoc_md170", [
        [ "Orientation — start here", "plan_audit_remediation.html#autotoc_md172", null ],
        [ "Phase 0 — Documentation truth-up (S)", "plan_audit_remediation.html#autotoc_md174", null ],
        [ "Phase 1 — Risk-removal batch (S, one sitting)", "plan_audit_remediation.html#autotoc_md176", null ],
        [ "Phase 2 — Item handler reentrancy (H4) (S–M)", "plan_audit_remediation.html#autotoc_md178", null ],
        [ "Phase 3 — Logic/GC hot paths (M)", "plan_audit_remediation.html#autotoc_md180", null ],
        [ "Phase 4 — Hit pipeline restructure (H1 + H2) (M–L, the big one)", "plan_audit_remediation.html#autotoc_md182", null ],
        [ "Phase 5 — Rendering fill-rate program (M–L, in-editor)", "plan_audit_remediation.html#autotoc_md184", null ],
        [ "Phase 6 — Deferred / gated", "plan_audit_remediation.html#autotoc_md186", null ],
        [ "Suggested sequence", "plan_audit_remediation.html#autotoc_md188", null ]
      ] ]
    ] ],
    [ "URP Migration (conditional)", "plan_urp_migration.html", [
      [ "URP Migration (conditional)", "plan_urp_migration.html#autotoc_md583", [
        [ "Decision &amp; rationale", "plan_urp_migration.html#autotoc_md585", null ],
        [ "Prerequisites — do these first (they shrink the migration ~70%)", "plan_urp_migration.html#autotoc_md586", null ],
        [ "Verified inventory (2026-07-02)", "plan_urp_migration.html#autotoc_md587", null ],
        [ "Migration phases", "plan_urp_migration.html#autotoc_md588", null ],
        [ "Open questions (answer at migration time)", "plan_urp_migration.html#autotoc_md589", null ]
      ] ]
    ] ],
    [ "Future Ideas &amp; Improvements", "plan_future_ideas.html", [
      [ "Future Ideas &amp; Improvements", "plan_future_ideas.html#autotoc_md341", [
        [ "1 — VFX Improvements", "plan_future_ideas.html#autotoc_md343", [
          [ "1.1 Unbreakable Pop VFX — Falling Debris", "plan_future_ideas.html#autotoc_md344", null ],
          [ "1.2 Soap Cluster Pop VFX", "plan_future_ideas.html#autotoc_md345", null ],
          [ "1.3 Deflector Bounce VFX", "plan_future_ideas.html#autotoc_md346", null ],
          [ "1.4 Absorber Consume VFX", "plan_future_ideas.html#autotoc_md347", null ],
          [ "1.5 Gatekeeper Hit + Break VFX", "plan_future_ideas.html#autotoc_md348", null ]
        ] ],
        [ "2 — Spawn Weights, Pity System &amp; Streak Balancing", "plan_future_ideas.html#autotoc_md350", [
          [ "2.1 Per-Level Balloon &amp; Item Weights", "plan_future_ideas.html#autotoc_md351", null ],
          [ "2.2 Pity System for Weight Randoms", "plan_future_ideas.html#autotoc_md352", null ],
          [ "2.3 Color Streak Balancing", "plan_future_ideas.html#autotoc_md353", null ],
          [ "2.4 Full Grid Clear-Out Calculation", "plan_future_ideas.html#autotoc_md354", null ]
        ] ],
        [ "3 — Custom Level Editor", "plan_future_ideas.html#autotoc_md356", [
          [ "3.1 Level Sequence Model", "plan_future_ideas.html#autotoc_md357", null ],
          [ "3.2 <span class=\"tt\">LevelDefinition</span> ScriptableObject", "plan_future_ideas.html#autotoc_md358", null ],
          [ "3.3 Custom Level Editor Window (Unity Editor)", "plan_future_ideas.html#autotoc_md359", null ],
          [ "3.4 Level Sequence Integration", "plan_future_ideas.html#autotoc_md360", null ]
        ] ],
        [ "4 — Tutorial System &amp; Pacing", "plan_future_ideas.html#autotoc_md362", [
          [ "4.1 Tutorial Trigger System", "plan_future_ideas.html#autotoc_md363", null ],
          [ "4.2 Tutorial Sequence Definition", "plan_future_ideas.html#autotoc_md364", null ],
          [ "4.3 Pacing — Suggested Actor Introduction Order", "plan_future_ideas.html#autotoc_md365", null ],
          [ "4.4 Tutorial UI", "plan_future_ideas.html#autotoc_md366", null ],
          [ "4.5 Persistence", "plan_future_ideas.html#autotoc_md367", null ]
        ] ],
        [ "5 — Relocated Future Ideas", "plan_future_ideas.html#autotoc_md369", [
          [ "5.1 Soap Cluster Merge <em>(from PLAN-ContentProduction + PLAN-GridActorExpansion)</em>", "plan_future_ideas.html#autotoc_md370", null ],
          [ "5.2 Unbreakable Roam <em>(from PLAN-ContentProduction)</em>", "plan_future_ideas.html#autotoc_md371", null ],
          [ "5.3 New Balloon Archetypes <em>(from PLAN-GridActorExpansion)</em>", "plan_future_ideas.html#autotoc_md372", null ],
          [ "5.4 New Grid Actor Archetypes <em>(from PLAN-GridActorExpansion)</em>", "plan_future_ideas.html#autotoc_md373", null ],
          [ "5.5 <span class=\"tt\">IPassThrough</span> Behaviour Extensions <em>(from PLAN-GridActorExpansion)</em>", "plan_future_ideas.html#autotoc_md374", null ]
        ] ],
        [ "6 — Puff Cloud Polish &amp; Performance", "plan_future_ideas.html#autotoc_md376", [
          [ "6.1 Visual Polish", "plan_future_ideas.html#autotoc_md377", null ],
          [ "6.2 Performance &amp; Device Scaling", "plan_future_ideas.html#autotoc_md378", null ],
          [ "6.3 Edge Cases", "plan_future_ideas.html#autotoc_md379", null ]
        ] ],
        [ "7 — Vertical Cloud Drift", "plan_future_ideas.html#autotoc_md381", null ],
        [ "8 — Timed Release: Balloon Pass-Through Delay", "plan_future_ideas.html#autotoc_md383", [
          [ "8.1 Current Behaviour", "plan_future_ideas.html#autotoc_md384", null ],
          [ "8.2 Proposed Flow", "plan_future_ideas.html#autotoc_md385", null ],
          [ "8.3 Model Sketch", "plan_future_ideas.html#autotoc_md386", null ],
          [ "8.4 Balancer Changes", "plan_future_ideas.html#autotoc_md387", null ],
          [ "8.5 Visual Feedback", "plan_future_ideas.html#autotoc_md388", null ],
          [ "8.6 Relationship to Existing Pass-Through Ideas", "plan_future_ideas.html#autotoc_md389", null ],
          [ "8.7 Open Questions", "plan_future_ideas.html#autotoc_md390", null ]
        ] ],
        [ "Open Questions", "plan_future_ideas.html#autotoc_md392", null ],
        [ "9 — Quality Settings System", "plan_future_ideas.html#autotoc_md394", [
          [ "9.1 Architecture", "plan_future_ideas.html#autotoc_md395", null ],
          [ "9.2 Parameter Inventory", "plan_future_ideas.html#autotoc_md396", [
            [ "Shader Keywords (GPU cost)", "plan_future_ideas.html#autotoc_md397", null ],
            [ "Disturbance Field (GPU + memory)", "plan_future_ideas.html#autotoc_md398", null ],
            [ "Unbreakable Balloon GrabPass (GPU — single highest-cost operation)", "plan_future_ideas.html#autotoc_md399", null ],
            [ "Particle VFX (GPU fill rate + CPU simulation)", "plan_future_ideas.html#autotoc_md400", null ],
            [ "Projectile Trail (GPU fill rate)", "plan_future_ideas.html#autotoc_md401", null ],
            [ "Procedural Shader Octave Count", "plan_future_ideas.html#autotoc_md402", null ],
            [ "Animation Quality", "plan_future_ideas.html#autotoc_md403", null ],
            [ "Pool Sizes", "plan_future_ideas.html#autotoc_md404", null ]
          ] ],
          [ "9.3 Suggested Tier Thresholds", "plan_future_ideas.html#autotoc_md405", null ],
          [ "9.4 Implementation Priorities", "plan_future_ideas.html#autotoc_md406", null ],
          [ "9.5 Open Questions", "plan_future_ideas.html#autotoc_md407", null ]
        ] ],
        [ "10 — Baked Noise Texture for Puff Clouds", "plan_future_ideas.html#autotoc_md409", null ],
        [ "11 — Runtime Bush Baking at Preload", "plan_future_ideas.html#autotoc_md411", null ],
        [ "12 — Losing Conditions", "plan_future_ideas.html#autotoc_md413", [
          [ "12.1 Grid encroachment — the natural fit (Puzzle Bobble lineage)", "plan_future_ideas.html#autotoc_md414", null ],
          [ "12.2 Resource economy — make the existing Shield item matter", "plan_future_ideas.html#autotoc_md415", null ],
          [ "12.3 Clock / turn pressure — score-attack flavor", "plan_future_ideas.html#autotoc_md416", null ],
          [ "12.4 Lockout / soft-lock (safety net, not a headline mechanic)", "plan_future_ideas.html#autotoc_md417", null ],
          [ "12.5 How these compose", "plan_future_ideas.html#autotoc_md418", null ]
        ] ]
      ] ]
    ] ]
];