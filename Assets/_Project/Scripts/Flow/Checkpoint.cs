using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class Checkpoint : MonoBehaviour
{
    [SerializeField] private string checkpointId = "boss";
    [SerializeField, Min(1)] private int order = 1;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private RetryController flow;
    [SerializeField] private SpriteRenderer marker;
    public string Id => checkpointId;
    public int Order => order;
    public Transform SpawnPoint => spawnPoint;
    public bool TryActivate(PlayerController player) => player == flow.Player && flow.ActivateCheckpoint(this);
    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController>();
        if (player != null) TryActivate(player);
    }
    public void RefreshVisual()
    {
        if (marker != null) marker.color = RunSession.CheckpointId == checkpointId ? new Color(1f, .8f, .35f) : new Color(.35f, .65f, .75f);
    }
}
