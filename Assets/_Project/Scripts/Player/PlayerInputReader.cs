using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

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
    public string BindingGroup { get; private set; } = "Keyboard&Mouse";
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
        move.performed += TrackDevice;
        jump.performed += OnJump;
        attack.performed += OnAttack;
        dodge.performed += OnDodge;
        parry.performed += OnParry;
    }

    private void OnEnable() => runtimeActions?.FindActionMap("Player", true).Enable();
    private void TrackDevice(InputAction.CallbackContext context) => BindingGroup = context.control.device is Gamepad ? "Gamepad" : "Keyboard&Mouse";
    private void OnJump(InputAction.CallbackContext context) { TrackDevice(context); jumpRequested = true; }
    private void OnAttack(InputAction.CallbackContext context) { TrackDevice(context); attackRequested = true; }
    private void OnDodge(InputAction.CallbackContext context) { TrackDevice(context); dodgeRequested = true; }
    private void OnParry(InputAction.CallbackContext context) { TrackDevice(context); parryRequested = true; }

    public string GetBindingLabel(string actionName)
    {
        var action = (runtimeActions != null ? runtimeActions : inputActions)?.FindAction("Player/" + actionName);
        if (action == null) return actionName;
        string label = action.GetBindingDisplayString(group: BindingGroup);
        if (!string.IsNullOrWhiteSpace(label)) return label;
        // This asset assigns groups to composite parts rather than the composite root.
        var parts = new List<string>();
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (!binding.isPartOfComposite || (binding.name != "left" && binding.name != "right") ||
                string.IsNullOrEmpty(binding.groups) || !binding.groups.Contains(BindingGroup)) continue;
            string part = action.GetBindingDisplayString(i);
            if (!string.IsNullOrWhiteSpace(part) && !parts.Contains(part)) parts.Add(part);
        }
        return parts.Count > 0 ? string.Join(" / ", parts) : actionName;
    }

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
