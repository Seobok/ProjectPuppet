using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class SetupEnemyGuard
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play mode first.");
        const string folder = "Assets/_Project/Art/Enemies/ArmedHumanoid/";
        var guardClip = Copy("HeroKnight_BlockIdle", "Guard", true);
        var parryClip = Copy("HeroKnight_BlockNoEffect", "Parry", false);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(folder + "ArmedHumanoid.controller");
        Add(controller, "IsGuarding", AnimatorControllerParameterType.Bool);
        Add(controller, "Parry", AnimatorControllerParameterType.Trigger);
        Add(controller, "ParrySpeed", AnimatorControllerParameterType.Float);
        var machine = controller.layers[0].stateMachine;
        var idle = machine.states.Single(s => s.state.name == "Idle").state;
        var guard = machine.states.FirstOrDefault(s => s.state.name == "Guard").state ?? machine.AddState("Guard", new Vector3(280, 400));
        var parry = machine.states.FirstOrDefault(s => s.state.name == "ParrySuccess").state ?? machine.AddState("ParrySuccess", new Vector3(520, 400));
        guard.motion = guardClip; parry.motion = parryClip;
        parry.speedParameterActive = true; parry.speedParameter = "ParrySpeed";
        foreach (var t in guard.transitions.ToArray()) guard.RemoveTransition(t);
        foreach (var t in parry.transitions.ToArray()) parry.RemoveTransition(t);
        foreach (var t in machine.anyStateTransitions.Where(t => t.destinationState == guard || t.destinationState == parry).ToArray()) machine.RemoveAnyStateTransition(t);
        var block = machine.AddAnyStateTransition(parry);
        block.hasExitTime = false; block.duration = 0f; block.canTransitionToSelf = false;
        block.AddCondition(AnimatorConditionMode.If, 0, "Parry");
        block.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        var enter = machine.AddAnyStateTransition(guard);
        enter.hasExitTime = false; enter.duration = 0f; enter.canTransitionToSelf = false;
        enter.AddCondition(AnimatorConditionMode.If, 0, "IsGuarding");
        enter.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        enter.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
        var exit = guard.AddTransition(idle);
        exit.hasExitTime = false; exit.duration = 0f;
        exit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGuarding");
        exit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        var recover = parry.AddTransition(idle);
        recover.hasExitTime = true; recover.exitTime = 1f; recover.duration = 0f;
        recover.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        EditorUtility.SetDirty(controller);
        const string path = "Assets/_Project/Prefabs/ArmedHumanoid.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<EnemyGuard>() == null) root.AddComponent<EnemyGuard>();
            var view = new SerializedObject(root.GetComponent<EnemyAnimation>());
            view.FindProperty("parryAnimation").objectReferenceValue = parryClip;
            view.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        return new { configured = true, guardSeconds = 0.5f, successSeconds = 0.25f, frontalOnly = true };
    }

    private static void Add(AnimatorController c, string name, AnimatorControllerParameterType type)
    {
        if (!c.parameters.Any(p => p.name == name)) c.AddParameter(name, type);
    }

    private static AnimationClip Copy(string source, string name, bool loop)
    {
        string path = "Assets/_Project/Art/Enemies/ArmedHumanoid/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            AssetDatabase.CopyAsset("Assets/Hero Knight - Pixel Art/Animations/" + source + ".anim", path);
            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        EditorUtility.SetDirty(clip);
        return clip;
    }
}
