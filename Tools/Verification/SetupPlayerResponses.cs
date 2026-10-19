using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;
using UnityEditor.Animations;

public static class SetupPlayerResponses
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play mode first.");
        const string inputPath = "Assets/InputSystem_Actions.inputactions";
        var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputPath);
        var map = input.FindActionMap("Player", true);
        var parryAction = map.FindAction("Parry") ?? map.AddAction("Parry", InputActionType.Button);
        foreach (var path in new[] { "<Keyboard>/f", "<Mouse>/rightButton", "<Gamepad>/leftTrigger" })
            if (!parryAction.bindings.Any(b => b.path == path)) parryAction.AddBinding(path, groups: path.Contains("Gamepad") ? "Gamepad" : "Keyboard&Mouse");
        System.IO.File.WriteAllText(inputPath, input.ToJson());
        AssetDatabase.ImportAsset(inputPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Warrior free set/Aniamtion/Warrior.controller");
        Add(controller, "isParrying", AnimatorControllerParameterType.Bool);
        Add(controller, "ParrySuccess", AnimatorControllerParameterType.Trigger);
        var machine = controller.layers[0].stateMachine;
        var idle = machine.states.Single(s => s.state.name == "Idle").state;
        var attempt = machine.states.FirstOrDefault(s => s.state.name == "Parry").state ?? machine.AddState("Parry", new Vector3(550, 500));
        var success = machine.states.FirstOrDefault(s => s.state.name == "ParrySuccess").state ?? machine.AddState("ParrySuccess", new Vector3(800, 500));
        attempt.motion = PoseClip("Parry", false);
        success.motion = PoseClip("ParrySuccess", true);
        foreach (var t in attempt.transitions.ToArray()) attempt.RemoveTransition(t);
        foreach (var t in success.transitions.ToArray()) success.RemoveTransition(t);
        foreach (var t in machine.anyStateTransitions.Where(t => t.destinationState == attempt || t.destinationState == success).ToArray()) machine.RemoveAnyStateTransition(t);
        var enterSuccess = machine.AddAnyStateTransition(success);
        enterSuccess.hasExitTime = false; enterSuccess.duration = 0f; enterSuccess.canTransitionToSelf = false;
        enterSuccess.AddCondition(AnimatorConditionMode.If, 0, "ParrySuccess");
        var enter = machine.AddAnyStateTransition(attempt);
        enter.hasExitTime = false; enter.duration = 0f; enter.canTransitionToSelf = false;
        enter.AddCondition(AnimatorConditionMode.If, 0, "isParrying");
        var exit = attempt.AddTransition(idle);
        exit.hasExitTime = false; exit.duration = 0f;
        exit.AddCondition(AnimatorConditionMode.IfNot, 0, "isParrying");
        var recover = success.AddTransition(idle);
        recover.hasExitTime = true; recover.exitTime = 1; recover.duration = 0f;
        // Ground locomotion/attack transitions cannot steal the parry pose.
        foreach (var t in machine.anyStateTransitions.Where(t => t != enter && t != enterSuccess))
            if (!t.conditions.Any(c => c.parameter == "isParrying")) t.AddCondition(AnimatorConditionMode.IfNot, 0, "isParrying");
        EditorUtility.SetDirty(controller);
        const string prefab = "Assets/_Project/Prefabs/Player.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefab);
        try
        {
            if (root.GetComponent<PlayerParry>() == null) root.AddComponent<PlayerParry>();
            PrefabUtility.SaveAsPrefabAsset(root, prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        return new { configured = true, bindings = "F / Right mouse / Gamepad left trigger", window = 0.12f, failedRecovery = 0.18f, recoilBuffer = 0.1f };
    }

    private static void Add(AnimatorController c, string name, AnimatorControllerParameterType type)
    {
        if (!c.parameters.Any(p => p.name == name)) c.AddParameter(name, type);
    }

    private static AnimationClip PoseClip(string name, bool success)
    {
        const string folder = "Assets/_Project/Art/Player/";
        System.IO.Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Warrior free set/Aniamtion/Attack1.anim");
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(source).Single();
        var frames = AnimationUtility.GetObjectReferenceCurve(source, binding);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + name + ".anim");
        if (clip == null) { clip = new AnimationClip { name = name, frameRate = 60 }; AssetDatabase.CreateAsset(clip, folder + name + ".anim"); }
        AnimationUtility.SetObjectReferenceCurve(clip, binding, new[]
        {
            new ObjectReferenceKeyframe { time = 0f, value = frames[success ? 1 : 0].value },
            new ObjectReferenceKeyframe { time = 0.10f, value = frames[0].value }
        });
        AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = !success;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }
}
