var plans =
[
    [ "Plans", "plans.html#autotoc_md254", null ],
    [ "Bubble Cluster Hit Feedback", "plan_bubble_cluster_hit_feedback.html", [
      [ "Bubble Cluster Hit Feedback", "plan_bubble_cluster_hit_feedback.html#autotoc_md126", [
        [ "Goals", "plan_bubble_cluster_hit_feedback.html#autotoc_md128", null ],
        [ "Root cause of the existing laser rotation bug", "plan_bubble_cluster_hit_feedback.html#autotoc_md130", null ],
        [ "New interface — <span class=\"tt\">IBalloonHitHandler</span>", "plan_bubble_cluster_hit_feedback.html#autotoc_md132", null ],
        [ "Discovery and caching", "plan_bubble_cluster_hit_feedback.html#autotoc_md134", [
          [ "Root components — cache at <span class=\"tt\">Bind()</span> time", "plan_bubble_cluster_hit_feedback.html#autotoc_md135", null ],
          [ "Item visual components — register via <span class=\"tt\">ItemDisplayService</span>", "plan_bubble_cluster_hit_feedback.html#autotoc_md136", null ],
          [ "What goes away", "plan_bubble_cluster_hit_feedback.html#autotoc_md137", null ]
        ] ],
        [ "<span class=\"tt\">BalloonView</span> changes", "plan_bubble_cluster_hit_feedback.html#autotoc_md139", [
          [ "New methods", "plan_bubble_cluster_hit_feedback.html#autotoc_md140", null ],
          [ "Removed", "plan_bubble_cluster_hit_feedback.html#autotoc_md141", null ],
          [ "Internal", "plan_bubble_cluster_hit_feedback.html#autotoc_md142", null ]
        ] ],
        [ "<span class=\"tt\">BalloonController</span> changes", "plan_bubble_cluster_hit_feedback.html#autotoc_md144", null ],
        [ "<span class=\"tt\">SoapBubbleClusterVariant</span> — implements <span class=\"tt\">IBalloonHitHandler</span>", "plan_bubble_cluster_hit_feedback.html#autotoc_md146", [
          [ "C# mirror of shader bubble layouts", "plan_bubble_cluster_hit_feedback.html#autotoc_md147", null ],
          [ "<span class=\"tt\">GetVfxWorldPosition</span>", "plan_bubble_cluster_hit_feedback.html#autotoc_md148", null ],
          [ "<span class=\"tt\">OnHit</span> — spin impulse", "plan_bubble_cluster_hit_feedback.html#autotoc_md149", null ],
          [ "<span class=\"tt\">OnPrePop</span>", "plan_bubble_cluster_hit_feedback.html#autotoc_md150", null ]
        ] ],
        [ "<span class=\"tt\">LaserItemRotation</span> — migrates to <span class=\"tt\">IBalloonHitHandler</span>", "plan_bubble_cluster_hit_feedback.html#autotoc_md152", null ],
        [ "File summary", "plan_bubble_cluster_hit_feedback.html#autotoc_md154", null ],
        [ "Open questions", "plan_bubble_cluster_hit_feedback.html#autotoc_md156", null ]
      ] ]
    ] ],
    [ "Grid Actor Expansion — Phase 8+", "plan_grid_actor_expansion.html", [
      [ "Grid Actor Expansion — Phase 8+", "plan_grid_actor_expansion.html#autotoc_md188", [
        [ "Orientation", "plan_grid_actor_expansion.html#autotoc_md190", null ],
        [ "Actor Vocabulary — Design Reference", "plan_grid_actor_expansion.html#autotoc_md192", [
          [ "Balloon archetypes", "plan_grid_actor_expansion.html#autotoc_md193", null ],
          [ "Grid actor archetypes", "plan_grid_actor_expansion.html#autotoc_md194", null ],
          [ "Hit controller pattern for non-balloon actors", "plan_grid_actor_expansion.html#autotoc_md195", null ]
        ] ],
        [ "Phases", "plan_grid_actor_expansion.html#autotoc_md197", [
          [ "✅ Phase 8.0 — Spawner Coordination", "plan_grid_actor_expansion.html#autotoc_md199", [
            [ "What was built", "plan_grid_actor_expansion.html#autotoc_md200", null ]
          ] ],
          [ "✅ Phase 8.1a — Absorb Routing", "plan_grid_actor_expansion.html#autotoc_md202", [
            [ "What was built", "plan_grid_actor_expansion.html#autotoc_md203", null ]
          ] ],
          [ "✅ Phase 8.1b — DamageContext API Migration", "plan_grid_actor_expansion.html#autotoc_md205", [
            [ "What was built", "plan_grid_actor_expansion.html#autotoc_md206", null ]
          ] ],
          [ "Phase 8.1c — UnbreakableBalloonModel + BalloonModelBase Cleanup", "plan_grid_actor_expansion.html#autotoc_md208", [
            [ "<span class=\"tt\">UnbreakableBalloonModel</span>", "plan_grid_actor_expansion.html#autotoc_md209", null ],
            [ "<span class=\"tt\">ScoreValue</span> on <span class=\"tt\">BalloonModelBase</span>", "plan_grid_actor_expansion.html#autotoc_md210", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md211", null ]
          ] ],
          [ "Phase 8.2a — Structural Actors (Puff + Bush)", "plan_grid_actor_expansion.html#autotoc_md213", [
            [ "Folder structure", "plan_grid_actor_expansion.html#autotoc_md214", null ],
            [ "Files", "plan_grid_actor_expansion.html#autotoc_md215", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md216", null ]
          ] ],
          [ "Phase 8.2b — Indestructible Hitable Actors (Deflector + Absorber)", "plan_grid_actor_expansion.html#autotoc_md218", [
            [ "Files", "plan_grid_actor_expansion.html#autotoc_md219", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md220", null ]
          ] ],
          [ "Phase 8.2c — Gatekeeper + GridActorHitController", "plan_grid_actor_expansion.html#autotoc_md222", [
            [ "Files", "plan_grid_actor_expansion.html#autotoc_md223", null ],
            [ "<span class=\"tt\">GatekeeperActorModel</span>", "plan_grid_actor_expansion.html#autotoc_md224", null ],
            [ "<span class=\"tt\">GridActorHitController</span>", "plan_grid_actor_expansion.html#autotoc_md225", null ],
            [ "<span class=\"tt\">NudgeOverrides</span> cleanup on <span class=\"tt\">BalloonModelBase</span>", "plan_grid_actor_expansion.html#autotoc_md226", null ],
            [ "NudgeService decoupling (done alongside 8.2c)", "plan_grid_actor_expansion.html#autotoc_md227", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md228", null ]
          ] ],
          [ "Phase 8.3 — Procedural Placement Engine", "plan_grid_actor_expansion.html#autotoc_md230", [
            [ "Design", "plan_grid_actor_expansion.html#autotoc_md231", null ],
            [ "Migration path", "plan_grid_actor_expansion.html#autotoc_md232", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md233", null ]
          ] ],
          [ "Phase 8.4 — Difficulty + Level Coupling", "plan_grid_actor_expansion.html#autotoc_md235", [
            [ "Design", "plan_grid_actor_expansion.html#autotoc_md236", null ],
            [ "Failing tests", "plan_grid_actor_expansion.html#autotoc_md237", null ]
          ] ]
        ] ],
        [ "Open Questions", "plan_grid_actor_expansion.html#autotoc_md239", null ],
        [ "Current State", "plan_grid_actor_expansion.html#autotoc_md241", null ]
      ] ]
    ] ],
    [ "Grid Actor System — Foundation Reference (Phases 1–7.5)", "plan_grid_actor_foundation.html", [
      [ "Grid Actor System — Foundation Reference (Phases 1–7.5)", "plan_grid_actor_foundation.html#autotoc_md242", [
        [ "Key Design Decisions — Summary", "plan_grid_actor_foundation.html#autotoc_md244", [
          [ "Mobility axis: <span class=\"tt\">SlotActorKind</span>", "plan_grid_actor_foundation.html#autotoc_md245", null ],
          [ "Durability axis: <span class=\"tt\">IHitable</span> + <span class=\"tt\">IHasDurability</span>", "plan_grid_actor_foundation.html#autotoc_md246", null ],
          [ "Item slot axis: <span class=\"tt\">IHasItemSlot</span>", "plan_grid_actor_foundation.html#autotoc_md247", null ],
          [ "Spawner type selection", "plan_grid_actor_foundation.html#autotoc_md248", null ],
          [ "Capability interfaces in <span class=\"tt\">Slots/</span>", "plan_grid_actor_foundation.html#autotoc_md249", null ]
        ] ],
        [ "Phase Status", "plan_grid_actor_foundation.html#autotoc_md251", [
          [ "Phase 7 — key changes", "plan_grid_actor_foundation.html#autotoc_md252", null ],
          [ "Phase 7.5 — key changes", "plan_grid_actor_foundation.html#autotoc_md253", null ]
        ] ]
      ] ]
    ] ],
    [ "Content Production Plan — Pre-8.3 Assets", "plan_content_production.html", [
      [ "Content Production Plan — Pre-8.3 Assets", "plan_content_production.html#autotoc_md157", [
        [ "Context", "plan_content_production.html#autotoc_md159", null ],
        [ "Asset status overview", "plan_content_production.html#autotoc_md161", null ],
        [ "Shared infrastructure needed", "plan_content_production.html#autotoc_md163", [
          [ "<span class=\"tt\">IHasScoreColor</span> — score attribution ✅ Complete", "plan_content_production.html#autotoc_md164", null ],
          [ "<span class=\"tt\">GridActorConfiguration</span> ScriptableObject", "plan_content_production.html#autotoc_md166", null ],
          [ "<span class=\"tt\">GridActorView</span> prefab root pattern", "plan_content_production.html#autotoc_md167", null ]
        ] ],
        [ "Per-actor detail", "plan_content_production.html#autotoc_md169", [
          [ "Soap Cluster ✅ Done", "plan_content_production.html#autotoc_md171", null ],
          [ "Puff", "plan_content_production.html#autotoc_md173", null ],
          [ "Bush", "plan_content_production.html#autotoc_md175", null ],
          [ "Deflector", "plan_content_production.html#autotoc_md177", null ],
          [ "Absorber", "plan_content_production.html#autotoc_md179", null ],
          [ "Gatekeeper", "plan_content_production.html#autotoc_md181", null ]
        ] ],
        [ "Suggested production order", "plan_content_production.html#autotoc_md183", null ],
        [ "Asset folder conventions", "plan_content_production.html#autotoc_md185", null ],
        [ "Open questions for art direction", "plan_content_production.html#autotoc_md187", null ]
      ] ]
    ] ]
];