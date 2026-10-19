using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public static class VerifyEnemyMovement
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Set(object o, string field, object value) => o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);
    public static object Main()
    {
        Check(EditorApplication.isPlaying, "Requires Play mode");
        GameObject enemy = null, player = null, floor = null, wall = null;
        try
        {
            floor = new GameObject("MovementTestFloor"); floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = new Vector3(10000, 9999.5f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(20, 1);
            enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ArmedHumanoid.prefab"), new Vector3(10000, 9999.9535f), Quaternion.identity);
            player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab"), new Vector3(10003, 10000.675f), Quaternion.identity);
            player.GetComponent<PlayerController>().enabled = false;
            player.GetComponent<Rigidbody2D>().simulated = false;
            var ai = enemy.GetComponent<EnemyController>(); ai.enabled = false;
            enemy.GetComponent<EnemyCombat>().enabled = false; // Exercise locomotion independently of attacks.
            var move = enemy.GetComponent<EnemyMovement2D>();
            var body = enemy.GetComponent<Rigidbody2D>();
            var animator = enemy.GetComponentInChildren<Animator>();
            Set(ai, "target", player.GetComponent<PlayerController>());
            Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(ai.IsTracking && body.linearVelocity.x > 0f && move.FacingDirection == 1, "Detect and approach right");
            animator.Update(0f); animator.Update(0f);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Run"), "Run animation");
            body.linearVelocity = new Vector2(body.linearVelocity.x, -2f); ai.Tick(0.02f);
            Check(body.linearVelocity.y == -2f, "Preserve vertical velocity");
            player.transform.position = new Vector3(10000.9f, 10000.675f); Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(body.linearVelocity.x == 0f, "Stop within range");
            player.transform.position = new Vector3(10001.1f, 10000.675f); Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(body.linearVelocity.x == 0f, "Hysteresis remains stopped");
            player.transform.position = new Vector3(10001.3f, 10000.675f); Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(body.linearVelocity.x > 0f, "Resume outside range");
            player.transform.position = new Vector3(9997, 10000.675f); Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(body.linearVelocity.x < 0f && move.FacingDirection == -1 && enemy.GetComponentInChildren<SpriteRenderer>().flipX, "Turn and approach left");
            player.transform.position = new Vector3(10008, 10000.675f); Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(!ai.IsTracking && body.linearVelocity.x == 0f, "Lose distant target");
            player.transform.position = new Vector3(10006, 10000.675f); Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(!ai.IsTracking, "Do not acquire outside detection");
            player.transform.position = new Vector3(10003, 10003); Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(!ai.IsTracking && body.linearVelocity.x == 0f, "Reject different floor height");
            player.transform.position = new Vector3(10003, 10000.675f); Physics2D.SyncTransforms(); ai.Tick(0.02f);
            wall = new GameObject("MovementTestWall"); wall.layer = floor.layer;
            wall.transform.position = new Vector3(10000.4f, 10000.5f); wall.AddComponent<BoxCollider2D>().size = new Vector2(0.15f, 1f);
            Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(body.linearVelocity.x == 0f && !animator.GetBool("IsMoving"), "Stop at wall");
            wall.SetActive(false);
            floor.GetComponent<BoxCollider2D>().size = new Vector2(0.6f, 1f);
            Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(body.linearVelocity.x == 0f, "Stop before ledge");
            floor.GetComponent<BoxCollider2D>().size = new Vector2(20, 1f);
            Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(body.linearVelocity.x > 0f, "Resume when path clear");
            ai.enabled = true; // Restore event subscriptions for damage tests.
            enemy.GetComponent<EnemyHealth>().ReceiveAttack(new AttackInfo(player, 1, Vector2.left));
            int facing = move.FacingDirection;
            player.transform.position = new Vector3(9997, 10000.675f); Physics2D.SyncTransforms(); ai.Tick(0.02f);
            Check(ai.IsHurt && body.linearVelocity.x == 0f && move.FacingDirection == facing, "Hurt locks movement and facing");
            ai.Tick(0.4f); ai.Tick(0.02f);
            Check(body.linearVelocity.x < 0f, "Resume after hurt");
            enemy.GetComponent<EnemyHealth>().ReceiveAttack(new AttackInfo(player, 100, Vector2.left)); ai.Tick(0.02f);
            Check(!body.simulated && !move.IsMoving && !animator.GetBool("IsMoving"), "Death stops movement");
            return new { passed = true, checks = "detection, approach, Run, vertical velocity, stop/resume hysteresis, facing, target loss, height, wall, ledge, hurt lock/recovery, death" };
        }
        finally
        {
            if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
            if (player != null) UnityEngine.Object.DestroyImmediate(player);
            if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
            if (floor != null) UnityEngine.Object.DestroyImmediate(floor);
        }
    }
}
