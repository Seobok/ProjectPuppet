using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
public static class VerifyMushroom
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
    static void Tick(PlayerController p) => typeof(PlayerController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(p, null);
    static void Anim(Animator a, float dt = 0) { a.Update(dt); a.Update(0); a.Update(0); }
    public static object Main()
    {
        Check(EditorApplication.isPlaying, "Requires Play mode");
        GameObject floor = null, enemy = null, player = null, wall = null;
        try
        {
            floor = new GameObject("MushroomVerificationFloor"); floor.layer = LayerMask.NameToLayer("Ground"); floor.transform.position = new Vector3(10000, 9999.5f);
            var floorCollider = floor.AddComponent<BoxCollider2D>(); floorCollider.size = new Vector2(30, 1);
            MushroomController ai = null; MushroomCombat combat = null; EnemyHealth health = null; EnemyMovement2D movement = null;
            PlayerController pc = null; PlayerHealth ph = null; Animator animator = null; Rigidbody2D body = null;
            Action<float> reset = x =>
            {
                if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy); if (player != null) UnityEngine.Object.DestroyImmediate(player);
                enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Mushroom.prefab"), new Vector3(10000, 9999.975f), Quaternion.identity);
                player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab"), new Vector3(10000 + x, 10000.675f), Quaternion.identity);
                ai = enemy.GetComponent<MushroomController>(); combat = enemy.GetComponent<MushroomCombat>(); health = enemy.GetComponent<EnemyHealth>(); movement = enemy.GetComponent<EnemyMovement2D>(); animator = enemy.GetComponentInChildren<Animator>(); body = enemy.GetComponent<Rigidbody2D>();
                pc = player.GetComponent<PlayerController>(); ph = player.GetComponent<PlayerHealth>();
                player.GetComponent<PlayerInputReader>().enabled = false; player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                Set(ai, "target", pc); Physics2D.SyncTransforms(); player.GetComponent<PlayerMovement2D>().UpdateGroundedState();
                animator.Rebind(); Anim(animator);
            };
            reset(3);
            Check(enemy.GetComponent<EnemyGuard>() == null && enemy.GetComponent<EnemyController>() == null, "Nonhuman has no guard or human controller");
            ai.Tick(.02f); Anim(animator);
            Check(ai.State == MushroomActionState.Approach && body.linearVelocity.x > 0 && animator.GetCurrentAnimatorStateInfo(0).IsName("Run"), "Approach and Run");
            Check(movement.FacingDirection == 1 && enemy.GetComponentInChildren<SpriteRenderer>().flipX, "Left-facing source is flipped for right approach");
            reset(-3); ai.Tick(.02f); Anim(animator);
            Check(movement.FacingDirection == -1 && body.linearVelocity.x < 0 && !enemy.GetComponentInChildren<SpriteRenderer>().flipX, "Left approach uses original left-facing sprite");
            reset(3); ai.Tick(.02f);
            player.transform.position += Vector3.right * 6; Physics2D.SyncTransforms(); ai.Tick(.02f);
            Check(!ai.IsTracking && body.linearVelocity.x == 0, "Lose distant target");
            reset(.8f); ai.Tick(.02f); Anim(animator);
            Check(combat.Phase == EnemyAttackPhase.Prepare && animator.GetCurrentAnimatorStateInfo(0).IsName("AttackPrepare"), "No guard: directly prepare");
            Anim(animator, .4f); Check(ph.CurrentHealth == 100 && combat.Phase == EnemyAttackPhase.Prepare, "Telegraph has no early hit");
            Anim(animator, .07f); Check(combat.Phase == EnemyAttackPhase.Active && ph.CurrentHealth == 90, "Animation event opens hitbox");
            combat.Tick(.02f); combat.Tick(.02f); Check(ph.CurrentHealth == 90 && body.linearVelocity.x > 0, "Single hit and forward lunge");
            Check(enemy.GetComponentInChildren<SpriteRenderer>().flipX && combat.HitboxCenter.x > enemy.transform.position.x, "Right lunge art and hitbox face right");
            player.transform.position = new Vector3(9999.2f, 10000.675f); Physics2D.SyncTransforms(); ai.Tick(.02f);
            Check(movement.FacingDirection == 1, "Active direction locked");
            Anim(animator, .24f); ai.Tick(.02f);
            Check(combat.Phase == EnemyAttackPhase.Recovery && movement.FacingDirection == 1 && body.linearVelocity.x == 0, "Recovery stops and cannot turn");
            Anim(animator, .7f); Check(!combat.IsAttacking, "Recovery event finishes attack");
            ai.Tick(.02f); Check(movement.FacingDirection == -1, "Turns only after recovery");
            Check(health.ReceiveAttack(new AttackInfo(player, 1, Vector2.right)) == AttackResult.Hit, "Player attack never parried");
            combat.BeginHitbox(); Anim(animator);
            Check(!combat.IsAttacking && ai.State == MushroomActionState.Hurt && animator.GetCurrentAnimatorStateInfo(0).IsName("Hurt"), "Damage interrupts prepare; stale event ignored");
            ai.Tick(.3f); Anim(animator, .3f); Check(ai.State == MushroomActionState.Idle, "Hurt recovers");

            reset(-.8f); movement.Face(-1);
            Check(!enemy.GetComponentInChildren<SpriteRenderer>().flipX, "Left strike uses original left-facing art");
            Check(pc.TryParry(), "Player parry starts"); Check(combat.BeginAttack(), "Parry attack starts"); combat.BeginHitbox(); Anim(animator);
            Check(ph.CurrentHealth == 100 && pc.CanAttack && ai.State == MushroomActionState.Parried && !combat.IsAttacking && animator.GetCurrentAnimatorStateInfo(0).IsName("Stun"), "Player parry staggers Mushroom and allows followup");
            Check(pc.TryAttack(), "Parry followup starts"); player.GetComponent<PlayerCombat>().BeginHitbox(); player.GetComponent<PlayerCombat>().TickHitbox();
            Check(health.CurrentHealth == 20, "Parry followup damages Mushroom");

            reset(-1.2f); movement.Face(-1); Check(combat.BeginAttack(), "Dodge attack starts");
            Set(player.GetComponent<PlayerInputReader>(), "dodgeRequested", true); Tick(pc);
            Check(pc.IsInvulnerable, "Dodge starts invulnerable");
            int frames = 0; bool crossed = false;
            while (pc.State == PlayerActionState.Dodge && frames++ < 30)
            {
                player.transform.position += Vector3.right * player.GetComponent<Rigidbody2D>().linearVelocity.x * .02f; Physics2D.SyncTransforms();
                if (frames == 2) { combat.BeginHitbox(); Check(ph.CurrentHealth == 100, "Dodge avoids strike"); combat.EndHitbox(); }
                if (player.GetComponent<PlayerDodge>().Remaining <= .09f) Set(player.GetComponent<PlayerInputReader>(), "attackRequested", true);
                crossed |= player.transform.position.x > enemy.transform.position.x;
                Tick(pc);
            }
            Check(crossed && pc.State == PlayerActionState.Attack && combat.Phase == EnemyAttackPhase.Recovery && movement.FacingDirection == -1, "Dodge crosses into rear and buffers attack during locked recovery");
            player.GetComponent<PlayerCombat>().FaceAction(-1); player.GetComponent<PlayerCombat>().BeginHitbox(); player.GetComponent<PlayerCombat>().TickHitbox();
            Check(health.CurrentHealth == 20 && ph.CurrentHealth == 100, "Rear followup damages Mushroom");

            reset(3); movement.Face(1);
            wall = new GameObject("MushroomVerificationWall"); wall.layer = floor.layer; wall.transform.position = new Vector3(10000.5f, 10000.5f); wall.AddComponent<BoxCollider2D>().size = new Vector2(.15f, 1);
            Physics2D.SyncTransforms(); ai.Tick(.02f); Check(body.linearVelocity.x == 0, "Approach stops at wall");
            Check(combat.BeginAttack(), "Wall lunge starts"); combat.BeginHitbox(); combat.Tick(.02f); Check(body.linearVelocity.x == 0, "Lunge stops at wall");
            combat.CancelAttack(); wall.SetActive(false); floorCollider.size = new Vector2(.7f, 1); Physics2D.SyncTransforms();
            ai.Tick(.02f); Check(body.linearVelocity.x == 0, "Approach stops at ledge");
            Check(combat.BeginAttack(), "Ledge lunge starts"); combat.BeginHitbox(); combat.Tick(.02f); Check(body.linearVelocity.x == 0, "Lunge stops at ledge");
            combat.CancelAttack(); floorCollider.size = new Vector2(30, 1); Physics2D.SyncTransforms();
            Check(combat.BeginAttack(), "Watchdog attack starts"); combat.Tick(10); Check(!combat.IsAttacking && body.linearVelocity.x == 0, "Missing animation event cancels attack");
            Check(combat.BeginAttack(), "Death attack starts"); health.ReceiveAttack(new AttackInfo(player, 1000, Vector2.left)); combat.BeginHitbox(); Anim(animator);
            Check(health.IsDead && !body.simulated && !combat.IsAttacking && animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), "Death cancels attack and collisions");
            Anim(animator, 3); Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Death") && !combat.BeginAttack(), "Death stays terminal");
            return new { passed = true, checks = "approach/run, target loss, no guard, animation event timing, single hit, lunge, locked facing/recovery, hurt cancellation/recovery, player parry/followup, dodge-through/rear followup, wall/ledge walking and lunge, timeout, death" };
        }
        finally
        {
            foreach (var obj in new[] { enemy, player, wall, floor }) if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
