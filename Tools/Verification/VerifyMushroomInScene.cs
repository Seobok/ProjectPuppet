using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
public static class VerifyMushroomInScene
{
    public static async Task<object> Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Requires Play mode");
        var mushroom = UnityEngine.Object.FindFirstObjectByType<MushroomController>();
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
        if (mushroom == null || player == null) throw new Exception("Requires MushroomTest scene");
        float startX = mushroom.transform.position.x;
        int startHealth = player.CurrentHealth;
        bool background = Application.runInBackground;
        bool paused = EditorApplication.isPaused;
        Application.runInBackground = true; EditorApplication.isPaused = false;
        try
        {
            await Task.Delay(2400);
            if (Mathf.Abs(mushroom.transform.position.x - startX) < .05f) throw new Exception("Automatic approach/lunge did not move Mushroom");
            if (player.CurrentHealth >= startHealth) throw new Exception("Automatic lunge did not damage player");
            if (mushroom.GetComponent<EnemyHealth>().IsDead) throw new Exception("Mushroom unexpectedly died");
            return new { passed = true, startX, endX = mushroom.transform.position.x, playerHealthBefore = startHealth, playerHealthAfter = player.CurrentHealth, state = mushroom.State.ToString() };
        }
        finally { Application.runInBackground = background; EditorApplication.isPaused = paused; }
    }
}
