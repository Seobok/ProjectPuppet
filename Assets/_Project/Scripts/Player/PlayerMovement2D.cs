using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMovement2D : MonoBehaviour
{
    private static readonly int Running = Animator.StringToHash("isRunning");
    private static readonly int Grounded = Animator.StringToHash("isGrounded");
    private static readonly int Jump = Animator.StringToHash("Jump");
    private static readonly int VerticalSpeed = Animator.StringToHash("verticalSpeed");
    private static readonly int WallSlide = Animator.StringToHash("IsWallSlide");
    
    [Header("InputAction")]
    [SerializeField] private InputActionAsset inputActions;
    [Header("Components")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerCombat combat;
    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.3f, 0.1f);
    [SerializeField] private LayerMask groundLayer;
    [Header("Movement Settings")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(0f)] private float jumpSpeed = 8f;
    [SerializeField, Min(0.1f)] private float wallSlideSpeed = 3f;

    private Rigidbody2D body;
    private InputActionAsset runtimeActions;
    private InputAction moveAction;
    private InputAction jumpAction;
    private bool jumpRequested;
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
        if(combat == null)
            combat = GetComponent<PlayerCombat>();

        if (inputActions == null)
        {
            Debug.LogError("Assign the input actions asset to PlayerMovement2D.", this);
            enabled = false;
            return;
        }

        runtimeActions = Instantiate(inputActions);
        moveAction = runtimeActions.FindAction("Player/Move", true);
        jumpAction = runtimeActions.FindAction("Player/Jump", true);
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        if (jumpAction != null)
        {
            jumpAction.performed += OnJump;
            jumpAction.Enable();
        }
    }
    
    private void FixedUpdate()
    {
        IsGrounded = CheckGrounded();
        IsWallSlide = false;
        bool blocked = combat != null && combat.BlocksMovement;
        
        // 이동
        float horizontal = 0f;
        if(!blocked)
            horizontal = moveAction.ReadValue<Vector2>().x;
        body.linearVelocity = new Vector2(horizontal * moveSpeed, body.linearVelocity.y);

        // 점프
        if (jumpRequested && IsGrounded && !blocked)
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpSpeed);
            IsGrounded = false;
            animator.SetTrigger(Jump);
        }
        jumpRequested = false;

        // 벽타기
        if (!IsGrounded && body.linearVelocity.y < -wallSlideSpeed && IsPressingIntoWall(horizontal))
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, -wallSlideSpeed);
            IsWallSlide = true;
        }

        if (spriteRenderer != null && Mathf.Abs(horizontal) > 0.01f)
        {
            spriteRenderer.flipX = horizontal < 0f;
        }

        if (animator != null)
        {
            animator.SetBool(Running, body.linearVelocityX != 0f);
            animator.SetBool(Grounded, IsGrounded);
            animator.SetFloat(VerticalSpeed, body.linearVelocity.y);
            animator.SetBool(WallSlide, IsWallSlide);
        }
    }

    // Jump 버튼 입력 이벤트
    private void OnJump(InputAction.CallbackContext context)
    {
        jumpRequested = true;
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

    private bool CheckGrounded()
    {
        if (groundCheck == null || body.linearVelocity.y > 0.05f)
            return false;

        var filter = new ContactFilter2D();
        filter.SetLayerMask(groundLayer);
        filter.useTriggers = false;
        int count = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, filter, groundHits);
        for (int i = 0; i < count; i++)
        {
            if (groundHits[i].attachedRigidbody != body)
                return true;
        }
        return false;
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        if (jumpAction != null)
        {
            jumpAction.performed -= OnJump;
            jumpAction.Disable();
        }
        jumpRequested = false;
        IsGrounded = false;
        if (body != null)
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private void OnDestroy()
    {
        if (runtimeActions != null)
            Destroy(runtimeActions);
    }
}
