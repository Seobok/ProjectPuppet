using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum CombatState
{
    Idle,
    Attacking,
    Recoil
}

[DisallowMultipleComponent]
public sealed class PlayerCombat : MonoBehaviour
{
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Recoiling = Animator.StringToHash("isRecoiling");

    [Header("InputAction")]
    [SerializeField] private InputActionAsset inputActions;
    [Header("Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Header("Attack")]
    [SerializeField] private Transform attackTransform;
    [SerializeField] private Vector2 attackSize = new Vector2(1f, 1f);
    [SerializeField] private LayerMask targetLayer;
    [SerializeField, Min(1)] private int attackDamage = 10;
    [Header("Recoil")]
    [SerializeField, Min(0f)] private float recoilDuration = 0.25f;
    
    private InputActionAsset runtimeActions;
    private InputAction attackAction;
    private CombatState state;
    private float recoilRemaining;
    private bool hitboxActive;
    private readonly HashSet<IAttackReceiver> hitTargets = new HashSet<IAttackReceiver>();
    
    private float FacingDirection => spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
    private Vector2 AttackCenter => (Vector2)transform.position + new Vector2(attackTransform.localPosition.x * FacingDirection, attackTransform.localPosition.y);
    public bool BlocksMovement => isActiveAndEnabled && state == CombatState.Attacking;
    public bool CanAttack => state == CombatState.Idle;
    public bool CanParry => state == CombatState.Idle;
    public bool CanDodge => true;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if(spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        
        if (inputActions == null)
        {
            Debug.LogError("Assign the input actions asset to PlayerCombat.", this);
            enabled = false;
            return;
        }

        runtimeActions = Instantiate(inputActions);
        attackAction = runtimeActions.FindAction("Player/Attack", true);
    }

    private void OnEnable()
    {
        if (attackAction == null)
            return;

        attackAction.performed += OnAttack;
        attackAction.Enable();
    }

    private void FixedUpdate()
    {
        if (!hitboxActive)
            return;
        
        Collider2D[] hits = Physics2D.OverlapBoxAll(AttackCenter, attackSize, 0f, targetLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit.transform.IsChildOf(transform))
                continue;

            IAttackReceiver target = hit.GetComponentInParent<IAttackReceiver>();
            
            if(target == null)
                continue;

            if (hitTargets.Add(target))
            {
                AttackResult result = target.ReceiveAttack(new AttackInfo(gameObject, attackDamage, hit.transform.position - transform.position));

                Debug.Log(hit.name + " " + result.ToString());
                
                // 패리 당함
                if (result == AttackResult.Parried)
                {
                    EnterRecoil();
                    break;
                }
            }
        }
    }
    
    private void Update()
    {
        // 반동 타이머
        if (state != CombatState.Recoil)
            return;

        recoilRemaining -= Time.deltaTime;

        if (recoilRemaining <= 0f)
        {
            state = CombatState.Idle;
            if(animator != null)
                animator.SetBool(Recoiling, false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(AttackCenter, new Vector3(attackSize.x, attackSize.y, 0f));
    }

    // Attack 버튼 입력 이벤트
    private void OnAttack(InputAction.CallbackContext context)
    {
        if (!CanAttack)
            return;
        
        hitTargets.Clear();
        state = CombatState.Attacking;
        if(animator != null)
            animator.SetTrigger(Attack);
    }

    // Attack 애니메이션 종료시 호출
    public void EndAttack()
    {
        if (state != CombatState.Attacking)
            return;
        
        hitboxActive = false;
        state = CombatState.Idle;
    }

    public void BeginHitbox()
    {
        if(state == CombatState.Attacking)
            hitboxActive = true;
    }

    public void EndHitbox()
    {
        hitboxActive = false;
    }
    
    private void EnterRecoil()
    {
        hitboxActive = false;
        state = CombatState.Recoil;
        recoilRemaining = recoilDuration;

        if(animator != null)
            animator.SetBool(Recoiling, true);
    }
    
    public void CancelAttackForDodge()
    {
        hitboxActive = false;
        recoilRemaining = 0f;
        state = CombatState.Idle;
    }

    private void OnDisable()
    {
        if (attackAction == null)
            return;

        attackAction.performed -= OnAttack;
        attackAction.Disable();
    }

    private void OnDestroy()
    {
        if (runtimeActions != null)
            Destroy(runtimeActions);
    }
}
