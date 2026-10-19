using System.Collections.Generic;
using UnityEngine;

public enum TutorialLesson { Movement, Jump, Attack, Dodge, Parry, Boss }

// Only survives scene reloads within this play session. No disk saves or persistent GameObjects.
public static class RunSession
{
    public static string ScenePath { get; private set; }
    public static string CheckpointId { get; private set; } = "start";
    public static int CheckpointOrder { get; private set; }
    private static readonly HashSet<TutorialLesson> completed = new HashSet<TutorialLesson>();
    public static bool IsComplete(TutorialLesson lesson) => completed.Contains(lesson);
    public static void Complete(TutorialLesson lesson) => completed.Add(lesson);
    public static void EnterScene(string path)
    {
        if (ScenePath == path) return;
        NewRun(); ScenePath = path;
    }
    public static bool ActivateCheckpoint(string id, int order)
    {
        if (string.IsNullOrEmpty(id) || order <= CheckpointOrder) return false;
        CheckpointId = id; CheckpointOrder = order; return true;
    }
    public static void NewRun()
    {
        CheckpointId = "start"; CheckpointOrder = 0; completed.Clear();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { ScenePath = null; NewRun(); }
}
