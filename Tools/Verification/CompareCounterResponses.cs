using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public static class CompareCounterResponses
{
    private static void Set(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);
    public static object Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Requires Play mode");
        int stand = Run("stand"), walk = Run("walk"), dodge = Run("dodge"), parry = Run("parry");
        if (stand != 90 || walk != 90 || dodge != 100 || parry != 100) throw new Exception("Unexpected responses: " + stand + "/" + walk + "/" + dodge + "/" + parry);
        return new { stationaryHealth = stand, walkingHealth = walk, dodgeHealth = dodge, bufferedParryHealth = parry };
    }

    private static int Run(string response)
    {
        GameObject enemy = null, player = null, floor = null;
        try
        {
            floor = new GameObject("CounterResponseFloor"); floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = new Vector3(10000, 9999.5f); floor.AddComponent<BoxCollider2D>().size = new Vector2(20, 1);
            player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab"), new Vector3(9999.2f, 10000.675f), Quaternion.identity);
            enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ArmedHumanoid.prefab"), new Vector3(10000, 9999.9535f), Quaternion.identity);
            var pc = player.GetComponent<PlayerController>();
            var pm = player.GetComponent<PlayerMovement2D>();
            var dodge = player.GetComponent<PlayerDodge>();
            var input = player.GetComponent<PlayerInputReader>(); input.enabled = false;
            var body = player.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic;
            var ai = enemy.GetComponent<EnemyController>(); Set(ai, "target", pc);
            var animator = enemy.GetComponentInChildren<Animator>();
            var tick = typeof(PlayerController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
            Physics2D.SyncTransforms(); ai.Tick(0.02f); animator.Update(0f);
            if (!pc.TryAttack()) throw new Exception("Could not begin player attack");
            var attack = player.GetComponent<PlayerCombat>(); attack.BeginHitbox(); attack.TickHitbox(); animator.Update(0f);
            if (pc.State != PlayerActionState.Recoil) throw new Exception("Swing was not parried");
            for (int frame = 0; frame < 25; frame++)
            {
                if (response == "dodge" && frame == 10) Set(input, "dodgeRequested", true);
                if (response == "parry" && frame == 9) Set(input, "parryRequested", true);
                tick.Invoke(pc, null);
                bool canMove = pc.State == PlayerActionState.Normal;
                pm.Tick(response == "walk" ? -1f : 0f, false, canMove, pc.State == PlayerActionState.Dodge, dodge.HorizontalVelocity);
                player.transform.position += Vector3.right * body.linearVelocity.x * 0.02f;
                Physics2D.SyncTransforms(); ai.Tick(0.02f);
                animator.Update(0.02f); animator.Update(0f); animator.Update(0f);
            }
            return player.GetComponent<PlayerHealth>().CurrentHealth;
        }
        finally
        {
            if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
            if (player != null) UnityEngine.Object.DestroyImmediate(player);
            if (floor != null) UnityEngine.Object.DestroyImmediate(floor);
        }
    }
}
