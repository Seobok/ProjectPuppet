using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
public static class SetupPlayerDamageAnimations
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play mode first.");
        var c = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Warrior free set/Aniamtion/Warrior.controller");
        var sm = c.layers[0].stateMachine;
        var hurt = sm.states.Single(s => s.state.name == "Hurt").state;
        var death = sm.states.Single(s => s.state.name == "Death").state;
        var idle = sm.states.Single(s => s.state.name == "Idle").state;
        foreach (var name in new[] { "isHurt", "isDead", "Hurt", "HurtSpeed" })
            if (!c.parameters.Any(p => p.name == name)) c.AddParameter(name, name == "Hurt" ? AnimatorControllerParameterType.Trigger : name == "HurtSpeed" ? AnimatorControllerParameterType.Float : AnimatorControllerParameterType.Bool);
        hurt.speed = ((AnimationClip)hurt.motion).length;
        hurt.speedParameter = "HurtSpeed"; hurt.speedParameterActive = true;
        foreach (var state in new[] { hurt, death })
        {
            var clip = (AnimationClip)state.motion;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            foreach (var t in state.transitions.ToArray()) state.RemoveTransition(t);
        }
        foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState == hurt || t.destinationState == death).ToArray()) sm.RemoveAnyStateTransition(t);
        foreach (var t in sm.anyStateTransitions)
            foreach (var name in new[] { "isHurt", "isDead" })
                if (!t.conditions.Any(x => x.parameter == name)) t.AddCondition(AnimatorConditionMode.IfNot, 0, name);
        var die = sm.AddAnyStateTransition(death);
        die.hasExitTime = false; die.duration = 0; die.canTransitionToSelf = false;
        die.AddCondition(AnimatorConditionMode.If, 0, "isDead");
        var hit = sm.AddAnyStateTransition(hurt);
        hit.hasExitTime = false; hit.duration = 0; hit.canTransitionToSelf = true;
        hit.AddCondition(AnimatorConditionMode.If, 0, "Hurt");
        hit.AddCondition(AnimatorConditionMode.IfNot, 0, "isDead");
        var recover = hurt.AddTransition(idle);
        recover.hasExitTime = false; recover.duration = 0;
        recover.AddCondition(AnimatorConditionMode.IfNot, 0, "isHurt");
        recover.AddCondition(AnimatorConditionMode.IfNot, 0, "isDead");
        EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return new { configured = true, hurt = hurt.motion.name, death = death.motion.name };
    }
}
