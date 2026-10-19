using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;

public static class VerifyPlayerResponseInputs
{
    public static async Task<object> Main()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Requires Play mode");
        GameObject player = null;
        Keyboard keyboard = null; Mouse mouse = null; Gamepad pad = null;
        bool background = Application.runInBackground;
        bool paused = EditorApplication.isPaused;
        var focus = InputSystem.settings.editorInputBehaviorInPlayMode;
        var backgroundInput = InputSystem.settings.backgroundBehavior;
        try
        {
            Application.runInBackground = true;
            EditorApplication.isPaused = false;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>(); pad = InputSystem.AddDevice<Gamepad>();
            player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab"), new Vector3(20000, 20000), Quaternion.identity);
            var pc = player.GetComponent<PlayerController>(); pc.enabled = false;
            player.GetComponent<Rigidbody2D>().simulated = false;
            var input = player.GetComponent<PlayerInputReader>();
            var actions = (InputActionAsset)typeof(PlayerInputReader).GetField("runtimeActions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(input);
            actions.devices = new InputDevice[] { keyboard, mouse, pad };
            await Task.Delay(100);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            await Task.Delay(100); input.Consume(out _, out _, out _, out bool f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Task.Delay(50);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            await Task.Delay(100); input.Consume(out _, out _, out _, out bool right);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            await Task.Delay(50);
            InputSystem.QueueStateEvent(pad, new GamepadState { leftTrigger = 1f, leftStick = Vector2.left });
            await Task.Delay(100); input.Consume(out _, out _, out _, out bool trigger);
            float horizontal = input.Horizontal;
            player.GetComponent<PlayerCombat>().FaceAction(horizontal);
            bool facesLeft = player.GetComponentInChildren<SpriteRenderer>().flipX;
            if (!f || !right || !trigger || horizontal > -0.9f || !facesLeft)
                throw new Exception("Input results F=" + f + " mouse=" + right + " trigger=" + trigger + " move=" + horizontal);
            return new { passed = true, keyboardF = f, rightMouse = right, gamepadLeftTrigger = trigger, leftDirection = horizontal, facesLeft };
        }
        finally
        {
            if (player != null) UnityEngine.Object.DestroyImmediate(player);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (pad != null) InputSystem.RemoveDevice(pad);
            Application.runInBackground = background;
            EditorApplication.isPaused = paused;
            InputSystem.settings.editorInputBehaviorInPlayMode = focus;
            InputSystem.settings.backgroundBehavior = backgroundInput;
        }
    }
}
