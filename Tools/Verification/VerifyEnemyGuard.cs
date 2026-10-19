using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public static class VerifyEnemyGuard
{
    private static void Check(bool v, string m) { if (!v) throw new Exception(m); }
    private static void Set(object o, string field, object value) => o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, value);
    private static void Advance(Animator a, float dt) { a.Update(dt); a.Update(0f); a.Update(0f); }
    public static object Main()
    {
        Check(EditorApplication.isPlaying, "Requires Play mode");
        GameObject enemy = null, player = null, floor = null;
        try
        {
            floor = new GameObject("GuardTestFloor"); floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = new Vector3(10000, 9999.5f); floor.AddComponent<BoxCollider2D>().size = new Vector2(20, 1);
            enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ArmedHumanoid.prefab"), new Vector3(10000, 9999.9535f), Quaternion.identity);
            player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab"), new Vector3(9999.2f, 10000.675f), Quaternion.identity);
            var pc = player.GetComponent<PlayerController>(); pc.enabled = false;
            player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var ai = enemy.GetComponent<EnemyController>();
            var guard = enemy.GetComponent<EnemyGuard>();
            var health = enemy.GetComponent<EnemyHealth>();
            var combat = enemy.GetComponent<EnemyCombat>();
            var movement = enemy.GetComponent<EnemyMovement2D>();
            var animator = enemy.GetComponentInChildren<Animator>();
            Set(ai, "target", pc);
            Physics2D.SyncTransforms(); ai.Tick(0.02f); Advance(animator, 0f);
            Check(guard.IsGuarding && ai.State == EnemyActionState.Guard && animator.GetCurrentAnimatorStateInfo(0).IsName("Guard"), "Guard stance before attack");
            int damaged = 0; health.Damaged += () => damaged++;
            Set(pc, "<State>k__BackingField", PlayerActionState.Attack);
            var playerCombat = player.GetComponent<PlayerCombat>();
            playerCombat.BeginAttack(); playerCombat.BeginHitbox(); playerCombat.TickHitbox(); playerCombat.TickHitbox();
            Advance(animator, 0f);
            Check(health.CurrentHealth == 30 && damaged == 0 && pc.State == PlayerActionState.Recoil, "Actual player swing parried without damage and enters recoil");
            Check(!guard.IsGuarding && ai.State == EnemyActionState.ParrySuccess && animator.GetCurrentAnimatorStateInfo(0).IsName("ParrySuccess"), "Parry success animation and no repeated parry");
            Check(health.ReceiveAttack(new AttackInfo(player, 1, Vector2.right)) == AttackResult.Hit && health.CurrentHealth == 29, "Hit during parry success is not parried");
            ai.Tick(0.4f); Advance(animator, 0.4f); ai.Tick(0.02f);
            Check(guard.IsGuarding && movement.FacingDirection == -1, "Guard resumes after hurt");
            player.transform.position = new Vector3(10000.8f, 10000.675f); Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(guard.IsGuarding && movement.FacingDirection == -1, "Guard facing stays locked when player passes behind");
            Check(health.ReceiveAttack(new AttackInfo(player, 1, Vector2.left)) == AttackResult.Hit && !guard.IsGuarding, "Rear hit damages and clears guard");
            ai.Tick(0.4f); Advance(animator, 0.4f); ai.Tick(0.02f);
            Check(guard.IsGuarding && movement.FacingDirection == 1, "Right facing guard");
            Check(health.ReceiveAttack(new AttackInfo(player, 1, Vector2.left)) == AttackResult.Parried, "Right side frontal parry");
            ai.Tick(0.3f); Advance(animator, 0.3f); ai.Tick(0.02f);
            Check(combat.IsCounterAttack, "Parry success flows directly to counterattack");
            health.ReceiveAttack(new AttackInfo(player, 1, Vector2.left));
            ai.Tick(0.4f); Advance(animator, 0.4f); ai.Tick(0.02f);
            player.transform.position = new Vector3(10003, 10000.675f); Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(!guard.IsGuarding && movement.IsMoving, "Leave guard to chase distant player");
            player.transform.position = new Vector3(10000.8f, 10000.675f); Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(guard.IsGuarding, "Guard reentry"); ai.Tick(0.6f);
            Check(combat.Phase == EnemyAttackPhase.Prepare && !guard.IsGuarding, "Guard expires into basic attack");
            Check(health.ReceiveAttack(new AttackInfo(player, 1, Vector2.left)) == AttackResult.Hit && !combat.IsAttacking, "Preparation vulnerable");
            ai.Tick(0.4f); ai.Tick(0.02f); ai.Tick(0.6f);
            combat.BeginHitbox(); combat.EndHitbox();
            Check(combat.Phase == EnemyAttackPhase.Recovery, "Recovery reached");
            Check(health.ReceiveAttack(new AttackInfo(player, 1, Vector2.left)) == AttackResult.Hit, "Recovery vulnerable");
            ai.Tick(0.4f); ai.Tick(0.02f);
            player.transform.position = new Vector3(10000, 10000.675f); Physics2D.SyncTransforms();
            Check(health.ReceiveAttack(new AttackInfo(player, 1, Vector2.left)) == AttackResult.Hit, "Overlapping attacker is not frontal");
            health.ReceiveAttack(new AttackInfo(player, 100, Vector2.left)); Advance(animator, 0f);
            Check(health.IsDead && !guard.IsGuarding && !animator.GetBool("IsGuarding") && animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), "Death clears guard and wins animation priority");
            return new { passed = true, checks = "guard animation, actual swing parry and recoil, both front directions, rear damage, facing lock, no repeated parry, chase exit, preparation/recovery vulnerability, overlap, death" };
        }
        finally
        {
            if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
            if (player != null) UnityEngine.Object.DestroyImmediate(player);
            if (floor != null) UnityEngine.Object.DestroyImmediate(floor);
        }
    }
}
