using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public static class VerifyEnemyCounter
{
    private static void Check(bool v, string message) { if (!v) throw new Exception(message); }
    private static void Set(object o, string field, object value) => o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);
    private static void Advance(Animator a, float dt) { a.Update(dt); a.Update(0f); a.Update(0f); }
    public static object Main()
    {
        Check(EditorApplication.isPlaying, "Requires Play mode");
        GameObject enemy = null, player = null, floor = null;
        try
        {
            floor = new GameObject("CounterTestFloor"); floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = new Vector3(10000, 9999.5f); floor.AddComponent<BoxCollider2D>().size = new Vector2(20, 1);
            player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab"), new Vector3(9999.2f, 10000.675f), Quaternion.identity);
            var pc = player.GetComponent<PlayerController>(); pc.enabled = false;
            player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            EnemyController ai = null; EnemyCombat combat = null; EnemyHealth health = null; Animator animator = null;
            Action reset = () =>
            {
                if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
                player.transform.position = new Vector3(9999.2f, 10000.675f);
                enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ArmedHumanoid.prefab"), new Vector3(10000, 9999.9535f), Quaternion.identity);
                ai = enemy.GetComponent<EnemyController>(); combat = enemy.GetComponent<EnemyCombat>();
                health = enemy.GetComponent<EnemyHealth>(); animator = enemy.GetComponentInChildren<Animator>();
                Set(ai, "target", pc); Physics2D.SyncTransforms(); ai.Tick(0.02f); Advance(animator, 0f);
                Check(enemy.GetComponent<EnemyGuard>().IsGuarding, "Fixture starts guarding");
            };
            Action parry = () =>
            {
                Check(health.ReceiveAttack(new AttackInfo(player, 1, Vector2.right)) == AttackResult.Parried, "Arm counter by frontal parry");
                Advance(animator, 0f);
            };
            reset();
            Set(pc, "<State>k__BackingField", PlayerActionState.Attack);
            var playerCombat = player.GetComponent<PlayerCombat>();
            playerCombat.BeginAttack(); playerCombat.BeginHitbox(); playerCombat.TickHitbox();
            Advance(animator, 0f);
            Check(pc.State == PlayerActionState.Recoil && health.CurrentHealth == 30, "Actual swing enters recoil without enemy damage");
            ai.Tick(0.08f); Advance(animator, 0.08f);
            Check(!combat.IsAttacking && ai.State == EnemyActionState.ParrySuccess && player.GetComponent<PlayerHealth>().CurrentHealth == 100, "No premature counter during parry success");
            pc.enabled = true;
            var playerTick = typeof(PlayerController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < 13; i++) playerTick.Invoke(pc, null);
            Check(pc.State == PlayerActionState.Normal && pc.CanAttack, "Player recoil ends before counter strike");
            ai.Tick(0.03f); Advance(animator, 0.02f);
            Check(combat.IsCounterAttack && combat.Phase == EnemyAttackPhase.Prepare && !enemy.GetComponent<EnemyGuard>().IsGuarding, "Counter starts directly without guarding");
            Advance(animator, 0.15f); Check(player.GetComponent<PlayerHealth>().CurrentHealth == 100, "Counter preparation gives response time");
            Advance(animator, 0.07f); combat.Tick(0.02f); combat.Tick(0.02f);
            Check(player.GetComponent<PlayerHealth>().CurrentHealth == 90, "Counter hits once for 10");
            Advance(animator, 0.12f); Check(combat.Phase == EnemyAttackPhase.Recovery, "Counter has recovery");
            Advance(animator, 0.55f); Check(!combat.IsAttacking && !combat.IsCounterAttack, "Counter ends normally");
            pc.enabled = false;

            reset(); parry();
            player.transform.position = new Vector3(10000.8f, 10000.675f); Physics2D.SyncTransforms();
            ai.Tick(0.12f); Advance(animator, 0.12f);
            Check(combat.IsCounterAttack && enemy.GetComponent<EnemyMovement2D>().FacingDirection == -1, "Counter keeps original direction when player crosses behind");
            Advance(animator, 0.22f); combat.Tick(0.02f);
            Check(player.GetComponent<PlayerHealth>().CurrentHealth == 90, "Player behind counter takes no damage");
            Advance(animator, 0.12f); Advance(animator, 0.55f); Check(!combat.IsAttacking, "Miss still completes recovery");

            reset(); parry();
            health.ReceiveAttack(new AttackInfo(player, 1, Vector2.right)); ai.Tick(0.4f); ai.Tick(0.02f);
            Check(!combat.IsCounterAttack && ai.State == EnemyActionState.Guard, "Hurt cancels pending counter");
            parry(); ai.Tick(0.12f);
            Check(combat.IsCounterAttack, "Counter preparation starts");
            health.ReceiveAttack(new AttackInfo(player, 1, Vector2.right)); combat.BeginHitbox(); ai.Tick(0.4f);
            Check(!combat.IsAttacking && !combat.IsCounterAttack, "Hurt during preparation cancels counter");

            reset(); parry(); ai.enabled = false; ai.enabled = true; ai.Tick(0.4f);
            Check(!combat.IsCounterAttack && ai.State == EnemyActionState.Guard, "Disable clears pending counter");
            parry(); health.ReceiveAttack(new AttackInfo(player, 100, Vector2.right)); ai.Tick(1f);
            Check(health.IsDead && !combat.IsAttacking && ai.State == EnemyActionState.Dead, "Death cancels pending counter");
            return new { passed = true, counterHitDamage = 10, responseTimeSeconds = 0.3f, checks = "actual parry/recoil, direct counter, timing, direction lock, miss/recovery, pending/preparation hurt cancellation, disable/death cancellation" };
        }
        finally
        {
            if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
            if (player != null) UnityEngine.Object.DestroyImmediate(player);
            if (floor != null) UnityEngine.Object.DestroyImmediate(floor);
        }
    }
}
