using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class SetupArmedHumanoid
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play mode before setup.");
        const string folder = "Assets/_Project/Art/Enemies/ArmedHumanoid";
        System.IO.Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        AnimationClip idle = CopyClip("HeroKnight_Idle", "Idle", true, folder);
        AnimationClip hurt = CopyClip("HeroKnight_Hurt", "Hurt", false, folder);
        AnimationClip death = CopyClip("HeroKnight_DeathNoBlood", "Death", false, folder);
        string controllerPath = folder + "/ArmedHumanoid.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
            throw new Exception("Controller already exists; refusing to overwrite.");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        var idleState = machine.AddState("Idle"); idleState.motion = idle;
        var hurtState = machine.AddState("Hurt"); hurtState.motion = hurt;
        var deathState = machine.AddState("Death"); deathState.motion = death;
        machine.defaultState = idleState;
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        var die = machine.AddAnyStateTransition(deathState);
        die.hasExitTime = false; die.duration = 0f; die.canTransitionToSelf = false;
        die.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
        var hit = machine.AddAnyStateTransition(hurtState);
        hit.hasExitTime = false; hit.duration = 0f; hit.canTransitionToSelf = true;
        hit.AddCondition(AnimatorConditionMode.If, 0f, "Hurt");
        hit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
        var recover = hurtState.AddTransition(idleState);
        recover.hasExitTime = true; recover.exitTime = 1f; recover.duration = 0f;
        recover.hasFixedDuration = true;
        recover.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
        var root = new GameObject("ArmedHumanoid");
        try
        {
            root.layer = LayerMask.NameToLayer("Attackable");
            var body = root.AddComponent<Rigidbody2D>();
            body.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var capsule = root.AddComponent<CapsuleCollider2D>();
            // Match the Hero Knight art at 75% scale, with the root at foot level.
            capsule.size = new Vector2(0.5475f, 0.9f);
            capsule.offset = new Vector2(0f, 0.4965f);
            capsule.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/_Project/Physics/PlayerNoFriction.physicsMaterial2D");
            var visual = new GameObject("Visual");
            visual.layer = root.layer;
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.75f;
            var renderer = visual.AddComponent<SpriteRenderer>();
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(idle).First();
            renderer.sprite = AnimationUtility.GetObjectReferenceCurve(idle, binding)[0].value as Sprite;
            renderer.flipX = true;
            renderer.sortingOrder = 1;
            var animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            root.AddComponent<EnemyHealth>();
            var animation = root.AddComponent<EnemyAnimation>();
            var serialized = new SerializedObject(animation);
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Project/Prefabs/ArmedHumanoid.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();

        const string testPath = "Assets/_Project/Scenes/EnemyHealthTest.unity";
        if (!AssetDatabase.CopyAsset("Assets/_Project/Scenes/CombatTest.unity", testPath))
            throw new Exception("Could not create separate test scene; prefab has been saved.");
        var original = SceneManager.GetActiveScene();
        var test = EditorSceneManager.OpenScene(testPath, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(test);
            var dummy = test.GetRootGameObjects().FirstOrDefault(g => g.name == "Training Dummy");
            if (dummy != null) dummy.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ArmedHumanoid.prefab");
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, test);
            enemy.transform.position = new Vector3(1.8f, -0.72f, 0f);
            EditorSceneManager.SaveScene(test);
        }
        finally
        {
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(test, true);
        }
        return new { prefab = "Assets/_Project/Prefabs/ArmedHumanoid.prefab", testScene = testPath, maxHealth = 30, originalSceneUntouched = true };
    }

    private static AnimationClip CopyClip(string sourceName, string name, bool loop, string folder)
    {
        var source = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Hero Knight - Pixel Art/Animations/" + sourceName + ".anim");
        if (source == null) throw new Exception("Missing animation " + sourceName);
        var clip = UnityEngine.Object.Instantiate(source);
        clip.name = name;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        AssetDatabase.CreateAsset(clip, folder + "/" + name + ".anim");
        return clip;
    }
}
