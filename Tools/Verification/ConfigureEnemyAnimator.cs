using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public static class ConfigureEnemyAnimator
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play mode first.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Project/Art/Enemies/ArmedHumanoid/ArmedHumanoid.controller");
        var machine = controller.layers[0].stateMachine;
        var idle = machine.states.Single(s => s.state.name == "Idle").state;
        var hurt = machine.states.Single(s => s.state.name == "Hurt").state;
        var death = machine.states.Single(s => s.state.name == "Death").state;
        Undo.RegisterCompleteObjectUndo(controller, "Configure enemy animation parameters");
        foreach (var transition in machine.anyStateTransitions) machine.RemoveAnyStateTransition(transition);
        foreach (var child in machine.states)
            foreach (var transition in child.state.transitions) child.state.RemoveTransition(transition);
        foreach (var parameter in controller.parameters)
            if (parameter.name == "Hurt" || parameter.name == "IsDead") controller.RemoveParameter(parameter);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        machine.defaultState = idle;
        // Death has priority and cannot repeatedly re-enter itself.
        var die = machine.AddAnyStateTransition(death);
        die.hasExitTime = false; die.duration = 0f; die.canTransitionToSelf = false;
        die.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
        var hit = machine.AddAnyStateTransition(hurt);
        hit.hasExitTime = false; hit.duration = 0f; hit.canTransitionToSelf = true;
        hit.AddCondition(AnimatorConditionMode.If, 0f, "Hurt");
        hit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
        var recover = hurt.AddTransition(idle);
        recover.hasExitTime = true; recover.exitTime = 1f; recover.duration = 0f;
        recover.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
        machine.states = new[]
        {
            new ChildAnimatorState { state = idle, position = new Vector3(280, 80) },
            new ChildAnimatorState { state = hurt, position = new Vector3(520, 80) },
            new ChildAnimatorState { state = death, position = new Vector3(280, 240) }
        };
        machine.anyStatePosition = new Vector3(40, 240);
        machine.entryPosition = new Vector3(40, 80);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return new { configured = true, parameters = new[] { "Hurt (Trigger)", "IsDead (Bool)" } };
    }
}
