using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class SetupKnightBoss
{
    const string Folder = "Assets/_Project/Art/Enemies/KnightBoss";
    const string Source = "Assets/FreeKnight_v1/Colour2/Outline/120x80_PNGSheets/";
    const string Prefab = "Assets/_Project/Prefabs/KnightBoss.prefab";
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play mode first.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefab) != null) throw new Exception("KnightBoss already exists; refusing to overwrite.");
        System.IO.Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var idle = Clip("Idle", "Idle", 0, 10, true);
        var run = Clip("Run", "Run", 0, 10, true);
        var hurt = Clip("Hurt", "Hit", 0, 1, false);
        var death = Clip("Death", "DeathNoMovement", 0, 10, false);
        var guard = Clip("Guard", "AttackNoMovement", 0, 1, true);
        var parry = Clip("Parry", "AttackNoMovement", 0, 1, false);
        var first = Step("Combo1", "AttackNoMovement", 1, .4f, .13f, .18f, 12, 0);
        var second = Step("Combo2", "Attack2NoMovement", 2, .24f, .16f, .75f, 12, 0);
        var heavy = Step("Heavy", "Attack2NoMovement", 3, .75f, .25f, 1.1f, 25, 1.8f);
        heavy.hitboxOffset = new Vector2(1f, .65f); heavy.hitboxSize = new Vector2(1.8f, 1.2f);
        var counter = Step("Counter", "AttackNoMovement", 4, .24f, .14f, .65f, 15, .25f);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(Folder + "/KnightBoss.controller");
        foreach (var name in new[] { "IsMoving", "IsDead", "IsAttacking", "IsGuarding", "IsStaggered" }) controller.AddParameter(name, AnimatorControllerParameterType.Bool);
        foreach (var name in new[] { "Attack", "Hurt", "Parry", "StrikeParried" }) controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        foreach (var name in new[] { "PrepareSpeed", "StrikeSpeed", "RecoverySpeed", "HurtSpeed", "ParrySpeed" }) controller.AddParameter(name, AnimatorControllerParameterType.Float);
        controller.AddParameter("Step", AnimatorControllerParameterType.Int);
        var sm = controller.layers[0].stateMachine;
        var i = State(sm, idle, new Vector3(200, 0)); var r = State(sm, run, new Vector3(450, 0));
        var h = State(sm, hurt, new Vector3(200, 150), "HurtSpeed");
        var d = State(sm, death, new Vector3(700, 150));
        var g = State(sm, guard, new Vector3(200, 300));
        var pa = State(sm, parry, new Vector3(450, 300), "ParrySpeed"); sm.defaultState = i;
        var die = Any(sm, d); die.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
        var hit = Any(sm, h); hit.canTransitionToSelf = true; hit.AddCondition(AnimatorConditionMode.If, 0, "Hurt"); hit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        var recoverHit = Link(h, i, false); recoverHit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsStaggered"); recoverHit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        var parryEnter = Any(sm, pa); parryEnter.AddCondition(AnimatorConditionMode.If, 0, "Parry"); parryEnter.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead");
        Link(pa, i, true);
        var guardEnter = Any(sm, g); guardEnter.AddCondition(AnimatorConditionMode.If, 0, "IsGuarding"); guardEnter.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking"); guardEnter.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead"); guardEnter.AddCondition(AnimatorConditionMode.IfNot, 0, "IsStaggered");
        Link(g, i, false).AddCondition(AnimatorConditionMode.IfNot, 0, "IsGuarding");
        Link(i, r, false).AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        Link(r, i, false).AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        int row = 0;
        foreach (var step in new[] { first, second, heavy, counter })
        {
            var p = State(sm, step.prepareAnimation, new Vector3(200, 500 + row * 150), "PrepareSpeed");
            var a = State(sm, step.strikeAnimation, new Vector3(450, 500 + row * 150), "StrikeSpeed");
            var rec = State(sm, step.recoveryAnimation, new Vector3(700, 500 + row * 150), "RecoverySpeed"); row++;
            var enter = Any(sm, p); enter.AddCondition(AnimatorConditionMode.If, 0, "Attack"); enter.AddCondition(AnimatorConditionMode.Equals, step.animationId, "Step"); enter.AddCondition(AnimatorConditionMode.If, 0, "IsAttacking"); enter.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead"); enter.AddCondition(AnimatorConditionMode.IfNot, 0, "IsStaggered");
            Link(p, a, true).AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");
            var deflect = Link(a, rec, false); deflect.AddCondition(AnimatorConditionMode.If, 0, "StrikeParried");
            Link(a, rec, true).AddCondition(AnimatorConditionMode.If, 0, "IsAttacking");
            foreach (var state in new[] { p, a, rec })
            {
                var cancel = Link(state, i, false); cancel.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking"); cancel.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDead"); cancel.AddCondition(AnimatorConditionMode.IfNot, 0, "IsStaggered");
            }
        }
        var root = new GameObject("KnightBoss");
        try
        {
            root.layer = LayerMask.NameToLayer("Attackable");
            var body = root.AddComponent<Rigidbody2D>(); body.gravityScale = 3; body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous; body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var capsule = root.AddComponent<CapsuleCollider2D>(); capsule.size = new Vector2(.62f, 1.25f); capsule.offset = new Vector2(0, .625f);
            capsule.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/_Project/Physics/PlayerNoFriction.physicsMaterial2D");
            var visual = new GameObject("Visual"); visual.layer = root.layer; visual.transform.SetParent(root.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = Sprites("Idle")[0]; renderer.sortingOrder = 1;
            visual.transform.localScale = Vector3.one * renderer.sprite.pixelsPerUnit / 28f; visual.transform.localPosition = Offset(renderer.sprite);
            var animator = visual.AddComponent<Animator>(); animator.runtimeAnimatorController = controller; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            visual.AddComponent<BossAnimationEvents>();
            var health = new SerializedObject(root.AddComponent<EnemyHealth>()); health.FindProperty("maxHealth").intValue = 240; health.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<EnemyMovement2D>();
            var view = root.AddComponent<BossAnimation>(); Assign(view, "animator", animator); Assign(view, "hurtAnimation", hurt); Assign(view, "parryAnimation", parry); Assign(view, "spriteRenderer", renderer);
            var combat = root.AddComponent<BossCombat>();
            var so = new SerializedObject(combat); var combo = so.FindProperty("combo"); combo.arraySize = 2;
            Fill(combo.GetArrayElementAtIndex(0), first); Fill(combo.GetArrayElementAtIndex(1), second); Fill(so.FindProperty("heavy"), heavy); Fill(so.FindProperty("counter"), counter); so.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<BossController>(); root.AddComponent<BossGuard>();
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        const string scenePath = "Assets/_Project/Scenes/BossTest.unity";
        if (!AssetDatabase.CopyAsset("Assets/_Project/Scenes/CombatTest.unity", scenePath)) throw new Exception("Could not create BossTest scene.");
        var original = SceneManager.GetActiveScene(); var test = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        try
        {
            foreach (var obj in test.GetRootGameObjects()) if (obj.name == "Training Dummy") obj.SetActive(false);
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), test); enemy.transform.position = new Vector3(2.8f, -.72f);
            EditorSceneManager.SaveScene(test);
        }
        finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(test, true); }
        return new { prefab = Prefab, testScene = scenePath, health = 240, patterns = "2-hit combo / advancing heavy / guard parry counter", source = Source };
    }
    static void Fill(SerializedProperty property, BossAttackStep step)
    {
        property.FindPropertyRelative("prepareDuration").floatValue = step.prepareDuration;
        property.FindPropertyRelative("activeDuration").floatValue = step.activeDuration;
        property.FindPropertyRelative("recoveryDuration").floatValue = step.recoveryDuration;
        property.FindPropertyRelative("damage").intValue = step.damage;
        property.FindPropertyRelative("lungeDistance").floatValue = step.lungeDistance;
        property.FindPropertyRelative("hitboxOffset").vector2Value = step.hitboxOffset;
        property.FindPropertyRelative("hitboxSize").vector2Value = step.hitboxSize;
        property.FindPropertyRelative("animationId").intValue = step.animationId;
        property.FindPropertyRelative("prepareAnimation").objectReferenceValue = step.prepareAnimation;
        property.FindPropertyRelative("strikeAnimation").objectReferenceValue = step.strikeAnimation;
        property.FindPropertyRelative("recoveryAnimation").objectReferenceValue = step.recoveryAnimation;
    }
    static BossAttackStep Step(string name, string sheet, int id, float prepare, float active, float recovery, int damage, float lunge)
    {
        bool second = sheet == "Attack2NoMovement";
        var p = Clip(name + "Prepare", sheet, 0, second ? 2 : 1, false);
        var a = Clip(name + "Strike", sheet, second ? 2 : 1, 2, false);
        var r = Clip(name + "Recovery", sheet, second ? 4 : 3, second ? 2 : 1, false);
        AnimationUtility.SetAnimationEvents(a, new[] { new AnimationEvent { time = 0, functionName = "BeginHitbox" }, new AnimationEvent { time = a.length, functionName = "EndHitbox" } });
        AnimationUtility.SetAnimationEvents(r, new[] { new AnimationEvent { time = r.length, functionName = "EndStep" } });
        return new BossAttackStep { animationId = id, prepareDuration = prepare, activeDuration = active, recoveryDuration = recovery, damage = damage, lungeDistance = lunge, prepareAnimation = p, strikeAnimation = a, recoveryAnimation = r };
    }
    static void Assign(UnityEngine.Object obj, string name, UnityEngine.Object value)
    { var so = new SerializedObject(obj); so.FindProperty(name).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static Sprite[] Sprites(string name) => AssetDatabase.LoadAllAssetsAtPath(Source + "_" + name + ".png").OfType<Sprite>().OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();
    static Vector3 Offset(Sprite s) => new Vector3((s.rect.x % 120 + s.pivot.x - 54) / 28f, (s.rect.y + s.pivot.y) / 28f, 0);
    static AnimationClip Clip(string name, string sheet, int start, int count, bool loop)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Source + "_" + sheet + ".png");
        if (importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed)
        { importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport(); }
        var sprites = Sprites(sheet).Skip(start).Take(count).ToArray(); if (sprites.Length != count) throw new Exception("Unexpected sprite count: " + sheet);
        var clip = new AnimationClip { name = name, frameRate = 10 }; var frames = new ObjectReferenceKeyframe[count + 1];
        var xs = new AnimationCurve(); var ys = new AnimationCurve();
        for (int j = 0; j <= count; j++)
        {
            var sprite = sprites[Math.Min(j, count - 1)]; var offset = Offset(sprite);
            frames[j] = new ObjectReferenceKeyframe { time = j * .1f, value = sprite }; xs.AddKey(j * .1f, offset.x); ys.AddKey(j * .1f, offset.y);
        }
        foreach (var curve in new[] { xs, ys }) for (int j = 0; j < curve.length; j++) { AnimationUtility.SetKeyLeftTangentMode(curve, j, AnimationUtility.TangentMode.Constant); AnimationUtility.SetKeyRightTangentMode(curve, j, AnimationUtility.TangentMode.Constant); }
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), frames);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"), xs);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"), ys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, Folder + "/" + name + ".anim"); return clip;
    }
    static AnimatorState State(AnimatorStateMachine sm, AnimationClip clip, Vector3 pos, string speed = null)
    { var s = sm.AddState(clip.name, pos); s.motion = clip; if (speed != null) { s.speedParameter = speed; s.speedParameterActive = true; } return s; }
    static AnimatorStateTransition Any(AnimatorStateMachine sm, AnimatorState state)
    { var t = sm.AddAnyStateTransition(state); t.hasExitTime = false; t.duration = 0; t.canTransitionToSelf = false; return t; }
    static AnimatorStateTransition Link(AnimatorState from, AnimatorState to, bool end)
    { var t = from.AddTransition(to); t.hasExitTime = end; t.exitTime = 1; t.duration = 0; return t; }
}
