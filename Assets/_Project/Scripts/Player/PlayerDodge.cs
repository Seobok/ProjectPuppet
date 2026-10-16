using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement2D))]
public sealed class PlayerDodge : MonoBehaviour
{
    private static readonly int DodgeSpeed = Animator.StringToHash("DodgeSpeed");
    private static readonly int IsWallSlide = Animator.StringToHash("IsWallSlide");
    private static readonly int Dodging = Animator.StringToHash("isDodging");
    private static readonly int Jump = Animator.StringToHash("Jump");
    
    [SerializeField] private AnimationClip dodgeAnimation;
    [SerializeField, Min(0f)] private float distance = 2.5f;                //이동거리
    [SerializeField, Min(0f)] private float moveDuration = 0.20f;           //이동시간 (최소 한 물리 프레임)
    [SerializeField, Min(0f)] private float invulnerableDuration = 0.14f;   //무적시간
    [SerializeField, Min(0f)] private float recoveryDuration = 0.08f;       //후딜
    [SerializeField, Min(0f)] private float attackBufferDuration = 0.10f;   //공격 버퍼 적용 시간
    [SerializeField] private LayerMask enemyBodyLayers = 1 << 9;         //충돌 무시 레이어

    private PlayerMovement2D movement;
    private PlayerCombat combat;
    private PlayerHealth health;
    private Rigidbody2D body;
    private SpriteRenderer sprite;
    private Animator animator;
    private InputAction dodgeAction;
    private Collider2D[] bodyColliders;
    private readonly List<CollisionPair> ignoredPairs = new List<CollisionPair>();
    private float elapsed;  //회피 진행 시간
    private float direction;
    private bool requested;
    
    const float TIME_TOLERANCE = 0.00001f;

    private struct CollisionPair
    {
        public Collider2D Player;
        public Collider2D Enemy;
    }

