using System;
using System.Threading.Tasks;
using UnityEngine;

public static class VerifyEnemyAttackInScene
{
    public static async Task<object> Main()
    {
        var enemy = UnityEngine.Object.FindFirstObjectByType<EnemyController>();
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        if (enemy == null || player == null) throw new Exception("Requires enemy and player.");
        float startX = enemy.transform.position.x;
        int startHp = player.CurrentHealth;
        bool background = Application.runInBackground;
        Application.runInBackground = true;
        try
        {
            await Task.Delay(1600);
            if (player.CurrentHealth >= startHp) throw new Exception("Automatic attack did not damage player.");
            if (enemy.GetComponent<EnemyHealth>().IsDead) throw new Exception("Enemy unexpectedly died.");
            return new { passed = true, startX, endX = enemy.transform.position.x, playerHealthBefore = startHp, playerHealthAfter = player.CurrentHealth, phase = enemy.GetComponent<EnemyCombat>().Phase.ToString() };
        }
        finally { Application.runInBackground = background; }
    }
}
