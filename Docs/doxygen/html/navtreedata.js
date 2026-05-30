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
        [ "Functions", "functions_func.html", null ],
        [ "Variables", "functions_vars.html", null ],
        [ "Properties", "functions_prop.html", null ]
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
"class_balloon_party_1_1_configuration_1_1_grid_actor_prefab_entry.html#acd4a5c2ed8df1f2f5d8b2cbf1f4e3b95",
"class_balloon_party_1_1_nudge_1_1_nudge_override.html#af3d539fd160975235c6050683295b043",
"class_balloon_party_1_1_u_i_1_1_score_1_1_score_counter_label.html",
"interface_balloon_party_1_1_shared_1_1_pool_1_1_i_poolable.html",
"plan_grid_actor_expansion.html#autotoc_md230"
];

const SYNCONMSG = 'click to disable panel synchronization';
const SYNCOFFMSG = 'click to enable panel synchronization';
const LISTOFALLMEMBERS = 'List of all members';