    public bool IsDodging { get; private set; }
    private float EffectiveMoveDuration => Mathf.Max(Time.fixedDeltaTime, moveDuration);
    private float TotalDuration => EffectiveMoveDuration + recoveryDuration;
    public bool IsInvulnerable => isActiveAndEnabled && IsDodging && elapsed + TIME_TOLERANCE < Mathf.Min(invulnerableDuration, EffectiveMoveDuration); //무적 여부 확인
    public bool CanBufferAttack => IsDodging && Remaining <= attackBufferDuration; // 회피 중 공격 예약 시간
    public float Remaining => Mathf.Max(0f, TotalDuration - elapsed); // 회피 남은 시간
    public float HorizontalVelocity => elapsed + TIME_TOLERANCE < EffectiveMoveDuration ? // 이동 단계인지 확인
        direction * distance / EffectiveMoveDuration *                              // 이동 단계가 맞으면 일반 속도 계산 (방향 * 거리 / 시간)
        Mathf.Clamp01((EffectiveMoveDuration - elapsed) / Time.fixedDeltaTime)       // 마지막 물리 프레임의 이동량 보정 ( 일반적인 상황에서는 1 )
        : 0f;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement2D>();
        combat = GetComponent<PlayerCombat>();
        health = GetComponent<PlayerHealth>();
        body = GetComponent<Rigidbody2D>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        bodyColliders = GetComponentsInChildren<Collider2D>();
        dodgeAction = movement.Actions?.FindAction("Player/Dodge", true);
    }

    private void OnEnable()
    {
        if (dodgeAction == null) return;
        dodgeAction.performed += OnDodge;
        dodgeAction.Enable();
    }

    private void OnDodge(InputAction.CallbackContext context) => requested = true;

    // 회피 계산 -> 이동 속도 적용 순서 보장용 커스텀 Tick
    public void Tick(float deltaTime, bool grounded, float horizontal)
    {
        RestoreSeparatedPairs(false);
        
        // 회피 중 죽으면 상태 취소
        if (IsDodging && health != null && health.IsDead)
            Cancel();

        // 회피 타이머
        if (IsDodging)
        {
            elapsed += deltaTime;
            if (elapsed + TIME_TOLERANCE >= TotalDuration)
            {
                IsDodging = false;
                EndDodgeAnimation();
            }
        }

        // 회피 입력 받음
        if (requested && !IsDodging && grounded && (health == null || !health.IsDead) && (combat == null || combat.CanDodge))
        {
            // 입력에 따른 대시 방향 결정
            if (Mathf.Abs(horizontal) > 0.01f)
            {
                // 입력이 음수면 왼쪽(-1), 양수면 오른쪽(1)
                direction = Mathf.Sign(horizontal);
            }
            else
            {
                // 입력이 없으면 바라보는 방향 사용
                if (sprite != null && sprite.flipX)
                    direction = -1f;
                else
                    direction = 1f;
            }
            
            elapsed = 0f;
            IsDodging = true;
            if (sprite != null) sprite.flipX = direction < 0f;
            combat?.CancelAttackForDodge();
            
            if (animator != null)
            {
                animator.ResetTrigger(Jump);
                animator.SetBool(IsWallSlide, false);
                animator.SetBool(Dodging, true);
                if (dodgeAnimation != null)
                    animator.SetFloat(DodgeSpeed, dodgeAnimation.length / TotalDuration);
            }
        }
        requested = false;

        if (IsDodging)
            IgnoreNearbyBodies(deltaTime);
        else
            RestoreSeparatedPairs(false);
    }

    //적을 통과할 수 있도록 충돌 무시
    private void IgnoreNearbyBodies(float deltaTime)
    {
        foreach (Collider2D own in bodyColliders)
        {
            // 충돌 안하는 콜라이더면 무시
            if (own == null || !own.enabled || own.isTrigger || own.attachedRigidbody != body) continue;
            
            Bounds bounds = own.bounds;
            float travel = HorizontalVelocity * deltaTime;
            
            // 이동하는 거리를 포함하도록 탐색 영역을 늘림
            Vector2 center = (Vector2)bounds.center + Vector2.right * travel * 0.5f;
            Vector2 size = (Vector2)bounds.size + new Vector2(Mathf.Abs(travel) + 0.1f, 0.1f);
            
            // 위 탐색영역에 있는 적 콜라이더 탐색
            foreach (Collider2D enemy in Physics2D.OverlapBoxAll(center, size, 0f, enemyBodyLayers))
            {
                if (enemy.isTrigger || enemy.transform.IsChildOf(transform) || Physics2D.GetIgnoreCollision(own, enemy)) continue;
                Physics2D.IgnoreCollision(own, enemy, true);
                ignoredPairs.Add(new CollisionPair { Player = own, Enemy = enemy });
            }
        }
    }

    private void FixedUpdate()
    {
        if (!IsDodging) RestoreSeparatedPairs(false);
    }

    // 회피 할 때 적과 충돌 무시 했던거 복구
    // force == true 면 회피 중이라도 강제 복구
    private void RestoreSeparatedPairs(bool force)
    {
        for (int i = ignoredPairs.Count - 1; i >= 0; i--)
        {
            CollisionPair pair = ignoredPairs[i];
            
            // 콜라이더가 삭제 되었으면 목록에서 그냥 제거
            if (pair.Player == null || pair.Enemy == null)
            {
                ignoredPairs.RemoveAt(i);
                continue;
            }
            
            // 아직 회피 중이면 나중에 복구
            if (!force && (IsDodging || pair.Player.Distance(pair.Enemy).isOverlapped)) continue;
            
            // 회피 끝났으면 다시 충돌 복구
            Physics2D.IgnoreCollision(pair.Player, pair.Enemy, false);
            ignoredPairs.RemoveAt(i);
        }
    }

    // 회피 취소
    public void Cancel()
    {
        bool wasDodging = IsDodging;
        IsDodging = false;
        requested = false;
        if (wasDodging) EndDodgeAnimation();
    }

    private void EndDodgeAnimation()
    {
        if (animator == null) return;
        animator.SetBool(Dodging, false);
    }

    private void OnDisable()
    {
        if (dodgeAction != null)
        {
            dodgeAction.performed -= OnDodge;
            dodgeAction.Disable();
        }
        Cancel();
        RestoreSeparatedPairs(true);
    }
}
