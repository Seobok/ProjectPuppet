using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-300)]
public sealed class PlayerInputReader : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    private InputActionAsset runtimeActions;
    private InputAction move, jump, attack, dodge;
    private bool jumpRequested, attackRequested, dodgeRequested;
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
        jump.performed += OnJump;
        attack.performed += OnAttack;
        dodge.performed += OnDodge;
    }

    private void OnEnable() => runtimeActions?.FindActionMap("Player", true).Enable();
    private void OnJump(InputAction.CallbackContext _) => jumpRequested = true;
    private void OnAttack(InputAction.CallbackContext _) => attackRequested = true;
    private void OnDodge(InputAction.CallbackContext _) => dodgeRequested = true;

    public void Consume(out bool jumpPressed, out bool attackPressed, out bool dodgePressed)
    {
        jumpPressed = jumpRequested;
        attackPressed = attackRequested;
        dodgePressed = dodgeRequested;
        Clear();
    }

    public void Clear() => jumpRequested = attackRequested = dodgeRequested = false;
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
