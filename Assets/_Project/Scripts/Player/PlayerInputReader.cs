using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-300)]
public sealed class PlayerInputReader : MonoBehaviour
{

    [Header("조작 입력 연결")]
    [Tooltip("플레이어 입력 액션 원본입니다. 키 변경은 이 에셋의 Player 액션 맵에서 설정합니다.")]
    [SerializeField] private InputActionAsset inputActions;
    private InputActionAsset runtimeActions;
    private InputAction move, jump, attack, dodge, parry;
    private bool jumpRequested, attackRequested, dodgeRequested, parryRequested;
    public float Horizontal => isActiveAndEnabled && move != null ? move.ReadValue<Vector2>().x : 0f;

    private void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogError("Assign the input actions asset to PlayerInputReader.", this);
            enabled = false;
            return;
        }
        runtimeActions = Instantiate(inputActions);
        move = runtimeActions.FindAction("Player/Move", true);
        jump = runtimeActions.FindAction("Player/Jump", true);
        attack = runtimeActions.FindAction("Player/Attack", true);
        dodge = runtimeActions.FindAction("Player/Dodge", true);
        parry = runtimeActions.FindAction("Player/Parry", true);
        jump.performed += OnJump;
        attack.performed += OnAttack;
        dodge.performed += OnDodge;
        parry.performed += OnParry;
    }

    private void OnEnable() => runtimeActions?.FindActionMap("Player", true).Enable();
    private void OnJump(InputAction.CallbackContext _) => jumpRequested = true;
    private void OnAttack(InputAction.CallbackContext _) => attackRequested = true;
    private void OnDodge(InputAction.CallbackContext _) => dodgeRequested = true;
    private void OnParry(InputAction.CallbackContext _) => parryRequested = true;

    public void Consume(out bool jumpPressed, out bool attackPressed, out bool dodgePressed)
    {
        Consume(out jumpPressed, out attackPressed, out dodgePressed, out _);
    }

    public void Consume(out bool jumpPressed, out bool attackPressed, out bool dodgePressed, out bool parryPressed)
    {
        jumpPressed = jumpRequested;
        attackPressed = attackRequested;
        dodgePressed = dodgeRequested;
        parryPressed = parryRequested;
        Clear();
    }

    public void Clear() => jumpRequested = attackRequested = dodgeRequested = parryRequested = false;
    private void OnDisable()
    {
        runtimeActions?.Disable();
        Clear();
    }
    private void OnDestroy()
    {
        if (runtimeActions != null) Destroy(runtimeActions);
    }
}
