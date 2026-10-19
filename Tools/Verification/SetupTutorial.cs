using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using TMPro;
public static class SetupTutorial
{
    const string ScenePath = "Assets/_Project/Scenes/Tutorial.unity";
    const string UIFolder = "Assets/_Project/Data/UI";
    static TMP_FontAsset font;
    static readonly Color Ink = new Color(.055f, .08f, .105f, .94f);
    static readonly Color Gold = new Color(.95f, .77f, .42f);
    static readonly Color Paper = new Color(.91f, .94f, .95f);
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play mode first.");
        if (File.Exists(ScenePath)) throw new Exception("Tutorial scene already exists; refusing to overwrite.");
        Directory.CreateDirectory(UIFolder); AssetDatabase.Refresh(); font = MakeFont();
        if (!AssetDatabase.CopyAsset("Assets/_Project/Scenes/CombatTest.unity", ScenePath)) throw new Exception("Cannot create tutorial scene.");
        var original = SceneManager.GetActiveScene(); var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var roots = scene.GetRootGameObjects();
            var groundSprite = roots.Single(g => g.name == "Ground").GetComponent<SpriteRenderer>().sprite;
            var player = roots.SelectMany(r => r.GetComponentsInChildren<PlayerController>(true)).Single();
            var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single();
            foreach (var root in roots) if (root != player.gameObject && root != camera.gameObject) UnityEngine.Object.DestroyImmediate(root);
            player.transform.position = new Vector3(.5f, .675f);
            camera.orthographicSize = 4.5f; camera.backgroundColor = new Color(.11f, .16f, .2f); camera.transform.position = new Vector3(3, 1.8f, -10);
            var follow = camera.gameObject.AddComponent<TutorialCamera>(); Set(follow, "target", player.transform);
            var world = new GameObject("TutorialWorld");
            Terrain("Ground", groundSprite, new Vector2(21.5f, -.4f), new Vector2(53, .8f), world.transform);
            Terrain("LeftBoundary", groundSprite, new Vector2(-5, 2f), new Vector2(.6f, 5), world.transform);
            Terrain("RightBoundary", groundSprite, new Vector2(48, 2f), new Vector2(.6f, 5), world.transform);
            Terrain("JumpStep", groundSprite, new Vector2(6.8f, .28f), new Vector2(1.3f, .56f), world.transform);
            Terrain("JumpPlatform", groundSprite, new Vector2(9.2f, .35f), new Vector2(1.4f, .3f), world.transform);
            var gate = Terrain("BossEntranceGate", groundSprite, new Vector2(33, 1.8f), new Vector2(.5f, 3.6f), world.transform); gate.SetActive(false);
            var regular = new GameObject("RegularEnemies");
            var mushroom = Instance("Assets/_Project/Prefabs/Mushroom.prefab", scene, new Vector3(15, 0)); mushroom.transform.SetParent(regular.transform, true);
            var humanoid = Instance("Assets/_Project/Prefabs/ArmedHumanoid.prefab", scene, new Vector3(25, -.0465f)); humanoid.transform.SetParent(regular.transform, true);
            var boss = Instance("Assets/_Project/Prefabs/KnightBoss.prefab", scene, new Vector3(40.5f, 0)); boss.SetActive(false);
            var systems = new GameObject("RunFlow"); var flow = systems.AddComponent<RetryController>(); var tutorial = systems.AddComponent<TutorialDirector>();
            var fall = systems.AddComponent<FallRespawn>(); Set(fall, "player", player.GetComponent<PlayerHealth>());
            var start = new GameObject("StartSpawn"); start.transform.position = player.transform.position;
            var cpRoot = Trigger("BossCheckpoint", new Vector2(30.5f, 1), new Vector2(1.6f, 2)); var checkpoint = cpRoot.AddComponent<Checkpoint>();
            var cpSpawn = new GameObject("CheckpointSpawn"); cpSpawn.transform.position = new Vector3(30.5f, .675f); cpSpawn.transform.SetParent(cpRoot.transform, true);
            var fire = Decor("PF Village Props - Campfire 01", new Vector3(30.5f, 0), scene);
            Set(checkpoint, "spawnPoint", cpSpawn.transform); Set(checkpoint, "flow", flow); Set(checkpoint, "marker", fire.GetComponentInChildren<SpriteRenderer>());
            var arena = Trigger("BossEncounter", new Vector2(34.4f, 1), new Vector2(.6f, 2)); var encounter = arena.AddComponent<BossEncounter>();
            var hud = MakeHUD(player.GetComponent<PlayerHealth>(), flow);
            Set(flow, "player", player); Set(flow, "startSpawn", start.transform); Set(flow, "checkpoints", new UnityEngine.Object[] { checkpoint }); Set(flow, "regularEnemies", regular); Set(flow, "hud", hud);
            Set(tutorial, "player", player); Set(tutorial, "flow", flow); Set(tutorial, "hud", hud);
            Set(encounter, "boss", boss); Set(encounter, "entranceGate", gate); Set(encounter, "flow", flow); Set(encounter, "hud", hud); Set(encounter, "tutorial", tutorial);
            Zone("MovementLesson", .5f, 4f, TutorialLesson.Movement, tutorial);
            Zone("JumpLesson", 5f, 4.5f, TutorialLesson.Jump, tutorial);
            Zone("AttackLesson", 10f, 2f, TutorialLesson.Attack, tutorial);
            Zone("DodgeLesson", 14.5f, 6.5f, TutorialLesson.Dodge, tutorial);
            Zone("ParryLesson", 24f, 9f, TutorialLesson.Parry, tutorial);
            Label("MovementSign", "이동 / 점프", new Vector3(4, 2.5f));
            Label("DodgeSign", "회피 / 뒤잡기", new Vector3(14, 2.5f));
            Label("ParrySign", "패링 / 공방", new Vector3(24, 2.5f));
            Label("CheckpointSign", "체크포인트", new Vector3(30.5f, 2.3f));
            Label("BossSign", "기사 보스", new Vector3(40, 2.7f));
            foreach (float x in new[] { -2f, 18f, 28f, 47f }) Decor("PF Village Props - Tree 02", new Vector3(x, 0), scene);
            foreach (float x in new[] { 11f, 21f, 34f }) Decor("PF Village Props - Sign 01", new Vector3(x, 0), scene);
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
        }
        finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene, true); }
        return new { scene = ScenePath, buildIndex = 0, map = "Movement / Jump / Mushroom / Armed Humanoid / Checkpoint / Knight Boss", retry = "scene reload", hud = "Korean uGUI + keyboard/gamepad UI" };
    }
    static TMP_FontAsset MakeFont()
    {
        string path = UIFolder + "/TutorialKorean SDF.asset"; var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); if (asset != null) return asset;
        asset = TMP_FontAsset.CreateFontAsset("C:/Windows/Fonts/malgun.ttf", 0, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024);
        if (asset == null) throw new Exception("Could not bake Korean font.");
        string all = string.Concat(Enumerable.Range(32, 95).Select(n => (char)n)) + string.Concat(Directory.GetFiles("Assets/_Project/Scripts/Flow", "*.cs").Select(File.ReadAllText)) + File.ReadAllText("Tools/Verification/SetupTutorial.cs");
        all = new string(all.Where(c => !char.IsControl(c)).Distinct().ToArray());
        if (!asset.TryAddCharacters(all, out string missing)) throw new Exception("Missing HUD glyphs: " + missing);
        asset.name = "TutorialKorean SDF"; asset.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(asset, path); asset.material.name = "TutorialKorean Material"; AssetDatabase.AddObjectToAsset(asset.material, asset);
        foreach (var texture in asset.atlasTextures) { texture.name = "TutorialKorean Atlas"; AssetDatabase.AddObjectToAsset(texture, asset); }
        EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets(); return asset;
    }
    static GameObject Instance(string path, Scene scene, Vector3 position)
    { var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene); obj.transform.position = position; return obj; }
    static GameObject Terrain(string name, Sprite sprite, Vector2 center, Vector2 size, Transform parent)
    {
        var obj = new GameObject(name); obj.layer = LayerMask.NameToLayer("Ground"); obj.transform.SetParent(parent, false); obj.transform.position = center;
        var sr = obj.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.drawMode = SpriteDrawMode.Tiled; sr.size = size; sr.sortingOrder = -1;
        obj.AddComponent<BoxCollider2D>().size = size; return obj;
    }
    static GameObject Trigger(string name, Vector2 center, Vector2 size)
    { var obj = new GameObject(name); obj.transform.position = center; var collider = obj.AddComponent<BoxCollider2D>(); collider.isTrigger = true; collider.size = size; return obj; }
    static void Zone(string name, float x, float width, TutorialLesson lesson, TutorialDirector director)
    { var zone = Trigger(name, new Vector2(x, 1.5f), new Vector2(width, 3)).AddComponent<TutorialZone>(); Set(zone, "lesson", (int)lesson); Set(zone, "director", director); }
    static GameObject Decor(string name, Vector3 position, Scene scene)
    {
        var obj = Instance("Assets/Cainos/Pixel Art Platformer - Village Props/Prefab/" + name + ".prefab", scene, position);
        foreach (var collider in obj.GetComponentsInChildren<Collider2D>(true)) UnityEngine.Object.DestroyImmediate(collider);
        foreach (var sprite in obj.GetComponentsInChildren<SpriteRenderer>(true)) sprite.sortingOrder = -3;
        return obj;
    }
    static TextMeshPro Label(string name, string value, Vector3 position)
    {
        var obj = new GameObject(name); obj.transform.position = position; obj.transform.localScale = Vector3.one * .12f;
        var text = obj.AddComponent<TextMeshPro>(); text.font = font; text.text = value; text.fontSize = 24; text.alignment = TextAlignmentOptions.Center; text.color = Gold;
        text.rectTransform.sizeDelta = new Vector2(60, 10); obj.GetComponent<MeshRenderer>().sortingOrder = 2; return text;
    }
    static TutorialHUD MakeHUD(PlayerHealth player, RetryController flow)
    {
        var canvasObject = new GameObject("TutorialHUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
        var hud = canvasObject.AddComponent<TutorialHUD>(); Set(hud, "player", player); Set(hud, "flow", flow);
        var health = Panel("PlayerHealth", canvasObject.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -26), new Vector2(270, 80), Ink);
        var hpText = Text("PlayerHealthText", health.transform, "체력 100 / 100", 21, new Vector2(18, -12), new Vector2(234, 30));
        var hpBack = Panel("HealthTrack", health.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -52), new Vector2(234, 10), new Color(.2f, .25f, .28f));
        var hpFill = Fill("HealthFill", hpBack.transform, new Color(.6f, .82f, .7f));
        var checkpoint = Text("CheckpointText", canvasObject.transform, "복귀 위치 · 훈련장 입구", 17, new Vector2(-32, -30), new Vector2(340, 34), new Vector2(1, 1)); checkpoint.alignment = TextAlignmentOptions.Right;
        var lesson = Panel("LessonPanel", canvasObject.transform, Vector2.zero, Vector2.zero, new Vector2(32, 24), new Vector2(1216, 142), Ink);
        var rect = lesson.GetComponent<RectTransform>(); rect.anchorMax = new Vector2(1, 0); rect.offsetMin = new Vector2(32, 24); rect.offsetMax = new Vector2(-32, 166);
        var title = Text("LessonTitle", lesson.transform, "01  이동", 23, new Vector2(22, -15), new Vector2(1140, 32)); title.color = Gold;
        var body = Text("LessonBody", lesson.transform, "오른쪽으로 이동해 훈련장을 통과하세요.", 20, new Vector2(22, -55), new Vector2(1140, 77));
        body.rectTransform.anchorMin = Vector2.zero; body.rectTransform.anchorMax = Vector2.one; body.rectTransform.offsetMin = new Vector2(22, 10); body.rectTransform.offsetMax = new Vector2(-22, -55);
        var notice = Text("Notice", canvasObject.transform, "", 22, new Vector2(0, 186), new Vector2(900, 36), new Vector2(.5f, 0)); notice.alignment = TextAlignmentOptions.Center; notice.color = Gold;
        var bossPanel = Panel("BossHealth", canvasObject.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -26), new Vector2(430, 80), Ink);
        var bossLabel = Text("BossHealthText", bossPanel.transform, "기사 보스", 21, new Vector2(18, -12), new Vector2(394, 30)); bossLabel.alignment = TextAlignmentOptions.Center;
        var bossTrack = Panel("BossTrack", bossPanel.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -52), new Vector2(394, 10), new Color(.2f, .25f, .28f)); var bossFill = Fill("BossFill", bossTrack.transform, new Color(.85f, .47f, .4f));
        var modal = Panel("RunModal", canvasObject.transform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(.015f, .025f, .04f, .8f)); Stretch(modal.GetComponent<RectTransform>()); modal.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        var card = Panel("ModalCard", modal.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(620, 350), new Color(Ink.r, Ink.g, Ink.b, 1f));
        var modalTitle = Text("ModalTitle", card.transform, "다시 도전", 34, new Vector2(30, -28), new Vector2(560, 48)); modalTitle.color = Gold; modalTitle.alignment = TextAlignmentOptions.Center;
        var modalBody = Text("ModalBody", card.transform, "체크포인트에서 다시 시작합니다.", 21, new Vector2(40, -95), new Vector2(540, 74)); modalBody.alignment = TextAlignmentOptions.Center;
        var primary = Button("PrimaryButton", card.transform, "재도전", new Vector2(0, 94), new Vector2(300, 50), Gold);
        var secondary = Button("RestartButton", card.transform, "처음부터 다시 시작", new Vector2(0, 34), new Vector2(300, 40), new Color(.24f, .33f, .4f));
        Set(hud, "playerHealthText", hpText); Set(hud, "playerHealthFill", hpFill); Set(hud, "checkpointText", checkpoint); Set(hud, "lessonTitle", title); Set(hud, "lessonBody", body); Set(hud, "noticeText", notice);
        Set(hud, "bossPanel", bossPanel); Set(hud, "bossHealthText", bossLabel); Set(hud, "bossHealthFill", bossFill); Set(hud, "modal", modal); Set(hud, "modalTitle", modalTitle); Set(hud, "modalBody", modalBody); Set(hud, "primaryButton", primary); Set(hud, "primaryLabel", primary.GetComponentInChildren<TMP_Text>()); Set(hud, "restartButton", secondary);
        bossPanel.SetActive(false); modal.SetActive(false);
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); var module = eventSystem.GetComponent<InputSystemUIInputModule>();
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"); module.actionsAsset = asset;
        module.point = Reference(asset, "Point"); module.leftClick = Reference(asset, "Click"); module.move = Reference(asset, "Navigate"); module.submit = Reference(asset, "Submit"); module.cancel = Reference(asset, "Cancel"); module.scrollWheel = Reference(asset, "ScrollWheel"); module.deselectOnBackgroundClick = false;
        return hud;
    }
    static InputActionReference Reference(InputActionAsset asset, string name)
    {
        string path = UIFolder + "/UI" + name + ".asset"; var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
        if (reference == null) { reference = InputActionReference.Create(asset.FindAction("UI/" + name, true)); AssetDatabase.CreateAsset(reference, path); } return reference;
    }
    static GameObject Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); obj.transform.SetParent(parent, false); var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
        var image = obj.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return obj;
    }
    static TextMeshProUGUI Text(string name, Transform parent, string value, int size, Vector2 position, Vector2 dimensions, Vector2? anchor = null)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false); var text = obj.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size; text.color = Paper; text.raycastTarget = false; text.enableAutoSizing = false; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
        var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(0, 1); rect.pivot = anchor ?? new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = dimensions; return text;
    }
    static UnityEngine.UI.Image Fill(string name, Transform parent, Color color)
    {
        var obj = Panel(name, parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, color); Stretch(obj.GetComponent<RectTransform>()); var fill = obj.GetComponent<UnityEngine.UI.Image>(); fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; return fill;
    }
    static UnityEngine.UI.Button Button(string name, Transform parent, string label, Vector2 position, Vector2 size, Color color)
    {
        var obj = Panel(name, parent, new Vector2(.5f, 0), new Vector2(.5f, 0), position, size, color); var image = obj.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = true;
        var button = obj.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; var colors = button.colors; colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f); colors.selectedColor = new Color(1.15f, 1.15f, 1.15f); colors.pressedColor = new Color(.75f, .75f, .75f); button.colors = colors;
        var text = Text("Label", obj.transform, label, 21, Vector2.zero, Vector2.zero); Stretch(text.rectTransform); text.alignment = TextAlignmentOptions.Center; text.color = name == "PrimaryButton" ? Ink : Paper; return button;
    }
    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    static void Set(UnityEngine.Object target, string path, object value)
    {
        var so = new SerializedObject(target); var p = so.FindProperty(path); if (p == null) throw new Exception(target.name + "." + path + " missing");
        if (value is UnityEngine.Object[] array) { p.arraySize = array.Length; for (int i = 0; i < array.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = array[i]; }
        else if (value is UnityEngine.Object reference) p.objectReferenceValue = reference;
        else if (value is int number) p.intValue = number;
        else throw new Exception("Unsupported setup value " + path);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}


