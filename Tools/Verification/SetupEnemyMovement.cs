using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class SetupEnemyMovement
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit play mode first.");
        const string folder = "Assets/_Project/Art/Enemies/ArmedHumanoid/";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(folder + "ArmedHumanoid.controller");
        var machine = controller.layers[0].stateMachine;
        if (!controller.parameters.Any(p => p.name == "IsMoving")) controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        var runClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "Run.anim");
        if (runClip == null)
        {
            AssetDatabase.CopyAsset("Assets/Hero Knight - Pixel Art/Animations/HeroKnight_Run.anim", folder + "Run.anim");
            runClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "Run.anim");
        }
        var run = machine.states.FirstOrDefault(s => s.state.name == "Run").state ?? machine.AddState("Run", new Vector3(280, -70));
        run.motion = runClip;
        var idle = machine.states.Single(s => s.state.name == "Idle").state;
        foreach (var t in idle.transitions.Where(t => t.destinationState == run).ToArray()) idle.RemoveTransition(t);
        foreach (var t in run.transitions.ToArray()) run.RemoveTransition(t);
        var start = idle.AddTransition(run);
        start.hasExitTime = false; start.duration = 0f;
        start.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        start.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        var stop = run.AddTransition(idle);
        stop.hasExitTime = false; stop.duration = 0f;
        stop.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        stop.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        EditorUtility.SetDirty(controller);
        const string path = "Assets/_Project/Prefabs/ArmedHumanoid.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeRotation;
            if (root.GetComponent<EnemyMovement2D>() == null) root.AddComponent<EnemyMovement2D>();
            if (root.GetComponent<EnemyController>() == null) root.AddComponent<EnemyController>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        return new { configured = true, speed = 1.5f, detection = 5f, loseTarget = 7f, stop = 1f, resume = 1.2f };
    }
}
