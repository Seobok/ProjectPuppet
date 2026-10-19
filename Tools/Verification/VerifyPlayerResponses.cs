using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;

public static class VerifyPlayerResponses
{
    private static void Check(bool v, string message) { if (!v) throw new Exception(message); }
    private static void Set(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);
    private static void Tick(PlayerController pc) => typeof(PlayerController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pc, null);
    private static void Anim(Animator a) { a.Update(0f); a.Update(0f); }
    public static object Main()
    {
        Check(EditorApplication.isPlaying, "Requires Play mode");
        GameObject player = null, enemy = null, floor = null;
        Gamepad pad = null;
        var oldFocus = InputSystem.settings.editorInputBehaviorInPlayMode;
        var oldBackground = InputSystem.settings.backgroundBehavior;
        try
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            pad = InputSystem.AddDevice<Gamepad>();
            floor = new GameObject("PlayerResponseFloor"); floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = new Vector3(10000, 9999.5f); floor.AddComponent<BoxCollider2D>().size = new Vector2(20, 1);
            PlayerController pc = null; PlayerHealth ph = null; PlayerParry parry = null; PlayerInputReader input = null;
            EnemyController ai = null; EnemyCombat ec = null; EnemyHealth eh = null;
            Action reset = () =>
            {
                if (player != null) UnityEngine.Object.DestroyImmediate(player);
                if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
                InputSystem.QueueStateEvent(pad, new GamepadState()); InputSystem.Update();
                player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab"), new Vector3(9999.2f, 10000.675f), Quaternion.identity);
                enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ArmedHumanoid.prefab"), new Vector3(10000, 9999.9535f), Quaternion.identity);
                pc = player.GetComponent<PlayerController>(); ph = player.GetComponent<PlayerHealth>(); parry = player.GetComponent<PlayerParry>(); input = player.GetComponent<PlayerInputReader>();
                var actions = (InputActionAsset)typeof(PlayerInputReader).GetField("runtimeActions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(input);
                actions.devices = new InputDevice[] { pad };
                actions.Disable(); actions.FindActionMap("Player", true).Enable();
                player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                ai = enemy.GetComponent<EnemyController>(); ec = enemy.GetComponent<EnemyCombat>(); eh = enemy.GetComponent<EnemyHealth>();
                Set(ai, "target", pc); Physics2D.SyncTransforms(); player.GetComponent<PlayerMovement2D>().UpdateGroundedState();
                enemy.GetComponent<EnemyMovement2D>().Face(-1);
            };
            reset();
            Check(pc.TryParry(), "Start parry"); Anim(player.GetComponentInChildren<Animator>());
            Check(player.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Parry"), "Parry pose");
            Check(ec.BeginAttack(), "Enemy attack"); ec.BeginHitbox();
            Check(ph.CurrentHealth == 100 && pc.CanAttack && ai.State == EnemyActionState.Parried && !ec.IsAttacking, "Player parry cancels and staggers enemy without damage");
            Anim(player.GetComponentInChildren<Animator>());
            Check(player.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("ParrySuccess"), "Success pose");
            Check(pc.TryAttack(), "Immediate followup attack");
            player.GetComponent<PlayerCombat>().BeginHitbox(); player.GetComponent<PlayerCombat>().TickHitbox();
            Check(eh.CurrentHealth == 20, "Followup hits staggered enemy");

            reset(); player.GetComponentInChildren<SpriteRenderer>().flipX = true;
            Check(pc.TryParry() && ec.BeginAttack(), "Rear test setup"); ec.BeginHitbox();
            Check(ph.CurrentHealth == 90 && pc.State == PlayerActionState.Hurt && !parry.IsParrying, "Rear attack bypasses parry");
            reset(); Check(pc.TryParry(), "Failure setup");
            for (int i = 0; i < 7; i++) Tick(pc);
            Check(!parry.IsWindowOpen && pc.State == PlayerActionState.Parry && !pc.TryAttack() && !pc.TryParry(), "Failed parry has recovery and cannot be spammed");
            Check(ec.BeginAttack(), "Late hit setup"); ec.BeginHitbox();
            Check(ph.CurrentHealth == 90, "Late attack deals damage");
            reset(); Check(pc.TryParry(), "Expire setup");
            for (int i = 0; i < 16; i++) Tick(pc);
            Check(pc.State == PlayerActionState.Normal && !parry.IsParrying, "Failed recovery expires");

            // Forward dodge crosses the enemy; a buffered attack uses the requested return direction.
            reset();
            player.transform.position = new Vector3(9998.8f, 10000.675f); Physics2D.SyncTransforms();
            Check(ec.BeginAttack(), "Dodge flow attack");
            Set(input, "dodgeRequested", true); Tick(pc);
            Check(pc.State == PlayerActionState.Dodge && pc.IsInvulnerable && !pc.TryParry(), "Dodge and parry do not overlap");
            var body = player.GetComponent<Rigidbody2D>();
            int frames = 0;
            while (pc.State == PlayerActionState.Dodge && frames++ < 30)
            {
                player.transform.position += Vector3.right * body.linearVelocity.x * 0.02f; Physics2D.SyncTransforms();
                if (frames == 2) { ec.BeginHitbox(); Check(ph.CurrentHealth == 100, "Dodge evades active strike"); ec.EndHitbox(); }
                if (player.GetComponent<PlayerDodge>().Remaining <= 0.09f)
                {
                    Set(input, "attackRequested", true);
                }
                Tick(pc);
            }
            Check(pc.State == PlayerActionState.Attack, "Dodge buffer starts followup attack");
            player.GetComponent<PlayerCombat>().FaceAction(-1f);
            Check(player.GetComponentInChildren<SpriteRenderer>().flipX, "Action can face back toward enemy");
            Check(body.position.x > enemy.transform.position.x && ec.Phase == EnemyAttackPhase.Recovery, "Behind enemy during recovery");
            player.GetComponent<PlayerCombat>().BeginHitbox(); player.GetComponent<PlayerCombat>().TickHitbox();
            Check(eh.CurrentHealth == 20 && ph.CurrentHealth == 100, "Dodge into rear followup deals damage during recovery: enemyHP=" + eh.CurrentHealth + " playerHP=" + ph.CurrentHealth + " x=" + body.position.x);

            reset(); Check(pc.TryParry(), "Disable test");
            parry.enabled = false; Tick(pc);
            Check(!parry.IsParrying && pc.State == PlayerActionState.Normal, "Disabled parry exits cleanly");
            return new { passed = true, checks = "front parry/stagger/followup, rear hit, failed recovery, late hit, dodge invulnerability, buffered rear attack with direction change, disable cleanup" };
        }
        finally
        {
            if (player != null) UnityEngine.Object.DestroyImmediate(player);
            if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
            if (floor != null) UnityEngine.Object.DestroyImmediate(floor);
            if (pad != null) InputSystem.RemoveDevice(pad);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldFocus;
            InputSystem.settings.backgroundBehavior = oldBackground;
        }
    }
}
