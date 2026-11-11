using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxAnimationBuilder
    {
        private static readonly string[] States = { "Idle", "Walk", "Attack", "Death", "Cast" };

        [MenuItem("Azure Dreams/Rendering/Build Animation Controllers")]
        public static void BuildAnimationControllers()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string animDir = "Assets/AzureDreams/Animations";
            Directory.CreateDirectory(Path.Combine(projectRoot, animDir));

            // Create animation controller template
            var controller = AnimatorController.CreateAnimatorControllerAtPath(animDir + "/PsxActor.controller");
            var rootStateMachine = controller.layers[0].stateMachine;

            foreach (var stateName in States)
            {
                var state = rootStateMachine.AddState(stateName);
                // Create empty animation clip
                var clip = new AnimationClip { name = stateName };
                string clipPath = animDir + "/" + stateName + ".anim";
                AssetDatabase.CreateAsset(clip, clipPath);
                state.motion = clip;
            }

            // Add transitions: Idle -> Walk, Idle -> Attack, etc.
            var idleState = rootStateMachine.states[0].state;
            var walkState = rootStateMachine.states[1].state;
            var attackState = rootStateMachine.states[2].state;

            // Idle -> Walk
            var t1 = idleState.AddTransition(walkState);
            t1.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");
            t1.duration = 0;

            // Walk -> Idle
            var t2 = walkState.AddTransition(idleState);
            t2.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
            t2.duration = 0;

            // Idle -> Attack
            var t3 = idleState.AddTransition(attackState);
            t3.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            t3.duration = 0;

            // Attack -> Idle
            var t4 = attackState.AddTransition(idleState);
            t4.hasExitTime = true;
            t4.exitTime = 1f;
            t4.duration = 0;

            // Add parameters
            controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Cast", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

            AssetDatabase.SaveAssets();
            Debug.Log("[PsxAnimationBuilder] Animation controller created: " + animDir + "/PsxActor.controller");
            Debug.Log("States: " + string.Join(", ", States));
            Debug.Log("Note: Animation clips are empty placeholders. Populate with sprite frame data from verified_decode.");
        }
    }
}
