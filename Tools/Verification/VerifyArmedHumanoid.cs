using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public static class VerifyArmedHumanoid
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    public static object Main()
    {
        Check(EditorApplication.isPlaying, "Requires Play mode.");
        // Scene objects can initialize in a different order than runtime Instantiate.
        foreach (var placed in UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
        {
            var placedAnimator = placed.GetComponentInChildren<Animator>();
            Check(placed.CurrentHealth == placed.MaxHealth && !placed.IsDead, "Placed enemy starts alive");
            Check(!placedAnimator.GetBool("IsDead") && !placedAnimator.GetCurrentAnimatorStateInfo(0).IsName("Death"), "Placed enemy is alive, not Death");
            Check(placed.GetComponent<Rigidbody2D>().simulated && placed.GetComponent<Collider2D>().enabled, "Placed enemy starts with active physics and collider");
        }
        GameObject enemy = null;
        GameObject player = null;
        GameObject repeatTarget = null;
        try
        {
            enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ArmedHumanoid.prefab"), new Vector3(10000.8f, 9999.28f), Quaternion.identity);
            player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab"), new Vector3(10000f, 10000f), Quaternion.identity);
            var controller = player.GetComponent<PlayerController>();
            controller.enabled = false;
            player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var combat = player.GetComponent<PlayerCombat>();
            var health = enemy.GetComponent<EnemyHealth>();
            var animator = enemy.GetComponentInChildren<Animator>();
            int hits = 0, deaths = 0;
            health.Damaged += () => hits++;
            health.Died += () => deaths++;
            Check(health.CurrentHealth == 30 && !health.IsDead, "Initial health");
            Check(health.ReceiveAttack(new AttackInfo(enemy, 10, Vector2.right)) == AttackResult.Ignored, "Self hit ignored");
            Check(health.ReceiveAttack(new AttackInfo(player, 0, Vector2.right)) == AttackResult.Ignored, "Zero damage ignored");
            Physics2D.SyncTransforms();
            for (int strike = 1; strike <= 3; strike++)
            {
                typeof(PlayerController).GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, PlayerActionState.Attack);
                combat.BeginAttack(); combat.BeginHitbox();
                combat.TickHitbox(); combat.TickHitbox(); combat.TickHitbox();
                combat.EndHitbox();
                Check(health.CurrentHealth == 30 - strike * 10, "Player attack must damage exactly once per swing: " + strike);
                animator.Update(0f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName(strike == 3 ? "Death" : "Hurt"), "Correct animation for strike " + strike);
                if (strike < 3)
                {
                    animator.Update(0.5f); animator.Update(0f);
                    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Hurt returns to Idle");
                }
                if (strike == 2) animator.SetTrigger("Hurt"); // A pending hit must not override death.
            }
            Check(health.IsDead && hits == 2 && deaths == 1, "Damage and death events");
            Check(!enemy.GetComponent<Rigidbody2D>().simulated && !enemy.GetComponent<Collider2D>().enabled, "Corpse collision and physics disabled");
            Check(health.ReceiveAttack(new AttackInfo(player, 10, Vector2.right)) == AttackResult.Ignored && deaths == 1, "Dead target ignored");
            animator.Update(2f);
            var lastSprite = enemy.GetComponentInChildren<SpriteRenderer>().sprite;
            animator.Update(2f);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Death") && lastSprite == enemy.GetComponentInChildren<SpriteRenderer>().sprite, "Death holds final sprite");
            Check(animator.GetBool("IsDead"), "Death bool is synchronized");
            animator.SetTrigger("Hurt"); animator.Update(0f); animator.Update(0.1f);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), "Hurt cannot escape Death");

            repeatTarget = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ArmedHumanoid.prefab"), new Vector3(11000, 11000), Quaternion.identity);
            var repeatHealth = repeatTarget.GetComponent<EnemyHealth>();
            var repeatAnimator = repeatTarget.GetComponentInChildren<Animator>();
            repeatHealth.ReceiveAttack(new AttackInfo(player, 1, Vector2.right));
            repeatAnimator.Update(0f); repeatAnimator.Update(0.1f);
            Check(repeatAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hurt") && repeatAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime > 0.1f, "Hurt progressed");
            repeatHealth.ReceiveAttack(new AttackInfo(player, 1, Vector2.right));
            repeatAnimator.Update(0f); repeatAnimator.Update(0f);
            Check(repeatAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hurt") && repeatAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.05f, "Repeated hit restarts Hurt");
            repeatHealth.ReceiveAttack(new AttackInfo(player, 100, Vector2.right));
            repeatAnimator.Update(0f); repeatAnimator.Update(0f);
            Check(repeatAnimator.GetCurrentAnimatorStateInfo(0).IsName("Death"), "Death interrupts Hurt immediately");
            return new { passed = true, playerHits = 3, damagePerSwing = 10, duplicateTicksPerSwing = 3, hurtEvents = hits, deathEvents = deaths, corpseHoldsFinalFrame = true, repeatedHurtRestarts = true, deathInterruptsHurt = true, deathCannotBeInterruptedByHurt = true };
        }
        finally
        {
            if (player != null) UnityEngine.Object.DestroyImmediate(player);
            if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
            if (repeatTarget != null) UnityEngine.Object.DestroyImmediate(repeatTarget);
        }
    }
}
