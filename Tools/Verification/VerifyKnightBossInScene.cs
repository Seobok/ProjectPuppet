using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
public static class VerifyKnightBossInScene
{
    public static async Task<object> Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Requires Play mode");
        var boss = UnityEngine.Object.FindFirstObjectByType<BossController>(); var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (boss == null || player == null) throw new Exception("Requires BossTest scene");
        var combat = boss.GetComponent<BossCombat>(); var health = player.GetComponent<PlayerHealth>(); var input = player.GetComponent<PlayerInputReader>();
        bool background = Application.runInBackground, paused = EditorApplication.isPaused, inputEnabled = input.enabled;
        Application.runInBackground = true; EditorApplication.isPaused = false; input.enabled = false;
        var patterns = new HashSet<BossPattern>(); float startX = boss.transform.position.x; int startHealth = health.CurrentHealth;
        try
        {
            bool guarded = false;
            for (int i = 0; i < 50; i++)
            {
                await Task.Delay(100); if (combat.Pattern != BossPattern.None) patterns.Add(combat.Pattern);
                if (boss.State == BossActionState.Guard && health.CurrentHealth < startHealth) { guarded = true; break; }
            }
            if (!guarded || !patterns.Contains(BossPattern.Heavy) || Mathf.Abs(boss.transform.position.x - startX) < .5f) throw new Exception("Heavy did not advance, hit, recover and enter guard");
            var attack = player.GetComponent<PlayerCombat>(); attack.FaceAction(boss.transform.position.x - player.transform.position.x);
            if (!player.TryAttack()) throw new Exception("Player could not attack guard");
            attack.BeginHitbox(); attack.TickHitbox();
            if (player.State != PlayerActionState.Recoil || boss.State != BossActionState.ParrySuccess) throw new Exception("Guard did not parry actual player swing");
            for (int i = 0; i < 55; i++)
            {
                await Task.Delay(100); if (combat.Pattern != BossPattern.None) patterns.Add(combat.Pattern);
                if (patterns.Contains(BossPattern.Combo) && combat.StepIndex == 1 && combat.Phase == EnemyAttackPhase.Recovery) break;
            }
            if (!patterns.Contains(BossPattern.Counter) || !patterns.Contains(BossPattern.Combo)) throw new Exception("Automatic counter and close combo were not observed");
            if (boss.GetComponent<EnemyHealth>().IsDead) throw new Exception("Boss unexpectedly died");
            if (health.CurrentHealth >= startHealth) throw new Exception("No automatic damage");
            return new { passed = true, patterns = "Heavy, Counter, Combo", startX, endX = boss.transform.position.x, playerHealthBefore = startHealth, playerHealthAfter = health.CurrentHealth, phase = combat.Phase.ToString() };
        }
        finally { input.enabled = inputEnabled; Application.runInBackground = background; EditorApplication.isPaused = paused; }
    }
}
