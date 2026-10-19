using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class PlayerParry : MonoBehaviour
{
    private static readonly int Parrying = Animator.StringToHash("isParrying");
    private static readonly int Success = Animator.StringToHash("ParrySuccess");

    [Header("패링 판정 및 실패 회복")]
    [Tooltip("초. 패링 입력 직후 정면 공격을 받아칠 수 있는 시간입니다. 길수록 성공하기 쉬워집니다.")]
    [SerializeField, Min(0.01f)] private float windowDuration = 0.12f;
    [Tooltip("초. 패링 판정이 닫힌 뒤의 실패 회복 시간입니다. 성공하면 회복을 생략하고 즉시 기본 공격이 가능합니다.")]
    [SerializeField, Min(0f)] private float recoveryDuration = 0.18f;

    [Header("표현 및 소리 연결")]
    [Tooltip("패링 시작 시 한 번 호출됩니다. 준비 효과나 소리를 연결할 수 있습니다.")]
    [SerializeField] private UnityEvent onParryStarted = new UnityEvent();
    [Tooltip("정면 패링 성공 시 한 번 호출됩니다. 성공 효과나 금속음을 연결할 수 있습니다.")]
    [SerializeField] private UnityEvent onParrySucceeded = new UnityEvent();
    [Tooltip("공격을 받아치지 못한 채 성공 판정 시간이 끝났을 때 호출됩니다.")]
    [SerializeField] private UnityEvent onParryFailed = new UnityEvent();
    private PlayerController controller;
    private Animator animator;
    private SpriteRenderer sprite;
    private Color originalColor;
    private float elapsed;
    private float flashRemaining;
    public bool IsParrying { get; private set; }
    public bool IsWindowOpen => isActiveAndEnabled && IsParrying && elapsed < windowDuration && controller.State == PlayerActionState.Parry;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        animator = GetComponentInChildren<Animator>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null) originalColor = sprite.color;
    }

    public void Begin()
    {
        elapsed = 0f; IsParrying = true;
        if (animator != null) { animator.ResetTrigger(Success); animator.SetBool(Parrying, true); }
        if (sprite != null) sprite.color = new Color(0.55f, 0.85f, 1f, originalColor.a);
        onParryStarted.Invoke();
    }

    public void Tick(float dt)
    {
        if (!IsParrying) return;
        bool wasOpen = elapsed < windowDuration;
        elapsed += dt;
        if (wasOpen && elapsed >= windowDuration)
        {
            if (sprite != null) sprite.color = originalColor;
            onParryFailed.Invoke();
        }
        if (elapsed + 0.00001f >= windowDuration + recoveryDuration) Cancel();
    }

    public bool TryReceive(AttackInfo attack)
    {
        if (!IsWindowOpen || attack.Attacker == null) return false;
        int facing = sprite != null && sprite.flipX ? -1 : 1;
        if ((attack.Attacker.transform.position.x - transform.position.x) * facing <= 0.05f) return false;
        controller.OnParrySucceeded();
        if (animator != null) animator.SetTrigger(Success);
        if (sprite != null) sprite.color = new Color(1f, 0.9f, 0.3f, originalColor.a);
        flashRemaining = 0.10f;
        onParrySucceeded.Invoke();
        return true;
    }

    private void Update()
    {
        if (flashRemaining <= 0f) return;
        flashRemaining -= Time.deltaTime;
        if (flashRemaining <= 0f && sprite != null) sprite.color = originalColor;
    }

    public void Cancel()
    {
        IsParrying = false; flashRemaining = 0f;
        if (animator != null) { animator.SetBool(Parrying, false); animator.ResetTrigger(Success); }
        if (sprite != null) sprite.color = originalColor;
    }

    private void OnDisable() => Cancel();
}
