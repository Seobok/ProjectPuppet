using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
public static class VerifyPlayerDamageAnimations
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Tick(PlayerController c) => typeof(PlayerController).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(c, null);
    static void Anim(Animator a) { a.Update(0); a.Update(0); }
    public static object Main()
    {
        Check(EditorApplication.isPlaying, "Requires Play mode");
        GameObject p = null, attacker = null;
        try
        {
            p = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab"), new Vector3(20000, 20000), Quaternion.identity);
            attacker = new GameObject("DamageAnimationTest");
            var c = p.GetComponent<PlayerController>(); var h = p.GetComponent<PlayerHealth>(); var a = p.GetComponentInChildren<Animator>();
            p.GetComponent<PlayerInputReader>().enabled = false;
            a.Rebind(); Anim(a);
            Check(c.TryAttack(), "Attack starts"); Anim(a);
            Check(h.ReceiveAttack(new AttackInfo(attacker, 10, Vector2.right)) == AttackResult.Hit, "Damage received");
            Anim(a);
            Check(c.State == PlayerActionState.Hurt && a.GetCurrentAnimatorStateInfo(0).IsName("Hurt"), "Damage interrupts attack with Hurt");
            Check(!c.TryAttack() && !c.TryParry(), "Hurt blocks actions");
            a.Update(0.05f); var previous = a.GetCurrentAnimatorStateInfo(0).normalizedTime;
            h.ReceiveAttack(new AttackInfo(attacker, 10, Vector2.right)); Anim(a);
            Check(a.GetCurrentAnimatorStateInfo(0).IsName("Hurt") && a.GetCurrentAnimatorStateInfo(0).normalizedTime < previous, "Repeated damage restarts Hurt");
            for (int i = 0; i < 20; i++) { Tick(c); a.Update(Time.fixedDeltaTime); }
            Check(c.State == PlayerActionState.Normal && !a.GetCurrentAnimatorStateInfo(0).IsName("Hurt"), "Hurt recovers");
            h.ReceiveAttack(new AttackInfo(attacker, 1000, Vector2.right)); Anim(a);
            Check(c.State == PlayerActionState.Dead && a.GetCurrentAnimatorStateInfo(0).IsName("Death"), "Lethal damage plays Death");
            for (int i = 0; i < 100; i++) { Tick(c); a.Update(Time.fixedDeltaTime); }
            Check(a.GetCurrentAnimatorStateInfo(0).IsName("Death") && !c.TryAttack() && !c.TryParry(), "Death remains terminal");
            Check(h.ReceiveAttack(new AttackInfo(attacker, 10, Vector2.right)) == AttackResult.Ignored, "Dead ignores further hits");
            return new { passed = true, checks = "attack interruption, hurt action lock, repeated hit restart, recovery, lethal hit, terminal death, dead hit ignored" };
        }
        finally { if(p != null) UnityEngine.Object.DestroyImmediate(p); if(attacker != null) UnityEngine.Object.DestroyImmediate(attacker); }
    }
}
