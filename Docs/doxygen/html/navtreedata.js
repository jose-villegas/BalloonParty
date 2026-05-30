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
    [ "BalloonParty", "index.html#autotoc_md113", [
      [ "Documentation", "index.html#autotoc_md115", null ],
      [ "Architecture at a Glance", "index.html#autotoc_md117", null ],
      [ "Quick Links", "index.html#autotoc_md119", null ]
    ] ],
    [ "BalloonParty — Code Style Guide", "style_guide.html", [
      [ "BalloonParty — Code Style Guide", "style_guide.html#autotoc_md270", [
        [ "Architecture — MVC", "style_guide.html#autotoc_md272", null ],
        [ "Stack", "style_guide.html#autotoc_md274", null ],
        [ "Dependency Injection — VContainer", "style_guide.html#autotoc_md276", [
          [ "Scopes", "style_guide.html#autotoc_md277", null ],
          [ "Instantiating prefabs with child scopes", "style_guide.html#autotoc_md278", null ],
          [ "Eager creation for <span class=\"tt\">RegisterComponentOnNewGameObject</span>", "style_guide.html#autotoc_md279", null ],
          [ "Injection timing", "style_guide.html#autotoc_md280", null ]
        ] ],
        [ "Reactive Programming — UniRx", "style_guide.html#autotoc_md282", null ],
        [ "Messaging — MessagePipe", "style_guide.html#autotoc_md284", [
          [ "Message design rules", "style_guide.html#autotoc_md285", null ]
        ] ],
        [ "Async — UniTask", "style_guide.html#autotoc_md287", null ],
        [ "Navigation State", "style_guide.html#autotoc_md289", [
          [ "States", "style_guide.html#autotoc_md290", null ],
          [ "Scene preloading flow", "style_guide.html#autotoc_md291", null ],
          [ "Editor standalone play", "style_guide.html#autotoc_md292", null ]
        ] ],
        [ "Cinematic State", "style_guide.html#autotoc_md293", [
          [ "ICinematicAware", "style_guide.html#autotoc_md294", null ],
          [ "States", "style_guide.html#autotoc_md295", null ],
          [ "Pause Integration", "style_guide.html#autotoc_md296", null ]
        ] ],
        [ "Object Pooling", "style_guide.html#autotoc_md298", null ],
        [ "Configuration — Single Source of Truth", "style_guide.html#autotoc_md300", null ],
        [ "Animation", "style_guide.html#autotoc_md302", null ],
        [ "Unity Project Settings", "style_guide.html#autotoc_md304", null ],
        [ "Code Quality Constraints", "style_guide.html#autotoc_md306", [
          [ "Comments", "style_guide.html#autotoc_md307", null ],
          [ "Naming &amp; Readability", "style_guide.html#autotoc_md308", null ],
          [ "Visibility", "style_guide.html#autotoc_md309", null ],
          [ "Architecture &amp; Reuse", "style_guide.html#autotoc_md310", null ],
          [ "Formatting", "style_guide.html#autotoc_md311", null ]
        ] ],
        [ "Member Ordering", "style_guide.html#autotoc_md313", [
          [ "Fields &amp; Properties", "style_guide.html#autotoc_md314", null ],
          [ "Methods", "style_guide.html#autotoc_md315", null ],
          [ "Canonical Example", "style_guide.html#autotoc_md316", null ]
        ] ],
        [ "Model Interface Pattern — Read/Write Separation", "style_guide.html#autotoc_md318", null ],
        [ "UI Scope Architecture", "style_guide.html#autotoc_md320", [
          [ "Dynamically instantiated prefab scopes", "style_guide.html#autotoc_md321", null ]
        ] ],
        [ "Cheat Console", "style_guide.html#autotoc_md323", null ],
        [ "Gizmos &amp; Editor Drawing", "style_guide.html#autotoc_md325", [
          [ "Build-stripping rules", "style_guide.html#autotoc_md326", null ]
        ] ],
        [ "Living Documentation", "style_guide.html#autotoc_md328", null ],
        [ "Enforcement Tooling", "style_guide.html#autotoc_md330", [
          [ "Quick reference", "style_guide.html#autotoc_md331", null ]
        ] ]
      ] ]
    ] ],
    [ "BalloonParty — Architecture Diagrams", "architecture.html", [
      [ "BalloonParty — Architecture Diagrams", "architecture.html#autotoc_md0", [
        [ "MVC Pattern", "architecture.html#autotoc_md2", null ],
        [ "Scope Hierarchy", "architecture.html#autotoc_md4", null ],
        [ "Turn Pipeline", "architecture.html#autotoc_md6", null ],
        [ "Balance Flow", "architecture.html#autotoc_md8", null ],
        [ "System Map", "architecture.html#autotoc_md10", null ],
        [ "Message Flow", "architecture.html#autotoc_md12", null ],
        [ "Score &amp; Cinematic Pipeline", "architecture.html#autotoc_md14", null ],
        [ "Trail Utility Composition", "architecture.html#autotoc_md16", null ],
        [ "Item Activation", "architecture.html#autotoc_md18", null ],
        [ "Static State", "architecture.html#autotoc_md20", null ],
        [ "Slot Actor Abstraction", "architecture.html#autotoc_md21", null ],
        [ "Folder → Namespace Mapping", "architecture.html#autotoc_md23", null ]
      ] ]
    ] ],
    [ "Plans", "plans.html", "plans" ],
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
"architecture.html#autotoc_md0",
"class_balloon_party_1_1_balloon_1_1_spawner_1_1_balloon_pool_channel.html",
"class_balloon_party_1_1_balloon_1_1_view_1_1_balloon_view.html#a580b2f1854bc10938a07bc378e5f8339",
"class_balloon_party_1_1_configuration_1_1_editor_1_1_map_limits_scene_overlay.html#aae262f3763981f1d264c05e3b46f5b0e",
"class_balloon_party_1_1_configuration_1_1_item_settings.html#ab813cca16551e9aa7984e96536bfe0b5",
"class_balloon_party_1_1_editor_1_1_effect_preview_1_1_chain_lightning_preview_module.html#afb7b4e23fbd68ac99814ee8b704de5dd",
"class_balloon_party_1_1_editor_1_1_script_search_popup.html#a94ae8f5624a931d1fe9e526fd20760f6",
"class_balloon_party_1_1_game_1_1_game_lifetime_scope.html#a28f0caf50774cff0bf0e4c5b941600ef",
"class_balloon_party_1_1_item_1_1_item_display_service.html#acf3e341f81fc0e9f3885336f74450d2d",
"class_balloon_party_1_1_item_1_1_paint_1_1_paint_splash_view.html#a444381861f6108b42d08eda33090e312",
"class_balloon_party_1_1_projectile_1_1_view_1_1_projectile_shield_view.html#a2094cf86bf2d54d8d1bed52dedffd59f",
"class_balloon_party_1_1_shared_1_1_game_state_1_1_cinematic.html#a3b8e1de661b01fce33a046932924a76b",
"class_balloon_party_1_1_shared_1_1_pool_1_1_particle_pool_channel.html#ad37663d2161666a4663a215a7d4f0646",
"class_balloon_party_1_1_slots_1_1_actor_1_1_archetype_1_1_grid_actor_pool_channel.html#a93e8c5d389ce866d7a1b874cb1ca7e49",
"class_balloon_party_1_1_thrower_1_1_thrower_controller.html#a30735ba0f34d91dba7302e7526567f75",
"class_balloon_party_1_1_u_i_1_1_score_1_1_flying_trail.html#aa9045a1b0760e035f6a93aa3b9258403",
"class_balloon_party_1_1_u_i_1_1_shields_1_1_shield_trail_pool_channel.html#af9947fb47bbcf9ff54006a64d90c2131",
"interface_balloon_party_1_1_nudge_1_1_i_nudgeable.html#ad3b74c05344a7f8ba091c13e19816d3f",
"namespace_balloon_party_1_1_shared_1_1_pause.html#ab006bd98ba69f3aaa2ba396e176136b5a8fce77fd86da3af309c0ad415d8d7952",
"struct_balloon_party_1_1_shared_1_1_messages_1_1_item_check_message.html#aca0dda3ecfd93984e0987d566142771d"
];

const SYNCONMSG = 'click to disable panel synchronization';
const SYNCOFFMSG = 'click to enable panel synchronization';
const LISTOFALLMEMBERS = 'List of all members';