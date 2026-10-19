using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
public static class VerifyKnightBoss
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
    static void Anim(Animator a, float dt = 0) { a.Update(dt); a.Update(0); a.Update(0); }
    sealed class Fixture : IDisposable
    {
        public GameObject Floor, Boss, Player, Wall;
        public BossController AI; public BossCombat Combat; public EnemyHealth Health; public EnemyMovement2D Move;
        public PlayerController PC; public PlayerHealth PH; public PlayerInputReader Input; public Animator Animator; public Rigidbody2D Body;
        public Fixture()
        {
            Floor = new GameObject("BossVerificationFloor"); Floor.layer = LayerMask.NameToLayer("Ground"); Floor.transform.position = new Vector3(10000, 9999.5f); Floor.AddComponent<BoxCollider2D>().size = new Vector2(30, 1);
        }
        public void Reset(float x)
        {
            if (Boss != null) UnityEngine.Object.DestroyImmediate(Boss); if (Player != null) UnityEngine.Object.DestroyImmediate(Player);
            Boss = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/KnightBoss.prefab"), new Vector3(10000, 10000), Quaternion.identity);
            Player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab"), new Vector3(10000 + x, 10000.675f), Quaternion.identity);
            AI = Boss.GetComponent<BossController>(); Combat = Boss.GetComponent<BossCombat>(); Health = Boss.GetComponent<EnemyHealth>(); Move = Boss.GetComponent<EnemyMovement2D>(); Animator = Boss.GetComponentInChildren<Animator>(); Body = Boss.GetComponent<Rigidbody2D>();
            PC = Player.GetComponent<PlayerController>(); PH = Player.GetComponent<PlayerHealth>(); Input = Player.GetComponent<PlayerInputReader>(); Input.enabled = false; Player.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            Set(AI, "target", PC); Physics2D.SyncTransforms(); Player.GetComponent<PlayerMovement2D>().UpdateGroundedState();
            Move.Face(x); Player.GetComponent<PlayerCombat>().FaceAction(-x); Animator.Rebind(); Anim(Animator);
        }
        public void Frame(bool moveBodies = false)
        {
            typeof(PlayerController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(PC, null);
            AI.Tick(.02f);
            if (moveBodies) { Boss.transform.position += Vector3.right * Body.linearVelocity.x * .02f; Player.transform.position += Vector3.right * Player.GetComponent<Rigidbody2D>().linearVelocity.x * .02f; Physics2D.SyncTransforms(); }
            Anim(Animator, .02f); Anim(Player.GetComponentInChildren<Animator>(), .02f);
        }
        public void Dispose() { foreach (var obj in new[] { Boss, Player, Wall, Floor }) if (obj != null) UnityEngine.Object.DestroyImmediate(obj); }
    }
    static void Counter(Fixture f, string response)
    {
        f.Reset(.8f); f.AI.Tick(.02f); Anim(f.Animator);
        Check(f.AI.State == BossActionState.Guard && f.Animator.GetCurrentAnimatorStateInfo(0).IsName("Guard"), "Guard pose");
        Check(f.PC.TryAttack(), "Player initiates guard attack");
        var attack = f.Player.GetComponent<PlayerCombat>(); attack.BeginHitbox(); attack.TickHitbox(); Anim(f.Animator);
        Check(f.Health.CurrentHealth == 240 && f.PC.State == PlayerActionState.Recoil && f.AI.State == BossActionState.ParrySuccess, "Front guard parries player and enters recoil");
        for (int frame = 1; frame <= 40; frame++)
        {
            if (response == "parry" && frame == 9) Set(f.Input, "parryRequested", true);
            if (response == "dodge" && frame == 12) Set(f.Input, "dodgeRequested", true);
            f.Frame(response == "dodge");
            if (f.AI.State == BossActionState.Parried) break;
        }
        if (response == "stand") Check(f.PH.CurrentHealth == 85, "Unanswered counter hits exactly once: " + f.PH.CurrentHealth);
        else Check(f.PH.CurrentHealth == 100, response + " responds to counter without damage: " + f.PH.CurrentHealth);
        if (response == "parry") Check(f.AI.State == BossActionState.Parried && f.PC.CanAttack, "Buffered parry restores attack opportunity");
    }
    public static object Main()
    {
        Check(EditorApplication.isPlaying, "Requires Play mode");
        using (var f = new Fixture())
        {
            f.Reset(5); f.AI.Tick(.02f); Anim(f.Animator);
            Check(f.AI.State == BossActionState.Approach && f.Body.linearVelocity.x > 0 && !f.Boss.GetComponentInChildren<SpriteRenderer>().flipX, "Right-facing approach");
            f.Player.transform.position = new Vector3(9995, 10000.675f); Physics2D.SyncTransforms(); f.AI.Tick(.02f);
            Check(f.Body.linearVelocity.x < 0 && f.Move.FacingDirection == -1 && f.Boss.GetComponentInChildren<SpriteRenderer>().flipX, "Left-facing approach");
            f.Player.transform.position = new Vector3(10020, 10000.675f); Physics2D.SyncTransforms(); f.AI.Tick(.02f); Check(!f.AI.IsTracking && f.Body.linearVelocity.x == 0, "Target loss");
            f.Reset(.8f); f.AI.Tick(.02f); f.Player.transform.position = new Vector3(9999.2f, 10000.675f); Physics2D.SyncTransforms();
            Check(f.Health.ReceiveAttack(new AttackInfo(f.Player, 10, Vector2.right)) == AttackResult.Hit && f.Health.CurrentHealth == 230 && f.AI.State == BossActionState.Hurt, "Rear attack bypasses guard");
            f.AI.Tick(.2f); Check(f.AI.State == BossActionState.Idle, "Hurt recovers");
            Counter(f, "stand"); Counter(f, "parry"); Counter(f, "dodge");

            f.Reset(.8f); Check(f.Combat.BeginPattern(BossPattern.Combo), "Combo begins"); Anim(f.Animator);
            Check(f.Animator.GetCurrentAnimatorStateInfo(0).IsName("Combo1Prepare"), "First preparation pose");
            bool firstParried = false, secondStarted = false, finalParried = false, dealtDuringGap = false;
            int lastIndex = 0; float prepareTime = 0; bool requested = false;
            for (int frame = 0; frame < 140; frame++)
            {
                if (f.Combat.StepIndex != lastIndex) { lastIndex = f.Combat.StepIndex; prepareTime = 0; requested = false; secondStarted = true; }
                if (f.Combat.Phase == EnemyAttackPhase.Prepare)
                {
                    prepareTime += .02f;
                    if (!requested && prepareTime >= (f.Combat.StepIndex == 0 ? .34f : .18f)) { Set(f.Input, "parryRequested", true); requested = true; }
                }
                f.Frame();
                if (f.Combat.Pattern == BossPattern.Combo && f.Combat.StepIndex == 0 && f.Combat.Phase == EnemyAttackPhase.Recovery)
                {
                    firstParried = f.PH.CurrentHealth == 100 && f.PC.CanAttack;
                    if (!dealtDuringGap)
                    {
                        f.Health.ReceiveAttack(new AttackInfo(f.Player, 1, Vector2.left)); dealtDuringGap = true;
                        Check(f.Combat.IsAttacking, "Ordinary hit does not cancel remaining combo");
                    }
                }
                if (f.AI.State == BossActionState.Parried) { finalParried = true; break; }
            }
            Check(firstParried && secondStarted && finalParried && f.PH.CurrentHealth == 100, "First parry preserves second strike; final parry staggers: " + firstParried + "/" + secondStarted + "/" + finalParried + "/" + f.PH.CurrentHealth);
            Check(f.PC.TryAttack(), "Final parry allows followup");
            var pcCombat = f.Player.GetComponent<PlayerCombat>(); pcCombat.BeginHitbox(); pcCombat.TickHitbox();
            Check(f.Health.CurrentHealth == 229 && f.AI.State == BossActionState.Parried, "Followup damages boss without shortening parry opening");

            f.Reset(.8f); Check(f.Combat.BeginPattern(BossPattern.Combo), "Unanswered combo begins");
            for (int frame = 0; frame < 140 && f.Combat.IsAttacking; frame++) f.Frame();
            Check(!f.Combat.IsAttacking && f.PH.CurrentHealth == 76, "Two distinct strikes deal damage once each: " + f.PH.CurrentHealth);

            f.Reset(2.7f); Check(f.Combat.BeginPattern(BossPattern.Heavy), "Heavy begins"); Anim(f.Animator);
            for (int frame = 0; frame < 32; frame++) f.Frame(true);
            Check(f.Combat.Phase == EnemyAttackPhase.Prepare && f.PH.CurrentHealth == 100 && f.Body.linearVelocity.x == 0, "Long heavy telegraph has no early movement or damage");
            f.Health.ReceiveAttack(new AttackInfo(f.Player, 1, Vector2.left)); Check(f.Combat.IsAttacking, "Committed heavy takes damage without cancellation");
            bool reachedRecovery = false;
            for (int frame = 0; frame < 40; frame++)
            {
                f.Frame(true); if (f.Combat.Phase == EnemyAttackPhase.Recovery) { reachedRecovery = true; break; }
            }
            Check(reachedRecovery && f.PH.CurrentHealth == 75 && f.Boss.transform.position.x > 10001.5f, "Heavy advances and hits once: hp=" + f.PH.CurrentHealth + " x=" + f.Boss.transform.position.x);
            f.Player.transform.position = f.Boss.transform.position + new Vector3(-.8f, .675f); Physics2D.SyncTransforms();
            f.Health.ReceiveAttack(new AttackInfo(f.Player, 10, Vector2.right));
            for (int frame = 0; frame < 30; frame++) f.Frame();
            Check(f.Combat.Phase == EnemyAttackPhase.Recovery && f.Move.FacingDirection == 1 && f.Body.linearVelocity.x == 0, "Rear hit preserves long recovery and locked direction");
            for (int frame = 0; frame < 40 && f.Combat.IsAttacking; frame++) f.Frame(); Check(!f.Combat.IsAttacking, "Heavy recovery completes via animation event");

            f.Reset(-.8f); Check(f.Combat.BeginPattern(BossPattern.Heavy) && f.PC.TryParry(), "Heavy parry setup"); f.Combat.BeginHitbox(); Anim(f.Animator);
            Check(f.PH.CurrentHealth == 100 && f.AI.State == BossActionState.Parried && !f.Combat.IsAttacking, "Heavy parry cancels attack");
            f.AI.Tick(.42f); Check(f.AI.State == BossActionState.Idle, "Heavy parry opening is shorter than dodge/rear recovery");

            f.Reset(-1.2f); Check(f.Combat.BeginPattern(BossPattern.Heavy), "Heavy dodge setup");
            for (int frame = 0; frame < 34; frame++) f.Frame(true);
            Set(f.Input, "dodgeRequested", true);
            bool crossed = false, rearFollowup = false;
            for (int frame = 0; frame < 90; frame++)
            {
                f.Frame(true);
                crossed |= f.Player.transform.position.x > f.Boss.transform.position.x;
                if (!crossed || !f.PC.CanAttack || f.Combat.Phase != EnemyAttackPhase.Recovery) continue;
                float offset = f.Boss.transform.position.x - f.Player.transform.position.x;
                if (Mathf.Abs(offset) > .9f)
                {
                    f.Player.GetComponent<PlayerMovement2D>().Tick(Mathf.Sign(offset), false, true, false, 0);
                    f.Player.transform.position += Vector3.right * f.Player.GetComponent<Rigidbody2D>().linearVelocity.x * .02f;
                    Physics2D.SyncTransforms();
                }
                else
                {
                    var attack = f.Player.GetComponent<PlayerCombat>(); attack.FaceAction(offset);
                    Check(f.PC.TryAttack(), "Rear attack after heavy dodge starts");
                    attack.BeginHitbox(); attack.TickHitbox(); rearFollowup = f.Health.CurrentHealth == 230; break;
                }
            }
            Check(crossed && rearFollowup && f.PH.CurrentHealth == 100 && f.Combat.Phase == EnemyAttackPhase.Recovery,
                "Heavy dodge-through can return and land rear attack within recovery: hp=" + f.PH.CurrentHealth + " boss=" + f.Health.CurrentHealth);

            f.Reset(3); f.Wall = new GameObject("BossVerificationWall"); f.Wall.layer = f.Floor.layer; f.Wall.transform.position = new Vector3(10000.5f, 10000.5f); f.Wall.AddComponent<BoxCollider2D>().size = new Vector2(.15f, 1.5f); Physics2D.SyncTransforms();
            Check(f.Combat.BeginPattern(BossPattern.Heavy), "Wall heavy starts"); f.Combat.BeginHitbox(); f.Combat.Tick(.02f); Check(f.Body.linearVelocity.x == 0, "Heavy stops at wall"); f.Combat.CancelAttack();
            f.Wall.SetActive(false); f.Floor.GetComponent<BoxCollider2D>().size = new Vector2(.65f, 1); Physics2D.SyncTransforms();
            Check(f.Combat.BeginPattern(BossPattern.Heavy), "Ledge heavy starts"); f.Combat.BeginHitbox(); f.Combat.Tick(.02f); Check(f.Body.linearVelocity.x == 0, "Heavy stops at ledge"); f.Combat.CancelAttack();
            f.Floor.GetComponent<BoxCollider2D>().size = new Vector2(30, 1); Physics2D.SyncTransforms();
            Check(f.Combat.BeginPattern(BossPattern.Combo), "Watchdog starts"); f.Combat.Tick(10); f.Combat.BeginHitbox(); Check(!f.Combat.IsAttacking, "Missing animation event recovers; stale event ignored");

            f.Reset(1); var choices = new BossPattern[3];
            for (int n = 0; n < choices.Length; n++)
            {
                f.AI.Tick(.02f); f.AI.Tick(.7f); choices[n] = f.Combat.Pattern;
                f.Player.transform.position = new Vector3(10010, 10000.675f); Physics2D.SyncTransforms();
                for (int step = 0; step < 2 && f.Combat.IsAttacking; step++) { f.Combat.BeginHitbox(); f.Combat.EndHitbox(); f.Combat.EndStep(); }
                f.Player.transform.position = new Vector3(10001, 10000.675f); Physics2D.SyncTransforms();
            }
            Check(choices[0] == BossPattern.Combo && choices[1] == BossPattern.Combo && choices[2] == BossPattern.Heavy, "Repeated close pattern switches after limit");
            f.AI.Tick(.02f); f.AI.Tick(.7f); f.Health.ReceiveAttack(new AttackInfo(f.Player, 1000, Vector2.left)); f.Combat.BeginHitbox(); Anim(f.Animator);
            Check(f.Health.IsDead && !f.Body.simulated && !f.Combat.IsAttacking && !f.Boss.GetComponent<CapsuleCollider2D>().enabled && f.Animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), "Death interrupts and disables collisions");
            Anim(f.Animator, 3); Check(f.Animator.GetCurrentAnimatorStateInfo(0).IsName("Death") && !f.Combat.BeginPattern(BossPattern.Heavy), "Death remains terminal");
            return new { passed = true, checks = "left/right approach, target loss, front/rear guard, counter hit/parry/dodge, timed double parry, final-parry followup, two combo hits, heavy telegraph/lunge/hit/recovery, armor, wall/ledge, watchdog, pattern repetition, death" };
        }
    }
}
