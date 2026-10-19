using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class SetupEnemyAttack
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play mode first.");
        const string folder = "Assets/_Project/Art/Enemies/ArmedHumanoid/";
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Hero Knight - Pixel Art/Animations/HeroKnight_Attack1.anim");
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(source).Single();
        var frames = AnimationUtility.GetObjectReferenceCurve(source, binding);
        var prepare = MakeClip(folder, "AttackPrepare", binding, frames[0].value, frames[1].value, false, false);
        var strike = MakeClip(folder, "AttackStrike", binding, frames[2].value, frames[3].value, true, false);
        var recovery = MakeClip(folder, "AttackRecovery", binding, frames[4].value, frames[5].value, false, true);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(folder + "ArmedHumanoid.controller");
        AddParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "IsAttacking", AnimatorControllerParameterType.Bool);
        foreach (var name in new[] { "PrepareSpeed", "StrikeSpeed", "RecoverySpeed" }) AddParameter(controller, name, AnimatorControllerParameterType.Float);
        var machine = controller.layers[0].stateMachine;
        var idle = machine.states.Single(s => s.state.name == "Idle").state;
        var p = State(machine, "AttackPrepare", prepare, "PrepareSpeed", new Vector3(750, 0));
        var a = State(machine, "AttackStrike", strike, "StrikeSpeed", new Vector3(1000, 0));
        var r = State(machine, "AttackRecovery", recovery, "RecoverySpeed", new Vector3(1250, 0));
        foreach (var t in machine.anyStateTransitions.Where(t => t.destinationState == p).ToArray()) machine.RemoveAnyStateTransition(t);
        // Added after Death and Hurt: these interruption transitions retain priority.
        var begin = machine.AddAnyStateTransition(p);
        begin.hasExitTime = false; begin.duration = 0f; begin.canTransitionToSelf = false;
        begin.AddCondition(AnimatorConditionMode.If, 0, "Attack");
        begin.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");
        begin.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        Link(p, a); Link(a, r);
        var finish = r.AddTransition(idle); finish.hasExitTime = true; finish.exitTime = 1f; finish.duration = 0f;
        foreach (var state in new[] { p, a, r })
        {
            var cancel = state.AddTransition(idle); cancel.hasExitTime = false; cancel.duration = 0f;
            cancel.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
            cancel.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        }
        EditorUtility.SetDirty(controller);
        const string prefab = "Assets/_Project/Prefabs/ArmedHumanoid.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefab);
        try
        {
            if (root.GetComponent<EnemyCombat>() == null) root.AddComponent<EnemyCombat>();
            var visual = root.transform.Find("Visual").gameObject;
            if (visual.GetComponent<EnemyAnimationEvents>() == null) visual.AddComponent<EnemyAnimationEvents>();
            var view = new SerializedObject(root.GetComponent<EnemyAnimation>());
            view.FindProperty("attackPrepare").objectReferenceValue = prepare;
            view.FindProperty("attackStrike").objectReferenceValue = strike;
            view.FindProperty("attackRecovery").objectReferenceValue = recovery;
            view.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        return new { configured = true, phases = "Prepare 0.35 / Active 0.1 / Recovery 0.5", damage = 10 };
    }

    private static void AddParameter(AnimatorController c, string name, AnimatorControllerParameterType type)
    {
        if (!c.parameters.Any(p => p.name == name)) c.AddParameter(name, type);
    }

    private static AnimatorState State(AnimatorStateMachine machine, string name, AnimationClip clip, string speed, Vector3 position)
    {
        var state = machine.states.FirstOrDefault(s => s.state.name == name).state ?? machine.AddState(name, position);
        foreach (var t in state.transitions.ToArray()) state.RemoveTransition(t);
        state.motion = clip; state.speedParameterActive = true; state.speedParameter = speed;
        return state;
    }

    private static void Link(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to); t.hasExitTime = true; t.exitTime = 1f; t.duration = 0f;
        t.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");
    }

    private static AnimationClip MakeClip(string folder, string name, EditorCurveBinding binding, UnityEngine.Object first, UnityEngine.Object second, bool strike, bool recovery)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + name + ".anim");
        if (clip == null) { clip = new AnimationClip { name = name, frameRate = 60 }; AssetDatabase.CreateAsset(clip, folder + name + ".anim"); }
        AnimationUtility.SetObjectReferenceCurve(clip, binding, new[]
        {
            new ObjectReferenceKeyframe { time = 0f, value = first },
            new ObjectReferenceKeyframe { time = 0.1f, value = second },
            new ObjectReferenceKeyframe { time = 0.2f, value = second }
        });
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AnimationUtility.SetAnimationEvents(clip, strike ? new[]
        {
            new AnimationEvent { time = 0f, functionName = "BeginHitbox" },
            new AnimationEvent { time = 0.2f, functionName = "EndHitbox" }
        } : recovery ? new[] { new AnimationEvent { time = 0.2f, functionName = "EndAttack" } } : Array.Empty<AnimationEvent>());
        EditorUtility.SetDirty(clip);
        return clip;
    }
}
