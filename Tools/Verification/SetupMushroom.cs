using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class SetupMushroom
{
    const string Folder = "Assets/_Project/Art/Enemies/Mushroom";
    const string Source = "Assets/Forest_Monsters_FREE/Mushroom/Mushroom without VFX/";
    const string Prefab = "Assets/_Project/Prefabs/Mushroom.prefab";
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play mode first.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefab) != null) throw new Exception("Mushroom prefab already exists.");
        System.IO.Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var idle = Clip("Idle", "Idle", 0, 7, true);
        var run = Clip("Run", "Run", 0, 8, true);
        var hurt = Clip("Hurt", "Hit", 0, 5, false);
        var stun = Clip("Stun", "Stun", 0, 18, false);
        var death = Clip("Death", "Die", 0, 15, false);
        var prepare = Clip("AttackPrepare", "Attack", 0, 4, false);
        var strike = Clip("AttackStrike", "Attack", 4, 4, false);
        var recovery = Clip("AttackRecovery", "Attack", 8, 2, false);
        AnimationUtility.SetAnimationEvents(strike, new[] { new AnimationEvent { time = 0, functionName = "BeginHitbox" }, new AnimationEvent { time = strike.length, functionName = "EndHitbox" } });
        AnimationUtility.SetAnimationEvents(recovery, new[] { new AnimationEvent { time = recovery.length, functionName = "EndAttack" } });
        var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/Mushroom.controller");
        foreach (string name in new[] { "IsMoving", "IsDead", "IsAttacking", "IsStaggered" }) controller.AddParameter(name, AnimatorControllerParameterType.Bool);
        foreach (string name in new[] { "Attack", "Hurt", "Stun" }) controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        foreach (string name in new[] { "PrepareSpeed", "StrikeSpeed", "RecoverySpeed", "StaggerSpeed" }) controller.AddParameter(name, AnimatorControllerParameterType.Float);
        var sm = controller.layers[0].stateMachine;
        var i = State(sm, idle, new Vector3(200, 0)); var r = State(sm, run, new Vector3(450, 0));
        var p = State(sm, prepare, new Vector3(200, 150), "PrepareSpeed");
        var a = State(sm, strike, new Vector3(450, 150), "StrikeSpeed");
        var rec = State(sm, recovery, new Vector3(700, 150), "RecoverySpeed");
        var h = State(sm, hurt, new Vector3(200, 300), "StaggerSpeed");
        var s = State(sm, stun, new Vector3(450, 300), "StaggerSpeed");
        var d = State(sm, death, new Vector3(700, 300)); sm.defaultState = i;
        var die = Any(sm, d); die.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
        foreach (var state in new[] { h, s })
        {
            var t = Any(sm, state); t.canTransitionToSelf = true;
            t.AddCondition(AnimatorConditionMode.If, 0, state == h ? "Hurt" : "Stun"); t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
            var exit = Link(state, i, false); exit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsStaggered"); exit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        }
        var enter = Any(sm, p); enter.AddCondition(AnimatorConditionMode.If, 0, "Attack"); enter.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking"); enter.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead"); enter.AddCondition(AnimatorConditionMode.IfNot, 0, "IsStaggered");
        Link(p, a, true).AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");
        Link(a, rec, true).AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");
        Link(rec, i, true);
        foreach (var state in new[] { p, a, rec })
        {
            var cancel = Link(state, i, false); cancel.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking"); cancel.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead"); cancel.AddCondition(AnimatorConditionMode.IfNot, 0, "IsStaggered");
        }
        Link(i, r, false).AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        Link(r, i, false).AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        var root = new GameObject("Mushroom");
        try
        {
            root.layer = LayerMask.NameToLayer("Attackable");
            var body = root.AddComponent<Rigidbody2D>(); body.gravityScale = 3; body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous; body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var capsule = root.AddComponent<CapsuleCollider2D>(); capsule.size = new Vector2(0.65f, 0.85f); capsule.offset = new Vector2(0, 0.45f);
            capsule.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/_Project/Physics/PlayerNoFriction.physicsMaterial2D");
            var visual = new GameObject("Visual"); visual.layer = root.layer; visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * (Sprites("Idle")[0].pixelsPerUnit / 32f);
            var renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = Sprites("Idle")[0]; renderer.sortingOrder = 1; renderer.flipX = true;
            visual.transform.localPosition = Offset(renderer.sprite);
            var animator = visual.AddComponent<Animator>(); animator.runtimeAnimatorController = controller; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            visual.AddComponent<MushroomAnimationEvents>();
            root.AddComponent<EnemyHealth>();
            var movement = new SerializedObject(root.AddComponent<EnemyMovement2D>());
            movement.FindProperty("spriteFacesLeft").boolValue = true; movement.ApplyModifiedPropertiesWithoutUndo();
            var combat = root.AddComponent<MushroomCombat>();
            Assign(combat, "animator", animator); Assign(combat, "attackPrepare", prepare); Assign(combat, "attackStrike", strike); Assign(combat, "attackRecovery", recovery);
            var ai = root.AddComponent<MushroomController>(); Assign(ai, "animator", animator); Assign(ai, "hurtAnimation", hurt); Assign(ai, "stunAnimation", stun);
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        const string scenePath = "Assets/_Project/Scenes/MushroomTest.unity";
        if (!AssetDatabase.CopyAsset("Assets/_Project/Scenes/CombatTest.unity", scenePath)) throw new Exception("Could not create Mushroom test scene.");
        var original = SceneManager.GetActiveScene(); var test = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            foreach (var obj in test.GetRootGameObjects()) if (obj.name == "Training Dummy") obj.SetActive(false);
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), test);
            enemy.transform.position = new Vector3(1.8f, -0.72f);
            EditorSceneManager.SaveScene(test);
        }
        finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(test, true); }
        return new { prefab = Prefab, testScene = scenePath, pixelsPerUnit = 32, damage = 10, health = 30, phases = "0.45 / 0.22 / 0.65", lunge = 1.15 };
    }
    static void Assign(UnityEngine.Object obj, string name, UnityEngine.Object value)
    {
        var so = new SerializedObject(obj); so.FindProperty(name).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    static Sprite[] Sprites(string name) => AssetDatabase.LoadAllAssetsAtPath(Source + "Mushroom-" + name + ".png").OfType<Sprite>().OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
    // Original sprites are tightly cropped. Restore their position inside the 80px-wide source frame so feet do not jump between frames.
    static Vector3 Offset(Sprite s) => new Vector3((s.rect.x % 80 + s.pivot.x - 40) / 32f, (s.rect.y + s.pivot.y) / 32f, 0);
    static AnimationClip Clip(string name, string sheet, int start, int count, bool loop)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Source + "Mushroom-" + sheet + ".png");
        if (importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed)
        { importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport(); }
        var sprites = Sprites(sheet).Skip(start).Take(count).ToArray();
        if (sprites.Length != count) throw new Exception("Unexpected sprite count in " + sheet);
        var clip = new AnimationClip { name = name, frameRate = 10 };
        var frames = new ObjectReferenceKeyframe[count + 1];
        var xs = new AnimationCurve(); var ys = new AnimationCurve();
        for (int j = 0; j <= count; j++)
        {
            var sprite = sprites[Math.Min(j, count - 1)]; var offset = Offset(sprite);
            frames[j] = new ObjectReferenceKeyframe { time = j * 0.1f, value = sprite };
            xs.AddKey(j * 0.1f, offset.x); ys.AddKey(j * 0.1f, offset.y);
        }
        foreach (var curve in new[] { xs, ys }) for (int j = 0; j < curve.length; j++)
        { AnimationUtility.SetKeyLeftTangentMode(curve, j, AnimationUtility.TangentMode.Constant); AnimationUtility.SetKeyRightTangentMode(curve, j, AnimationUtility.TangentMode.Constant); }
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), frames);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"), xs);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"), ys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, Folder + "/" + name + ".anim"); return clip;
    }
    static AnimatorState State(AnimatorStateMachine sm, AnimationClip clip, Vector3 pos, string speed = null)
    {
        var state = sm.AddState(clip.name, pos); state.motion = clip;
        if (speed != null) { state.speedParameter = speed; state.speedParameterActive = true; }
        return state;
    }
    static AnimatorStateTransition Any(AnimatorStateMachine sm, AnimatorState state)
    { var t = sm.AddAnyStateTransition(state); t.hasExitTime = false; t.duration = 0; t.canTransitionToSelf = false; return t; }
    static AnimatorStateTransition Link(AnimatorState from, AnimatorState to, bool end)
    { var t = from.AddTransition(to); t.hasExitTime = end; t.exitTime = 1; t.duration = 0; return t; }
}
