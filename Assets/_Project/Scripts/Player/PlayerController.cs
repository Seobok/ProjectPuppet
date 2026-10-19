using UnityEngine;

public enum PlayerActionState { Normal, Attack, Dodge, Recoil, Hurt, Dead, Parry }

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(PlayerInputReader), typeof(PlayerMovement2D))]
[RequireComponent(typeof(PlayerCombat), typeof(PlayerDodge), typeof(PlayerHealth))]
[RequireComponent(typeof(PlayerParry))]
public sealed class PlayerController : MonoBehaviour
{

    [Header("반동 및 피격")]
    [Tooltip("초. 공격이 적에게 패링된 뒤 공격·패링·걷기가 제한되는 시간입니다. 회피는 가능합니다.")]
    [SerializeField, Min(0f)] private float recoilDuration = 0.25f;
    [Tooltip("초. 피해를 받은 뒤 행동이 제한되는 시간입니다.")]
    [SerializeField, Min(0f)] private float hurtDuration = 0.2f;

    [Header("후속 입력 예약")]
    [Tooltip("초. 회피가 끝나기 전 이 시간 안에 누른 공격을 예약해 회피 종료 직후 실행합니다.")]
    [SerializeField, Min(0f)] private float attackBufferDuration = 0.10f;
    [Tooltip("초. 반동이 끝나기 전 이 시간 안에 누른 패링을 예약합니다. 실제 판정은 반동 종료 후 열립니다.")]
    [SerializeField, Min(0f)] private float parryBufferDuration = 0.10f;

    [Header("개발용 안전장치")]
    [Tooltip("초. 공격 종료 이벤트가 누락되었을 때 강제로 공격 상태를 해제하는 최대 시간입니다. 공격 속도 조절용이 아닙니다.")]
    [SerializeField, Min(0.1f)] private float attackTimeout = 2f;
    private PlayerInputReader input;
    private PlayerMovement2D movement;
    private PlayerCombat combat;
    private PlayerDodge dodge;
    private PlayerHealth health;
    private PlayerParry parry;
    private float remaining;
    private bool bufferedAttack;
    private bool bufferedParry;
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
        parry = GetComponent<PlayerParry>();
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
        input.Consume(out bool jumpPressed, out bool attackPressed, out bool dodgePressed, out bool parryPressed);
        bool parryFollowup = false;
        
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
        else if (State == PlayerActionState.Parry)
        {
            if (parry.isActiveAndEnabled) parry.Tick(Time.fixedDeltaTime);
            if (!parry.isActiveAndEnabled || !parry.IsParrying) Transition(PlayerActionState.Normal);
        }
        else if (State == PlayerActionState.Attack || State == PlayerActionState.Recoil || State == PlayerActionState.Hurt)
        {
            remaining -= Time.fixedDeltaTime;
            if (remaining <= 0f || (State == PlayerActionState.Attack && !combat.isActiveAndEnabled))
            {
                parryFollowup = State == PlayerActionState.Recoil && bufferedParry;
                Transition(PlayerActionState.Normal);
            }
        }
        
        // 회피 입력
        if (dodgePressed && movement.isActiveAndEnabled && movement.IsGrounded && dodge.isActiveAndEnabled &&
            !health.IsDead && (State == PlayerActionState.Normal || State == PlayerActionState.Attack || State == PlayerActionState.Recoil))
        {
            Transition(PlayerActionState.Dodge);
            dodge.BeginDodge(input.Horizontal);
        }
        if (parryFollowup && State == PlayerActionState.Normal) TryParry();
        if (parryPressed && State != PlayerActionState.Dodge)
        {
            if (State == PlayerActionState.Recoil && remaining <= parryBufferDuration)
                bufferedParry = true;
            else TryParry();
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
            movement.Tick(input.Horizontal, jumpPressed, State == PlayerActionState.Normal,
                State == PlayerActionState.Dodge, dodge.HorizontalVelocity);
        
        // 공격 계산
        if (State == PlayerActionState.Attack && combat.isActiveAndEnabled) combat.TickHitbox();
    }

    public bool TryAttack()
    {
        if (!CanAttack) return false;
        combat.FaceAction(input.Horizontal);
        Transition(PlayerActionState.Attack);
        remaining = attackTimeout;
        combat.BeginAttack();
        return true;
    }
    public void EndAttack()
    {
        if (State == PlayerActionState.Attack) Transition(PlayerActionState.Normal);
    }
    public bool TryParry()
    {
        if (!isActiveAndEnabled || health.IsDead || State != PlayerActionState.Normal ||
            !movement.isActiveAndEnabled || !movement.IsGrounded || !parry.isActiveAndEnabled) return false;
        combat.FaceAction(input.Horizontal);
        Transition(PlayerActionState.Parry);
        parry.Begin();
        return true;
    }

    public void OnParrySucceeded()
    {
        if (State == PlayerActionState.Parry) Transition(PlayerActionState.Normal);
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
        if (parry != null) parry.Cancel();
        bufferedAttack = false;
        bufferedParry = false;
        State = next;
    }
}
