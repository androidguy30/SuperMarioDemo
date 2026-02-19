using UnityEngine;

/// <summary>
/// Smooth camera follow script that tracks the player with a forward look bias.
/// Camera only moves right (classic Mario behavior) unless at level boundaries.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Settings")]
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private Vector3 offset = new Vector3(3f, 1f, -10f);
    [SerializeField] private float leftBound = 0f;
    [SerializeField] private float rightBound = 210f;
    [SerializeField] private float verticalMin = -2f;
    [SerializeField] private float verticalMax = 2f;

    private float maxX;

    private void Start()
    {
        if (target == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                target = player.transform;
        }
        maxX = leftBound;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;

        // Only scroll right (classic Mario)
        if (desiredPosition.x > maxX)
            maxX = desiredPosition.x;

        desiredPosition.x = maxX;

        // Clamp vertical
        desiredPosition.y = Mathf.Clamp(desiredPosition.y, verticalMin, verticalMax);

        // Clamp to level bounds
        float halfWidth = Camera.main.orthographicSize * Camera.main.aspect;
        desiredPosition.x = Mathf.Clamp(desiredPosition.x, leftBound + halfWidth, rightBound - halfWidth);

        // Smooth follow
        Vector3 smoothed = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        smoothed.z = offset.z;
        transform.position = smoothed;
    }
}
