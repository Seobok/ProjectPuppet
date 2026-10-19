using System;
using System.Threading.Tasks;
using UnityEngine;

public static class VerifyEnemyApproachInScene
{
    public static async Task<object> Main()
    {
        var enemy = UnityEngine.Object.FindFirstObjectByType<EnemyController>();
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (enemy == null || player == null) throw new Exception("Requires enemy and player in scene.");
        bool background = Application.runInBackground;
        Application.runInBackground = true;
        float start = enemy.transform.position.x;
        try
        {
            await Task.Delay(2000);
            float distance = Mathf.Abs(enemy.transform.position.x - player.transform.position.x);
            float expected = enemy.GetComponent<EnemyCombat>().AttackDistance;
            if (distance < expected - 0.05f || distance > expected + 0.05f || enemy.GetComponent<EnemyMovement2D>().IsMoving)
                throw new Exception("Enemy did not stop at desired distance: " + distance);
            return new { passed = true, startX = start, endX = enemy.transform.position.x, stoppedDistance = distance };
        }
        finally { Application.runInBackground = background; }
    }
}
