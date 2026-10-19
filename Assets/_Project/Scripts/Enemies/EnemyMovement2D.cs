using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public sealed class EnemyMovement2D : MonoBehaviour
{

    [Header("접근 이동")]
    [Tooltip("초당 월드 이동 거리입니다. 공격·방어·피격 중에는 이동이 제한됩니다.")]
    [SerializeField, Min(0f)] private float moveSpeed = 1.5f;

    [Header("벽 및 낭떠러지 감지")]
    [Tooltip("월드 단위. 몸 앞에서 벽을 검사하는 거리입니다. 길수록 벽에서 일찍 멈춥니다.")]
    [SerializeField, Min(0.01f)] private float wallCheckDistance = 0.15f;
    [Tooltip("월드 단위. 몸의 앞쪽 끝에서 추가로 내다보는 발판 검사 거리입니다. 길수록 낭떠러지에서 일찍 멈춥니다.")]
    [SerializeField, Min(0.01f)] private float ledgeLookAhead = 0.12f;
    [Tooltip("월드 단위. 발밑과 진행 방향의 바닥을 아래로 검사하는 길이입니다. 너무 크면 아래층을 같은 바닥으로 인식할 수 있습니다.")]
    [SerializeField, Min(0.01f)] private float groundCheckDepth = 0.35f;

    [Header("연결 및 지형 레이어")]
    [Tooltip("벽·발판·지면으로 검사할 레이어입니다.")]
    [SerializeField] private LayerMask groundLayer = 1 << 8;
    [Tooltip("방향 전환 시 좌우 반전할 적 스프라이트입니다. 비워 두면 자식에서 찾습니다.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Rigidbody2D body;
    private CapsuleCollider2D capsule;
    private ContactFilter2D groundFilter;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[1];
    public int FacingDirection { get; private set; } = 1;
    public bool IsMoving { get; private set; }
    public float FeetY => capsule.bounds.min.y;
    public bool IsGrounded => capsule != null && capsule.enabled &&
        Physics2D.Raycast(new Vector2(capsule.bounds.center.x, capsule.bounds.min.y + 0.1f), Vector2.down, groundFilter, hits, groundCheckDepth) > 0;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        capsule = GetComponent<CapsuleCollider2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        FacingDirection = spriteRenderer != null && spriteRenderer.flipX ? -1 : 1;
        groundFilter = new ContactFilter2D { useTriggers = false };
        groundFilter.SetLayerMask(groundLayer);
    }

    public void Face(float horizontalOffset)
    {
        if (Mathf.Abs(horizontalOffset) < 0.1f) return;
        FacingDirection = horizontalOffset > 0f ? 1 : -1;
        if (spriteRenderer != null) spriteRenderer.flipX = FacingDirection < 0;
    }

    public void Move(float horizontalOffset, float remainingDistance, float deltaTime)
    {
        Face(horizontalOffset);
        if (!body.simulated || !capsule.enabled || !CanAdvance(FacingDirection))
        {
            Stop();
            return;
        }
        float speed = Mathf.Min(moveSpeed, Mathf.Max(0f, remainingDistance) / Mathf.Max(deltaTime, 0.0001f));
        body.linearVelocity = new Vector2(FacingDirection * speed, body.linearVelocity.y);
        IsMoving = speed > 0.01f;
    }

    private bool CanAdvance(int direction)
    {
        Bounds bounds = capsule.bounds;
        Vector2 front = new Vector2(bounds.center.x + direction * (bounds.extents.x + 0.01f), bounds.center.y);
        if (Physics2D.Raycast(front, Vector2.right * direction, groundFilter, hits, wallCheckDistance) > 0) return false;
        Vector2 foot = new Vector2(bounds.center.x, bounds.min.y + 0.1f);
        if (Physics2D.Raycast(foot, Vector2.down, groundFilter, hits, groundCheckDepth) == 0) return false;
        foot.x += direction * (bounds.extents.x + ledgeLookAhead);
        return Physics2D.Raycast(foot, Vector2.down, groundFilter, hits, groundCheckDepth) > 0;
    }

    public void Stop()
    {
        IsMoving = false;
        if (body != null && body.simulated) body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private void OnDisable() => Stop();
}
