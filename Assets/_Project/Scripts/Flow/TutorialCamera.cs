using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class TutorialCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float leftBound = -5f;
    [SerializeField] private float rightBound = 48f;
    [SerializeField] private float height = 1.8f;
    [SerializeField, Min(.01f)] private float smoothTime = .15f;
    private Camera view;
    private Vector3 velocity;
    private void Awake() => view = GetComponent<Camera>();
    private Vector3 Destination()
    {
        float halfWidth = view.orthographicSize * view.aspect;
        float x = rightBound - leftBound > halfWidth * 2 ? Mathf.Clamp(target.position.x, leftBound + halfWidth, rightBound - halfWidth) : (leftBound + rightBound) * .5f;
        return new Vector3(x, height, -10);
    }
    private void Start() => Snap();
    public void Snap() { velocity = Vector3.zero; transform.position = Destination(); }
    private void LateUpdate() { if (target != null) transform.position = Vector3.SmoothDamp(transform.position, Destination(), ref velocity, smoothTime); }
}
