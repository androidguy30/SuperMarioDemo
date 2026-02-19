using UnityEngine;

/// <summary>
/// Collectible coin that the player can pick up.
/// Animated spinning effect and auto-destroys on collection.
/// </summary>
public class CoinPickup : MonoBehaviour
{
    [SerializeField] private float bobSpeed = 3f;
    [SerializeField] private float bobHeight = 0.15f;

    private Vector3 startPos;

    private void Start()
    {
        startPos = transform.position;
    }

    private void Update()
    {
        // Floating bob animation
        float yOffset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = startPos + Vector3.up * yOffset;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            GameManager.Instance?.AddCoin();
            Destroy(gameObject);
        }
    }
}
