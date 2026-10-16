using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMovement2D : MonoBehaviour
{
    private static readonly int Running = Animator.StringToHash("isRunning");
    private static readonly int Grounded = Animator.StringToHash("isGrounded");
    private static readonly int Jump = Animator.StringToHash("Jump");
    private static readonly int VerticalSpeed = Animator.StringToHash("verticalSpeed");
    private static readonly int WallSlide = Animator.StringToHash("IsWallSlide");
    
    [Header("Components")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.3f, 0.1f);
    [SerializeField] private LayerMask groundLayer;
    [Header("Movement Settings")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float jumpSpeed = 8f;
    [SerializeField, Min(0.1f)] private float wallSlideSpeed = 3f;

    private Rigidbody2D body;
    private readonly Collider2D[] groundHits = new Collider2D[8];
    private readonly ContactPoint2D[] wallContacts = new ContactPoint2D[16];

    public bool IsGrounded { get; private set; }
    public bool IsWallSlide { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public void UpdateGroundedState()
    {
        if (groundCheck == null || body.linearVelocity.y > 0.05f)
        {
            IsGrounded = false;
            return;
        }

        var filter = new ContactFilter2D();
        filter.SetLayerMask(groundLayer);
        filter.useTriggers = false;
        int count = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, filter, groundHits);
        for (int i = 0; i < count; i++)
        {
            if (groundHits[i].attachedRigidbody != body)
            {
                IsGrounded = true;
                return;
            }
        }
        
        IsGrounded = false;
    }

    public void Tick(float input, bool jumpRequested, bool canMove, bool dodging, float dodgeVelocity)
    {
        IsWallSlide = false;
        float horizontal = canMove ? input : 0f;
        body.linearVelocity = new Vector2(dodging ? dodgeVelocity : horizontal * moveSpeed, body.linearVelocity.y);
        
        if (jumpRequested && IsGrounded && canMove)
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpSpeed);
            IsGrounded = false;
            if (animator != null) animator.SetTrigger(Jump);
        }
        
        if (canMove && !IsGrounded && body.linearVelocity.y < -wallSlideSpeed && IsPressingIntoWall(horizontal))
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, -wallSlideSpeed);
            IsWallSlide = true;
        }
        
        if (spriteRenderer != null && Mathf.Abs(horizontal) > 0.01f)
            spriteRenderer.flipX = horizontal < 0f;
        
        if (animator != null)
        {
            animator.SetBool(Running, body.linearVelocityX != 0f);
            animator.SetBool(Grounded, IsGrounded);
            animator.SetFloat(VerticalSpeed, body.linearVelocity.y);
            animator.SetBool(WallSlide, IsWallSlide);
        }
    }

    private bool IsPressingIntoWall(float horizontal)
    {
        if (Mathf.Abs(horizontal) < 0.01f)
            return false;

        int count = body.GetContacts(wallContacts);
        for (int i = 0; i < count; i++)
        {
            Vector2 normal = wallContacts[i].normal;
            if (Mathf.Abs(normal.x) > 0.9f && normal.x * horizontal < 0f)
                return true;
        }
        return false;
    }

    private void OnDisable()
    {
        IsGrounded = false;
        if (body != null)
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

}
