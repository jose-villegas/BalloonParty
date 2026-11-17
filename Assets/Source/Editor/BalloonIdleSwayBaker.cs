using System.Collections.Generic;
using BalloonParty.Configuration.Balloons;
using BalloonParty.EditorUI.Utilities;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BalloonParty.Editor
{
    /// <summary>
    ///     Bakes the plain balloon's idle sway out of its Animator controller into the Idle Sway block of
    ///     <see cref="BalloonsConfiguration" />, which <c>BalloonSwayTicker</c> plays for every balloon in place of
    ///     an Animator each. The controller and its clips stay where the sway is authored; this copies their
    ///     curves and crossfade times, nothing typed by hand.
    ///     <para>
    ///         A curve is baked as the Animator plays it, which isn't always as it was drawn: see
    ///         <see cref="AsPlayed" />.
    ///     </para>
    ///     <para>
    ///         The ticker reproduces one shape of controller only: an empty default state, a stable and an
    ///         unstable looping clip that each turn one child about Z, switched by the <c>IsStable</c> bool over
    ///         fixed-time crossfades that can't be interrupted. <see cref="Read" /> refuses anything else and says
    ///         why, so a controller that grows past that shape can't be baked into a sway that no longer matches
    ///         it.
    ///     </para>
    /// </summary>
    internal static class BalloonIdleSwayBaker
    {
        private const string MenuPath = "Tools/BalloonParty/Balloons/Bake Idle Sway";
        private const string StableParameter = "IsStable";
        private const string EulerProperty = "localEulerAnglesRaw.";
        private const string SwayAxis = "z";
        private const float OnFrameTolerance = 1e-3f;

        private static readonly EditorAssetCache<BalloonsConfiguration> ConfigCache = new();

        /// <summary>
        ///     Reads <paramref name="controller" /> as an idle sway. Returns null, with the reasons in
        ///     <paramref name="problems" />, if it does anything the ticker doesn't reproduce.
        /// </summary>
        internal static BalloonIdleSwaySettings Read(AnimatorController controller, List<string> problems)
        {
            var before = problems.Count;
            var machine = SingleMachine(controller, problems);
            if (machine == null)
            {
                return null;
            }

            var rest = machine.defaultState;
            CheckState(rest, problems);
            if (rest.motion != null)
            {
                problems.Add($"The default state '{rest.name}' plays a motion; the sway starts from an empty state.");
            }

            var enterStable = Transition(rest, true, problems);
            var enterUnstable = Transition(rest, false, problems);
            if (enterStable?.destinationState == null || enterUnstable?.destinationState == null
                || rest.transitions.Length != 2)
            {
                problems.Add($"'{rest.name}' needs exactly two transitions: one for {StableParameter} on, one off.");
                return null;
            }

            var stableState = enterStable.destinationState;
            var unstableState = enterUnstable.destinationState;
            var toUnstable = Transition(stableState, false, problems);
            var toStable = Transition(unstableState, true, problems);
            if (toUnstable == null || toStable == null
                || stableState.transitions.Length != 1 || unstableState.transitions.Length != 1
                || toUnstable.destinationState != unstableState || toStable.destinationState != stableState)
            {
                problems.Add("The stable and unstable states need exactly one transition each, to the other.");
                return null;
            }

            var stable = ReadLoop(stableState, enterStable, toStable, problems, out var stablePath);
            var unstable = ReadLoop(unstableState, enterUnstable, toUnstable, problems, out var unstablePath);
            if (stablePath != unstablePath)
            {
                problems.Add($"The two clips turn different children: '{stablePath}' and '{unstablePath}'.");
            }

            return problems.Count == before
                ? new BalloonIdleSwaySettings(controller, stablePath, stable, unstable)
                : null;
        }

        /// <summary>
        ///     <paramref name="authored" /> as the Animator plays it. A stretch of curve between two keys is
        ///     played as drawn unless either key's tangent on that side is weighted. A weighted stretch the
        ///     Animator plays as straight lines between the curve's values at the clip's frames, up to a hundredth
        ///     of a degree off the curve on this sway (read off a real Animator, which
        ///     <c>ShippedBalloonIdleSwayTests</c> compares the result with). Such a stretch is baked as a key a
        ///     frame.
        /// </summary>
        internal static AnimationCurve AsPlayed(AnimationCurve authored, AnimationClip clip, List<string> problems)
        {
            var keys = authored.keys;
            var points = new List<PlayedPoint>(keys.Length);
            var resampled = false;
            for (var i = 0; i + 1 < keys.Length; i++)
            {
                if (!IsWeighted(keys[i], keys[i + 1]))
                {
                    points.Add(new PlayedPoint(keys[i], false));
                    continue;
                }

                var startFrame = keys[i].time * clip.frameRate;
                var frames = (keys[i + 1].time - keys[i].time) * clip.frameRate;
                var wholeFrames = Mathf.RoundToInt(frames);
                if (wholeFrames < 1 || Mathf.Abs(frames - wholeFrames) > OnFrameTolerance
                    || Mathf.Abs(startFrame - Mathf.Round(startFrame)) > OnFrameTolerance)
                {
                    problems.Add(
                        $"'{clip.name}' has a weighted stretch from {keys[i].time} s whose keys are off the clip's " +
                        "frames; where the Animator's straight lines would fall isn't known.");
                    continue;
                }

                resampled = true;
                points.Add(new PlayedPoint(keys[i], true));
                for (var frame = 1; frame < wholeFrames; frame++)
                {
                    var time = keys[i].time + frame / clip.frameRate;
                    points.Add(new PlayedPoint(new Keyframe(time, authored.Evaluate(time)), true));
                }
            }

            if (!resampled)
            {
                return authored;
            }

            points.Add(new PlayedPoint(keys[^1], false));
            var played = new Keyframe[points.Count];
            for (var i = 0; i < played.Length; i++)
            {
                var key = points[i].Key;
                var inTangent = i > 0 && points[i - 1].StraightAfter ? Slope(points[i - 1].Key, key) : key.inTangent;
                var outTangent = points[i].StraightAfter ? Slope(key, points[i + 1].Key) : key.outTangent;
                played[i] = new Keyframe(key.time, key.value, inTangent, outTangent);
            }

            return new AnimationCurve(played)
            {
                preWrapMode = authored.preWrapMode,
                postWrapMode = authored.postWrapMode
            };
        }

        /// <summary>
        ///     Bakes <paramref name="controller" /> into <paramref name="config" />, left dirty for the caller to
        ///     save. False, and nothing written, if it can't be.
        /// </summary>
        internal static bool Bake(BalloonsConfiguration config, AnimatorController controller, List<string> problems)
        {
            var sway = Read(controller, problems);
            if (sway == null)
            {
                return false;
            }

            var serialized = new SerializedObject(config);
            var block = serialized.FindProperty("_idleSway");
            block.FindPropertyRelative("_source").objectReferenceValue = sway.Source;
            block.FindPropertyRelative("_pivotPath").stringValue = sway.PivotPath;
            WriteLoop(block.FindPropertyRelative("_stable"), (BalloonSwayLoop)sway.Stable);
            WriteLoop(block.FindPropertyRelative("_unstable"), (BalloonSwayLoop)sway.Unstable);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(config);
            return true;
        }

        [MenuItem(MenuPath)]
        private static void BakeFromMenu()
        {
            var config = ConfigCache.Value;
            if (config == null)
            {
                EditorUtility.DisplayDialog("Bake Idle Sway", "No BalloonsConfiguration asset found.", "OK");
                return;
            }

            // The first bake has no source on record yet: take the controller selected in the Project window.
            var controller = config.IdleSway.Source as AnimatorController
                             ?? Selection.activeObject as AnimatorController;
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Bake Idle Sway",
                    "The configuration has no source controller yet. Select the balloon's Animator controller in " +
                    "the Project window and run this again.", "OK");
                return;
            }

            var problems = new List<string>();
            if (Bake(config, controller, problems))
            {
                AssetDatabase.SaveAssetIfDirty(config);
                Debug.Log($"[BalloonIdleSwayBaker] Baked '{controller.name}' into '{config.name}'.", config);
                return;
            }

            Debug.LogError(
                $"[BalloonIdleSwayBaker] '{controller.name}' can't be played by BalloonSwayTicker; nothing was " +
                $"written:\n- {string.Join("\n- ", problems)}", controller);
        }

        private static AnimatorStateMachine SingleMachine(AnimatorController controller, List<string> problems)
        {
            if (controller.layers.Length != 1)
            {
                problems.Add($"{controller.layers.Length} layers; the sway is one.");
                return null;
            }

            var parameters = controller.parameters;
            if (parameters.Length != 1 || parameters[0].name != StableParameter
                || parameters[0].type != AnimatorControllerParameterType.Bool || parameters[0].defaultBool)
            {
                problems.Add($"The only parameter must be the bool '{StableParameter}', off by default.");
            }

            var machine = controller.layers[0].stateMachine;
            if (machine.stateMachines.Length != 0 || machine.anyStateTransitions.Length != 0
                || machine.entryTransitions.Length != 0 || machine.behaviours.Length != 0)
            {
                problems.Add("Sub-state machines, Any State or Entry transitions, or state machine behaviours.");
            }

            if (machine.states.Length != 3 || machine.defaultState == null)
            {
                problems.Add($"{machine.states.Length} states; the sway is a default rest state and two loops.");
                return null;
            }

            return machine;
        }

        // The state's one transition taken when IsStable is (or isn't) set, or null. Anything about it the
        // ticker wouldn't reproduce is a problem.
        private static AnimatorStateTransition Transition(AnimatorState state, bool whenStable, List<string> problems)
        {
            var wanted = whenStable ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
            foreach (var transition in state.transitions)
            {
                var conditions = transition.conditions;
                if (conditions.Length != 1 || conditions[0].parameter != StableParameter || conditions[0].mode != wanted)
                {
                    continue;
                }

                if (transition.hasExitTime || !transition.hasFixedDuration || transition.offset != 0f
                    || transition.interruptionSource != TransitionInterruptionSource.None
                    || transition.mute || transition.solo || transition.isExit
                    || transition.destinationState == null || transition.destinationStateMachine != null)
                {
                    problems.Add(
                        $"The transition out of '{state.name}' must go to a state over a fixed time, with no exit " +
                        "time, offset, interruption, mute or solo.");
                }

                return transition;
            }

            return null;
        }

        private static BalloonSwayLoop ReadLoop(
            AnimatorState state,
            AnimatorStateTransition enter,
            AnimatorStateTransition switchIn,
            List<string> problems,
            out string pivotPath)
        {
            pivotPath = null;
            CheckState(state, problems);
            if (state.motion is not AnimationClip clip)
            {
                problems.Add($"'{state.name}' doesn't play a clip.");
                return null;
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (clip.legacy || !settings.loopTime || settings.startTime != 0f || settings.cycleOffset != 0f
                || settings.mirror || settings.stopTime <= 0f || clip.events.Length != 0
                || AnimationUtility.GetObjectReferenceCurveBindings(clip).Length != 0)
            {
                problems.Add(
                    $"'{clip.name}' must be a plain loop from 0: no cycle offset, mirror, events or object curves.");
            }

            var angle = ReadAngle(clip, problems, out pivotPath);
            return angle == null
                ? null
                : new BalloonSwayLoop(
                    AsPlayed(angle, clip, problems), settings.stopTime, enter.duration, switchIn.duration);
        }

        // The clip's Z curve, required to be all it animates: Euler angles on one transform, X and Y flat at
        // zero, so the curve is the pivot's angle over time and nothing else.
        private static AnimationCurve ReadAngle(AnimationClip clip, List<string> problems, out string pivotPath)
        {
            pivotPath = null;
            AnimationCurve angle = null;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform) || !binding.propertyName.StartsWith(EulerProperty)
                    || (pivotPath != null && binding.path != pivotPath))
                {
                    problems.Add(
                        $"'{clip.name}' animates '{binding.path}' {binding.propertyName}; the sway is one " +
                        "transform's Euler rotation.");
                    continue;
                }

                pivotPath = binding.path;
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (binding.propertyName == EulerProperty + SwayAxis)
                {
                    angle = curve;
                }
                else if (!IsFlatZero(curve))
                {
                    problems.Add($"'{clip.name}' turns '{binding.path}' about {binding.propertyName}; the sway is Z only.");
                }
            }

            if (angle == null)
            {
                problems.Add($"'{clip.name}' has no Z rotation curve.");
            }

            return angle;
        }

        private static void CheckState(AnimatorState state, List<string> problems)
        {
            if (state.speed != 1f || state.speedParameterActive || state.cycleOffset != 0f
                || state.cycleOffsetParameterActive || state.timeParameterActive || state.mirror
                || state.mirrorParameterActive || !state.writeDefaultValues || state.behaviours.Length != 0)
            {
                problems.Add(
                    $"'{state.name}' must play at speed 1 with Write Defaults on: no cycle offset, mirror, " +
                    "parameter-driven speed or time, or behaviours.");
            }
        }

        private static bool IsWeighted(Keyframe from, Keyframe to)
        {
            return (from.weightedMode & WeightedMode.Out) != 0 || (to.weightedMode & WeightedMode.In) != 0;
        }

        private static float Slope(Keyframe from, Keyframe to)
        {
            return (to.value - from.value) / (to.time - from.time);
        }

        private static bool IsFlatZero(AnimationCurve curve)
        {
            foreach (var key in curve.keys)
            {
                if (key.value != 0f || key.inTangent != 0f || key.outTangent != 0f)
                {
                    return false;
                }
            }

            return true;
        }

        private static void WriteLoop(SerializedProperty property, BalloonSwayLoop loop)
        {
            property.FindPropertyRelative("_angle").animationCurveValue = loop.Angle;
            property.FindPropertyRelative("_seconds").floatValue = loop.Seconds;
            property.FindPropertyRelative("_enterSeconds").floatValue = loop.EnterSeconds;
            property.FindPropertyRelative("_switchSeconds").floatValue = loop.SwitchSeconds;
        }

        // A key of the played curve, and whether the Animator plays the stretch after it as a straight line.
        private readonly struct PlayedPoint
        {
            public readonly Keyframe Key;
            public readonly bool StraightAfter;

            public PlayedPoint(Keyframe key, bool straightAfter)
            {
                Key = key;
                StraightAfter = straightAfter;
            }
        }
    }
}
