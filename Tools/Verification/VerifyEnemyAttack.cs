using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public static class VerifyEnemyAttack
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Set(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);
    private static void Advance(Animator a, float time) { a.Update(time); a.Update(0f); a.Update(0f); }

    public static object Main()
    {
        Check(EditorApplication.isPlaying, "Requires Play mode");
        GameObject enemy = null, player = null, floor = null;
        try
        {
            floor = new GameObject("AttackTestFloor"); floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = new Vector3(10000, 9999.5f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(20, 1);
            enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ArmedHumanoid.prefab"), new Vector3(10000, 9999.9535f), Quaternion.identity);
            player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab"), new Vector3(10001, 10000.675f), Quaternion.identity);
            var pc = player.GetComponent<PlayerController>(); pc.enabled = false;
            player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var ai = enemy.GetComponent<EnemyController>();
            var combat = enemy.GetComponent<EnemyCombat>();
            var health = enemy.GetComponent<EnemyHealth>();
            var move = enemy.GetComponent<EnemyMovement2D>();
            var animator = enemy.GetComponentInChildren<Animator>();
            var ph = player.GetComponent<PlayerHealth>();
            Set(ai, "target", pc);
            Physics2D.SyncTransforms(); ai.Tick(0.02f); Advance(animator, 0f);
            Check(ai.State == EnemyActionState.Guard, "Guard precedes basic attack");
            ai.Tick(0.6f); Advance(animator, 0f);
            Check(combat.Phase == EnemyAttackPhase.Prepare && animator.GetCurrentAnimatorStateInfo(0).IsName("AttackPrepare"), "AI starts preparation through Attack Trigger");
            Advance(animator, 0.3f);
            Check(ph.CurrentHealth == 100 && combat.Phase == EnemyAttackPhase.Prepare, "No early damage");
            Advance(animator, 0.07f);
            Check(combat.Phase == EnemyAttackPhase.Active && ph.CurrentHealth == 90, "Animation event activates hitbox and applies damage: phase=" + combat.Phase + " hp=" + ph.CurrentHealth + " state=" + animator.GetCurrentAnimatorStateInfo(0).fullPathHash);
            combat.Tick(0.02f); combat.Tick(0.02f); combat.Tick(0.02f);
            Check(ph.CurrentHealth == 90, "One hit per swing");
            int facing = move.FacingDirection;
            player.transform.position = new Vector3(9999, 10000.675f); Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(move.FacingDirection == facing && enemy.GetComponent<Rigidbody2D>().linearVelocity.x == 0f, "Direction and movement locked during attack");
            Advance(animator, 0.12f);
            Check(combat.Phase == EnemyAttackPhase.Recovery, "Hitbox ends before recovery");
            ai.Tick(0.02f); Check(combat.Phase == EnemyAttackPhase.Recovery && move.FacingDirection == facing, "No immediate reattack or turn during recovery");
            Advance(animator, 0.55f);
            Check(!combat.IsAttacking && !animator.GetBool("IsAttacking"), "Recovery end event completes attack");

            // Preparation can be interrupted, and late animation events cannot reopen a cancelled hitbox.
            ai.Tick(0.02f); ai.Tick(0.6f); Check(combat.IsAttacking && move.FacingDirection == -1, "Next attack can face the other way");
            health.ReceiveAttack(new AttackInfo(player, 1, Vector2.right));
            combat.BeginHitbox(); combat.EndHitbox();
            Advance(animator, 0f);
            Check(!combat.IsAttacking && ai.IsHurt && animator.GetCurrentAnimatorStateInfo(0).IsName("Hurt"), "Damage cancels attack and ignores late hitbox events");
            ai.Tick(0.5f); Advance(animator, 0.5f);

            // Dodge invulnerability uses the existing PlayerHealth path.
            move.Face(-1);
            pc.enabled = true;
            Set(pc, "<State>k__BackingField", PlayerActionState.Dodge);
            player.GetComponent<PlayerDodge>().BeginDodge(-1);
            Check(combat.BeginAttack(), "Start dodge test");
            combat.BeginHitbox(); combat.Tick(0.02f);
            Check(ph.CurrentHealth == 90, "Dodge evades enemy attack");
            combat.CancelAttack(); pc.enabled = false;

            // An attack that misses still finishes; a missing event cannot leave it stuck.
            player.transform.position = new Vector3(9997, 10000.675f); Physics2D.SyncTransforms();
            Check(combat.BeginAttack(), "Start miss test"); combat.BeginHitbox(); combat.EndHitbox(); combat.EndAttack();
            Check(ph.CurrentHealth == 90 && !combat.IsAttacking, "Miss causes no damage and finishes");
            Check(combat.BeginAttack(), "Start timeout test"); combat.Tick(10f);
            Check(!combat.IsAttacking, "Missing event watchdog");

            Check(combat.BeginAttack(), "Start active interruption test"); combat.BeginHitbox();
            health.ReceiveAttack(new AttackInfo(player, 1, Vector2.right));
            Check(!combat.IsAttacking, "Damage cancels active hitbox");
            Check(combat.BeginAttack(), "Start death cancellation test");
            health.ReceiveAttack(new AttackInfo(player, 100, Vector2.right)); combat.BeginHitbox();
            Check(health.IsDead && !combat.IsAttacking && !animator.GetBool("IsAttacking"), "Death permanently cancels attack");
            return new { passed = true, checks = "AI start, animation event timing, one hit per swing, direction lock, recovery, hurt cancellation, dodge, miss, watchdog, death" };
        }
        finally
        {
            if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
            if (player != null) UnityEngine.Object.DestroyImmediate(player);
            if (floor != null) UnityEngine.Object.DestroyImmediate(floor);
        }
    }
}
