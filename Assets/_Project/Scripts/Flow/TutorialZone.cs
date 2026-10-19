using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class TutorialZone : MonoBehaviour
{
    [SerializeField] private TutorialLesson lesson;
    [SerializeField] private TutorialDirector director;
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerController>() != null) director.EnterLesson(lesson);
    }
}
