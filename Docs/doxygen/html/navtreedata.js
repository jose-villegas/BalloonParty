/*
 @licstart  The following is the entire license notice for the JavaScript code in this file.

 The MIT License (MIT)

 Copyright (C) 1997-2020 by Dimitri van Heesch

 Permission is hereby granted, free of charge, to any person obtaining a copy of this software
 and associated documentation files (the "Software"), to deal in the Software without restriction,
 including without limitation the rights to use, copy, modify, merge, publish, distribute,
 sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

 The above copyright notice and this permission notice shall be included in all copies or
 substantial portions of the Software.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
 BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
 DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

 @licend  The above is the entire license notice for the JavaScript code in this file
*/
var NAVTREE =
[
  [ "BalloonParty", "index.html", [
    [ "BalloonParty", "index.html#autotoc_md157", [
      [ "Documentation", "index.html#autotoc_md159", null ],
      [ "Architecture at a Glance", "index.html#autotoc_md161", null ],
      [ "Quick Links", "index.html#autotoc_md163", null ]
    ] ],
    [ "BalloonParty — Code Style Guide", "style_guide.html", [
      [ "BalloonParty — Code Style Guide", "style_guide.html#autotoc_md629", [
        [ "Architecture — MVC", "style_guide.html#autotoc_md631", null ],
        [ "Stack", "style_guide.html#autotoc_md633", null ],
        [ "Dependency Injection — VContainer", "style_guide.html#autotoc_md635", [
          [ "Scopes", "style_guide.html#autotoc_md636", null ],
          [ "Instantiating prefabs with child scopes", "style_guide.html#autotoc_md637", null ],
          [ "Eager creation for <span class=\"tt\">RegisterComponentOnNewGameObject</span>", "style_guide.html#autotoc_md638", null ],
          [ "Injection timing", "style_guide.html#autotoc_md639", null ]
        ] ],
        [ "Reactive Programming — UniRx", "style_guide.html#autotoc_md641", null ],
        [ "Messaging — MessagePipe", "style_guide.html#autotoc_md643", [
          [ "Message design rules", "style_guide.html#autotoc_md644", null ]
        ] ],
        [ "Async — UniTask", "style_guide.html#autotoc_md646", null ],
        [ "Navigation State", "style_guide.html#autotoc_md648", [
          [ "States", "style_guide.html#autotoc_md649", null ],
          [ "Scene preloading flow", "style_guide.html#autotoc_md650", null ],
          [ "Editor standalone play", "style_guide.html#autotoc_md651", null ]
        ] ],
        [ "Cinematic State", "style_guide.html#autotoc_md652", [
          [ "ICinematicAware", "style_guide.html#autotoc_md653", null ],
          [ "States", "style_guide.html#autotoc_md654", null ],
          [ "Pause Integration", "style_guide.html#autotoc_md655", null ]
        ] ],
        [ "Object Pooling", "style_guide.html#autotoc_md657", null ],
        [ "Configuration — Single Source of Truth", "style_guide.html#autotoc_md659", null ],
        [ "Animation", "style_guide.html#autotoc_md661", null ],
        [ "Unity Project Settings", "style_guide.html#autotoc_md663", null ],
        [ "Code Quality Constraints", "style_guide.html#autotoc_md665", [
          [ "Comments", "style_guide.html#autotoc_md666", null ],
          [ "Naming &amp; Readability", "style_guide.html#autotoc_md667", null ],
          [ "Visibility", "style_guide.html#autotoc_md668", null ],
          [ "Architecture &amp; Reuse", "style_guide.html#autotoc_md669", null ],
          [ "Formatting", "style_guide.html#autotoc_md670", null ]
        ] ],
        [ "Member Ordering", "style_guide.html#autotoc_md672", [
          [ "Fields &amp; Properties", "style_guide.html#autotoc_md673", null ],
          [ "Methods", "style_guide.html#autotoc_md674", null ],
          [ "Canonical Example", "style_guide.html#autotoc_md675", null ]
        ] ],
        [ "Model Interface Pattern — Read/Write Separation", "style_guide.html#autotoc_md677", null ],
        [ "UI Scope Architecture", "style_guide.html#autotoc_md679", [
          [ "Dynamically instantiated prefab scopes", "style_guide.html#autotoc_md680", null ]
        ] ],
        [ "Cheat Console", "style_guide.html#autotoc_md682", null ],
        [ "Gizmos &amp; Editor Drawing", "style_guide.html#autotoc_md684", [
          [ "Build-stripping rules", "style_guide.html#autotoc_md685", null ]
        ] ],
        [ "Living Documentation", "style_guide.html#autotoc_md687", null ],
        [ "Enforcement Tooling", "style_guide.html#autotoc_md689", [
          [ "Quick reference", "style_guide.html#autotoc_md690", null ]
        ] ]
      ] ]
    ] ],
    [ "BalloonParty — Architecture Diagrams", "architecture.html", "architecture" ],
    [ "Plans", "plans.html", "plans" ],
    [ "Bush Baking Tools", "editor_bush.html", [
      [ "Bush Baking Tools", "editor_bush.html#autotoc_md83", [
        [ "Contents", "editor_bush.html#autotoc_md84", null ],
        [ "Shaders (in <span class=\"tt\">Assets/Shaders/BalloonParty/Grid/Editor/</span>)", "editor_bush.html#autotoc_md85", null ],
        [ "Pipeline", "editor_bush.html#autotoc_md86", [
          [ "Branch Map + Variant Export", "editor_bush.html#autotoc_md87", null ],
          [ "Leaf Atlas", "editor_bush.html#autotoc_md88", null ]
        ] ],
        [ "Dependencies", "editor_bush.html#autotoc_md89", null ]
      ] ]
    ] ],
    [ "Refactor Audit — Helpers &amp; Extensions Candidates", "md__plans_2_r_e_f_a_c_t_o_r-_helpers-_extensions-_audit.html", [
      [ "1. Pool Fire-and-Forget — <span class=\"tt\">PoolManager</span> Extension", "md__plans_2_r_e_f_a_c_t_o_r-_helpers-_extensions-_audit.html#autotoc_md595", null ],
      [ "2. Color.WithAlpha — <span class=\"tt\">Color</span> Extension", "md__plans_2_r_e_f_a_c_t_o_r-_helpers-_extensions-_audit.html#autotoc_md597", null ],
      [ "3. Gradient-to-Texture Baking — Shared Helper", "md__plans_2_r_e_f_a_c_t_o_r-_helpers-_extensions-_audit.html#autotoc_md599", null ],
      [ "4. <span class=\"tt\">IBalloonModel</span> Source Color Extraction — Extension", "md__plans_2_r_e_f_a_c_t_o_r-_helpers-_extensions-_audit.html#autotoc_md601", null ],
      [ "5. <span class=\"tt\">GetProfile</span> + <span class=\"tt\">Stamp</span> Always Paired — Convenience Overload", "md__plans_2_r_e_f_a_c_t_o_r-_helpers-_extensions-_audit.html#autotoc_md603", null ],
      [ "6. <span class=\"tt\">EvaluateHit</span> + <span class=\"tt\">ActorHitMessage</span> Publish Combo", "md__plans_2_r_e_f_a_c_t_o_r-_helpers-_extensions-_audit.html#autotoc_md605", null ],
      [ "7. Squared Distance Check — <span class=\"tt\">MathUtils</span> Addition", "md__plans_2_r_e_f_a_c_t_o_r-_helpers-_extensions-_audit.html#autotoc_md607", null ],
      [ "8. Quad Mesh Creation — <span class=\"tt\">MeshHelper</span>", "md__plans_2_r_e_f_a_c_t_o_r-_helpers-_extensions-_audit.html#autotoc_md609", null ],
      [ "9. <span class=\"tt\">ContactFilter2D</span> + <span class=\"tt\">Physics2D</span> + <span class=\"tt\">GetComponentInParent&lt;BalloonView&gt;</span> — Physics Helper", "md__plans_2_r_e_f_a_c_t_o_r-_helpers-_extensions-_audit.html#autotoc_md611", null ],
      [ "10. <span class=\"tt\">DisturbanceFieldService</span> + <span class=\"tt\">IDisturbanceFieldSettings</span> Double-Inject", "md__plans_2_r_e_f_a_c_t_o_r-_helpers-_extensions-_audit.html#autotoc_md613", null ],
      [ "Summary — Priority Order", "md__plans_2_r_e_f_a_c_t_o_r-_helpers-_extensions-_audit.html#autotoc_md615", null ]
    ] ],
    [ "Packages", "namespaces.html", [
      [ "Package List", "namespaces.html", "namespaces_dup" ],
      [ "Package Members", "namespacemembers.html", [
        [ "All", "namespacemembers.html", null ],
        [ "Enumerations", "namespacemembers_enum.html", null ]
      ] ]
    ] ],
    [ "Classes", "annotated.html", [
      [ "Class List", "annotated.html", "annotated_dup" ],
      [ "Class Index", "classes.html", null ],
      [ "Class Hierarchy", "hierarchy.html", "hierarchy" ],
      [ "Class Members", "functions.html", [
        [ "All", "functions.html", "functions_dup" ],
        [ "Functions", "functions_func.html", "functions_func" ],
        [ "Variables", "functions_vars.html", "functions_vars" ],
        [ "Enumerations", "functions_enum.html", null ],
        [ "Properties", "functions_prop.html", "functions_prop" ]
      ] ]
    ] ],
    [ "Files", "files.html", [
      [ "File List", "files.html", "files_dup" ],
      [ "File Members", "globals.html", [
        [ "All", "globals.html", null ],
        [ "Typedefs", "globals_type.html", null ]
      ] ]
    ] ]
  ] ]
];

