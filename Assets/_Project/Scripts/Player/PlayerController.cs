using UnityEngine;

public enum PlayerActionState { Normal, Attack, Dodge, Recoil, Hurt, Dead }

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(PlayerInputReader), typeof(PlayerMovement2D))]
[RequireComponent(typeof(PlayerCombat), typeof(PlayerDodge), typeof(PlayerHealth))]
public sealed class PlayerController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float recoilDuration = 0.25f;
    [SerializeField, Min(0f)] private float attackBufferDuration = 0.10f;
    [SerializeField, Min(0f)] private float hurtDuration = 0.2f;
    [SerializeField, Min(0.1f)] private float attackTimeout = 2f;
    private PlayerInputReader input;
    private PlayerMovement2D movement;
    private PlayerCombat combat;
    private PlayerDodge dodge;
    private PlayerHealth health;
    private float remaining;
    private bool bufferedAttack;
    public PlayerActionState State { get; private set; }
    public bool IsInvulnerable => isActiveAndEnabled && State == PlayerActionState.Dodge && dodge.IsInvulnerable;
    public bool CanAttack => isActiveAndEnabled && !health.IsDead && combat.isActiveAndEnabled && State == PlayerActionState.Normal;

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        movement = GetComponent<PlayerMovement2D>();
        combat = GetComponent<PlayerCombat>();
        dodge = GetComponent<PlayerDodge>();
        health = GetComponent<PlayerHealth>();
    }
    private void OnEnable() => health.Damaged += OnDamaged;
    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        Transition(health.IsDead ? PlayerActionState.Dead : PlayerActionState.Normal);
        input.Clear();
        if (movement.isActiveAndEnabled) 
            movement.Tick(0f, false, false, false, 0f);
    }

    private void FixedUpdate()
    {
        // 입력 받기
        input.Consume(out bool jumpPressed, out bool attackPressed, out bool dodgePressed);
        
        // 플레이어 바닥에 닿아있는지 업데이트
        movement.UpdateGroundedState();
        
        // 체력이 없으면 죽은 상태로 전환
        if (health.IsDead && State != PlayerActionState.Dead) 
            Transition(PlayerActionState.Dead);
        
        // 회피 중이면
        if (State == PlayerActionState.Dodge)
        {
            // 회피 타이머
            if (dodge.isActiveAndEnabled) 
                dodge.Tick(Time.fixedDeltaTime);
            // 회피 끝
            if (!dodge.isActiveAndEnabled || !dodge.IsDodging)
            {
                bool followup = bufferedAttack;
                Transition(PlayerActionState.Normal);
                if (followup) TryAttack();
            }
        }
        // 이동 불가 상태 타이머
        else if (State == PlayerActionState.Attack || State == PlayerActionState.Recoil || State == PlayerActionState.Hurt)
        {
            remaining -= Time.fixedDeltaTime;
            if (remaining <= 0f || (State == PlayerActionState.Attack && !combat.isActiveAndEnabled))
                Transition(PlayerActionState.Normal);
        }
        
        // 회피 입력
        if (dodgePressed && movement.isActiveAndEnabled && movement.IsGrounded && dodge.isActiveAndEnabled &&
            !health.IsDead && (State == PlayerActionState.Normal || State == PlayerActionState.Attack || State == PlayerActionState.Recoil))
        {
            Transition(PlayerActionState.Dodge);
            dodge.BeginDodge(input.Horizontal);
        }
        
        // 공격 입력
        if (attackPressed)
        {
            if (State == PlayerActionState.Dodge)
                bufferedAttack |= dodge.IsDodging && dodge.Remaining <= attackBufferDuration;
            else TryAttack();
        }
        
        // 이동
        if (movement.isActiveAndEnabled)
            movement.Tick(input.Horizontal, jumpPressed, State == PlayerActionState.Normal || State == PlayerActionState.Recoil,
                State == PlayerActionState.Dodge, dodge.HorizontalVelocity);
        
        // 공격 계산
        if (State == PlayerActionState.Attack && combat.isActiveAndEnabled) combat.TickHitbox();
    }

    public bool TryAttack()
    {
        if (!CanAttack) return false;
        Transition(PlayerActionState.Attack);
        remaining = attackTimeout;
        combat.BeginAttack();
        return true;
    }
    public void EndAttack()
    {
        if (State == PlayerActionState.Attack) Transition(PlayerActionState.Normal);
    }
    public void EnterRecoil()
    {
        if (State != PlayerActionState.Attack) return;
        Transition(PlayerActionState.Recoil);
        remaining = recoilDuration;
        combat.SetRecoil(true);
    }
    private void OnDamaged(bool dead)
    {
        Transition(dead ? PlayerActionState.Dead : PlayerActionState.Hurt);
        remaining = hurtDuration;
        input.Clear();
    }
    private void Transition(PlayerActionState next)
    {
        combat.CancelAttack();
        combat.SetRecoil(false);
        dodge.Cancel();
        bufferedAttack = false;
        State = next;
    }
}
