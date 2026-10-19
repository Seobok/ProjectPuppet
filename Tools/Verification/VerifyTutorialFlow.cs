using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using TMPro;
using UnityEditor;
public static class VerifyTutorialFlow
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    static RetryController Flow() => UnityEngine.Object.FindFirstObjectByType<RetryController>();
    static BossEncounter Encounter() => UnityEngine.Object.FindFirstObjectByType<BossEncounter>();
    static GameObject Regular() => Get<GameObject>(Flow(), "regularEnemies");
    static void Teleport(PlayerController player, Vector3 position)
    { var body = player.GetComponent<Rigidbody2D>(); player.transform.position = position; body.position = position; body.linearVelocity = Vector2.zero; Physics2D.SyncTransforms(); }
    static AttackResult Damage(PlayerHealth health, int amount)
    {
        var source = new GameObject("TutorialTestAttackSource"); source.transform.position = health.transform.position + Vector3.right;
        return health.ReceiveAttack(new AttackInfo(source, amount, Vector2.left));
    }
    static async Task Until(Func<bool> condition, string message, int ticks = 80)
    {
        for (int i = 0; i < ticks; i++) { if (condition()) return; await Task.Delay(50); }
        throw new Exception(message);
    }
    static async Task Reloaded(int oldId)
    {
        await Until(() => Flow() != null && Flow().GetInstanceID() != oldId && Get<PlayerHealth>(Flow(), "health") != null, "Scene reload did not complete");
        await Task.Delay(100);
        Check(Flow().State == RunState.Playing, "New scene starts Playing");
        Check(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "One EventSystem after reload");
        var player = Flow().Player;
        Check(player.GetComponent<PlayerHealth>().CurrentHealth == 100 && player.enabled && player.GetComponent<PlayerInputReader>().enabled && player.GetComponent<Rigidbody2D>().simulated && player.GetComponent<CapsuleCollider2D>().enabled, "Fresh player restores health, input and physics");
    }
    static UnityEngine.UI.Button Primary()
    { return Get<UnityEngine.UI.Button>(Get<TutorialHUD>(Flow(), "hud"), "primaryButton"); }
    static void CheckAndSubmitPrimary()
    {
        var button = Primary(); var rect = (RectTransform)button.transform;
        var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(EventSystem.current) { position = point };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button, "Primary button is reachable by pointer raycast");
        Check(EventSystem.current.currentSelectedGameObject == button.gameObject, "Primary button selected for keyboard/gamepad");
        Check(UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>().submit.action.enabled, "UI submit remains enabled with player input disabled");
        ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
    }
    static async Task Capture(string file)
    {
        UnityEngine.Object.FindFirstObjectByType<TutorialCamera>().Snap();
        int frame = Time.frameCount;
        await Until(() => Time.frameCount >= frame + 3, "UI did not settle for capture");
        ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("tmp/" + file));
        await Task.Delay(150);
    }
    public static async Task<object> Main()
    {
        Check(EditorApplication.isPlaying, "Requires Play mode in Tutorial scene");
        Check(Flow() != null && Flow().gameObject.scene.name == "Tutorial", "Tutorial scene required");
        bool background = Application.runInBackground, paused = EditorApplication.isPaused;
        Application.runInBackground = true; EditorApplication.isPaused = false;
        Gamepad pad = null; Keyboard keyboard = null;
        try
        {
            await Task.Delay(150);
            var flow = Flow(); var player = flow.Player; var health = player.GetComponent<PlayerHealth>(); var reader = player.GetComponent<PlayerInputReader>(); var director = UnityEngine.Object.FindFirstObjectByType<TutorialDirector>();
            Check(RunSession.CheckpointOrder == 0 && flow.State == RunState.Playing && Regular().activeSelf && !Encounter().Started, "Initial run starts with regular enemies and dormant boss");
            Check(!string.IsNullOrWhiteSpace(reader.GetBindingLabel("Move")) && reader.GetBindingLabel("Move") != "Move", "Composite movement keys are shown");
            pad = InputSystem.AddDevice<Gamepad>(); keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueDeltaStateEvent(pad.leftStick, Vector2.right); InputSystem.Update();
            Check(reader.BindingGroup == "Gamepad" && !string.IsNullOrWhiteSpace(reader.GetBindingLabel("Dodge")), "Gamepad binding hints");
            InputSystem.QueueDeltaStateEvent(pad.leftStick, Vector2.zero); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); InputSystem.Update();
            Check(reader.BindingGroup == "Keyboard&Mouse" && reader.GetBindingLabel("Jump").Contains("Space"), "Keyboard binding hints");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); reader.Clear();
            director.EnterLesson(TutorialLesson.Movement); Teleport(player, new Vector3(2.5f, .675f)); Invoke(director, "Update"); Check(RunSession.IsComplete(TutorialLesson.Movement), "Movement lesson completes");
            director.EnterLesson(TutorialLesson.Jump); player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.up * 3; player.GetComponent<PlayerMovement2D>().UpdateGroundedState(); Invoke(director, "Update"); Check(RunSession.IsComplete(TutorialLesson.Jump), "Jump lesson completes");
            player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero; player.GetComponent<PlayerMovement2D>().UpdateGroundedState();
            director.EnterLesson(TutorialLesson.Attack); Check(player.TryAttack(), "Attack starts"); Invoke(director, "Update"); player.EndAttack(); Check(RunSession.IsComplete(TutorialLesson.Attack), "Attack lesson completes");
            director.EnterLesson(TutorialLesson.Dodge); typeof(PlayerInputReader).GetField("dodgeRequested", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(reader, true); Invoke(player, "FixedUpdate");
            Check(Damage(health, 10) == AttackResult.Evaded && RunSession.IsComplete(TutorialLesson.Dodge), "Actual evasion completes dodge lesson");
            player.GetComponent<PlayerDodge>().Tick(1); Invoke(player, "FixedUpdate");
            director.EnterLesson(TutorialLesson.Parry); player.GetComponent<PlayerCombat>().FaceAction(1); Check(player.TryParry(), "Parry starts");
            Check(Damage(health, 10) == AttackResult.Parried && RunSession.IsComplete(TutorialLesson.Parry), "Actual parry completes lesson");
            Check(health.CurrentHealth == 100, "Defensive lessons preserve health");
            int initialPlayer = player.GetInstanceID(), oldFlow = flow.GetInstanceID();
            Check(Damage(health, 1000) == AttackResult.Hit, "Lethal hit");
            await Until(() => Flow().State == RunState.Dead, "Death menu not shown");
            Check(!reader.enabled && player.State == PlayerActionState.Dead && Primary().gameObject.activeInHierarchy, "Death animation and menu without player input");
            await Capture("TutorialDeath.png");
            Check(flow.Retry() && !flow.Retry(), "Retry accepts only one load request"); await Reloaded(oldFlow);
            flow = Flow(); player = flow.Player; health = player.GetComponent<PlayerHealth>();
            Check(player.GetInstanceID() != initialPlayer && Mathf.Abs(player.transform.position.x - .5f) < .1f && Regular().activeSelf && RunSession.CheckpointOrder == 0, "Before-checkpoint retry restores start and regular enemies");
            Check(RunSession.IsComplete(TutorialLesson.Parry), "Learned actions survive retry");
            Damage(health, 10); Teleport(player, new Vector3(30.5f, .675f));
            await Until(() => RunSession.CheckpointOrder == 1, "Checkpoint trigger did not activate");
            Check(health.CurrentHealth == 100 && !Regular().activeSelf && RunSession.CheckpointId == "boss", "Checkpoint heals and retires passed enemies");
            var checkpoint = UnityEngine.Object.FindFirstObjectByType<Checkpoint>();
            Damage(health, 1); Check(!checkpoint.TryActivate(player) && health.CurrentHealth == 99, "Checkpoint cannot be farmed for healing");
            await Capture("TutorialCheckpoint.png");
            Teleport(player, new Vector3(34.5f, .675f)); await Until(() => Encounter().Started, "Boss entrance trigger failed");
            Check(Encounter().Health.CurrentHealth == 240 && Get<GameObject>(Encounter(), "entranceGate").activeSelf, "Boss awakens at full health and closes entrance");
            await Task.Delay(200); await Capture("TutorialBoss.png");
            oldFlow = flow.GetInstanceID(); Damage(health, 1000); await Until(() => Flow().State == RunState.Dead, "Boss death menu missing");
            CheckAndSubmitPrimary(); Check(flow.State == RunState.Loading && !flow.Retry(), "UI submit starts one retry"); await Reloaded(oldFlow);
            flow = Flow(); player = flow.Player;
            Check(Mathf.Abs(player.transform.position.x - 30.5f) < .1f && !Regular().activeSelf && !Encounter().Started && !Get<GameObject>(Encounter(), "entranceGate").activeSelf, "After-checkpoint retry restores safe spawn and resets arena");
            Check(RunSession.IsComplete(TutorialLesson.Dodge) && RunSession.IsComplete(TutorialLesson.Parry), "Lessons remain complete after checkpoint retry");
            Teleport(player, new Vector3(34.5f, .675f)); await Until(() => Encounter().Started, "Boss cannot be reentered");
            Check(Encounter().Health.CurrentHealth == 240, "Boss health resets on retry");
            var bossHealth = Encounter().Health; var source = new GameObject("TutorialBossFinishSource"); source.transform.position = bossHealth.transform.position + Vector3.right;
            Check(bossHealth.ReceiveAttack(new AttackInfo(source, 1000, Vector2.left)) == AttackResult.Hit, "Boss kill");
            Check(flow.State == RunState.Cleared && Encounter().Completed && !Get<GameObject>(Encounter(), "entranceGate").activeSelf && !flow.Retry() && RunSession.IsComplete(TutorialLesson.Boss), "Boss kill opens gate and shows terminal clear state");
            await Capture("TutorialClear.png");
            oldFlow = flow.GetInstanceID(); CheckAndSubmitPrimary(); await Reloaded(oldFlow);
            Check(RunSession.CheckpointOrder == 0 && !RunSession.IsComplete(TutorialLesson.Parry) && Regular().activeSelf && Mathf.Abs(Flow().Player.transform.position.x - .5f) < .1f && !Encounter().Started, "Clear restart resets checkpoint, lessons and enemies");
            return new { passed = true, checks = "keyboard/gamepad labels, movement/jump/attack learning, actual dodge/parry learning, death UI, single retry request, start respawn, checkpoint trigger/heal/anti-farm, boss entrance/gates, checkpoint respawn, boss reset, pointer raycast/UI submit, clear/restart, repeated scene reload without duplicate EventSystems" };
        }
        finally
        {
            if (pad != null) InputSystem.RemoveDevice(pad); if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            Application.runInBackground = background; EditorApplication.isPaused = paused;
        }
    }
}