var NAVTREEINDEX =
[
"_absorber_actor_model_8cs.html",
"_nudge_override_resolver_8cs.html",
"class_balloon_party_1_1_balloon_1_1_controller_1_1_balloon_balancer.html#ae12709cc939469ea1d20186087dce775",
"class_balloon_party_1_1_balloon_1_1_model_1_1_unbreakable_balloon_model.html#a128f930790cc3c403f5a9e41dabb8e02",
"class_balloon_party_1_1_balloon_1_1_spawner_1_1_rejected_balloon_effect.html#afa9c95ec80101b4324a8692454836078",
"class_balloon_party_1_1_configuration_1_1_balloon_prefab_entry.html#a09968ee8fc6c7eefd7daa5af34f2fa5a",
"class_balloon_party_1_1_configuration_1_1_camera_rig_cinematic_settings.html#a0fc1580dedf2e038c3ac191d5daa6e31",
"class_balloon_party_1_1_configuration_1_1_editor_1_1_stamp_profile_drawer.html",
"class_balloon_party_1_1_configuration_1_1_overflow_settings.html",
"class_balloon_party_1_1_editor_1_1_bush_1_1_bush_baker_window.html#a88f8eeff9a42752560581e72e9987677",
"class_balloon_party_1_1_editor_1_1_bush_1_1_bush_leaf_baker.html#a8d9535e164be861698ac4e8050cc8248",
"class_balloon_party_1_1_editor_1_1_effect_preview_1_1_chain_lightning_preview_module.html#a6868ce6dc052d6a30aab56fa86129294",
"class_balloon_party_1_1_editor_1_1_property_drawer_helper.html#a3768c6e23d4929ee151043e8198c92d9",
"class_balloon_party_1_1_game_1_1_cinematics_1_1_camera_rig_cinematic.html#ad1cf334ba5ebd5b275f821edf069db41",
"class_balloon_party_1_1_game_1_1_game_lifetime_scope.html",
"class_balloon_party_1_1_game_1_1_score_1_1_score_trail_service.html#a18e7e16c7b9e4776fefd07822c6888c8",
"class_balloon_party_1_1_item_1_1_laser_item_rotation.html",
"class_balloon_party_1_1_item_1_1_shield_1_1_shield_item_handler.html#a34b97139ca49a9548433c3f4056442a9",
"class_balloon_party_1_1_projectile_1_1_view_1_1_projectile_trail.html",
"class_balloon_party_1_1_shared_1_1_disturbance_1_1_disturbance_field_service.html#a5d1cf98b9aa962219a6d13466a3323d0",
"class_balloon_party_1_1_shared_1_1_game_state_1_1_navigation_ready_gate.html#aeb3f6c5dcf39c83ec32474ee985b1763",
"class_balloon_party_1_1_shared_1_1_pool_1_1_pool_manager.html#a107a1515096b88f183d59edcd55fd22b",
"class_balloon_party_1_1_slots_1_1_actor_1_1_archetype_1_1_bush_cluster_registry.html#a26682eef3cb52cbcbbb99c1aeaa24128",
"class_balloon_party_1_1_slots_1_1_actor_1_1_archetype_1_1_bush_view.html#aa110504024f08c4034b3663b3a0ac268",
"class_balloon_party_1_1_slots_1_1_actor_1_1_archetype_1_1_puff_cluster_registry.html#a58742ebee7df75c1458bde66e329405b",
"class_balloon_party_1_1_slots_1_1_actor_1_1_grid_actor_hit_controller.html#afdbe057fb3b5160571ba2d98807ef453",
"class_balloon_party_1_1_thrower_1_1_thrower_lifetime_scope.html",
"class_balloon_party_1_1_u_i_1_1_level_up_1_1_level_up_pop_up.html#a6ca783f301a27f4b8cc285c824ac75a4",
"class_balloon_party_1_1_u_i_1_1_score_1_1_progress_notice.html#a53214c954913f5f69c9d6a8cdde54435",
"editor_bush.html#autotoc_md86",
"interface_balloon_party_1_1_configuration_1_1_i_cinematics_settings.html",
"interface_balloon_party_1_1_shared_1_1_i_game_configuration.html#a0169c592be6ebffbb8f7c3bfdae315de",
"namespace_balloon_party_1_1_nudge.html#a4d2e641b1da2f9eb813f7185abd74329a822b02e1b751097d290eaad55252eb81",
"plan_future_ideas.html#autotoc_md366",
"struct_balloon_party_1_1_editor_1_1_bush_1_1_bush_leaf_extractor_1_1_tip_candidate.html#aaf17f8b89e720a766c16403339df9c9c",
"struct_balloon_party_1_1_shared_1_1_messages_1_1_score_point_message.html#a7b73991b5ac4cde8227fe6927ac91050"
];

const SYNCONMSG = 'click to disable panel synchronization';
const SYNCOFFMSG = 'click to enable panel synchronization';
const LISTOFALLMEMBERS = 'List of all members';