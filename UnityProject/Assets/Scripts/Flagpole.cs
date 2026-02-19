using UnityEngine;

/// <summary>
/// Flagpole trigger at end of level.
/// When player touches it, triggers the win sequence.
/// </summary>
public class Flagpole : MonoBehaviour
{
    [SerializeField] private Transform flag;
    [SerializeField] private Transform flagBottom;
    [SerializeField] private float flagSlideSpeed = 5f;

    private bool triggered;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            player.OnReachedFlag();
        }

        // Calculate score based on flag height
        float flagHeight = other.transform.position.y - flagBottom.position.y;
        float maxHeight = transform.position.y - flagBottom.position.y;
        float ratio = Mathf.Clamp01(flagHeight / maxHeight);

        int flagScore;
        if (ratio > 0.9f) flagScore = 5000;
        else if (ratio > 0.7f) flagScore = 2000;
        else if (ratio > 0.5f) flagScore = 800;
        else if (ratio > 0.2f) flagScore = 400;
        else flagScore = 100;

        GameManager.Instance?.AddScore(flagScore);
        GameManager.Instance?.OnFlagReached();
    }

    private void Update()
    {
        if (!triggered || flag == null || flagBottom == null) return;

        // Slide flag down the pole
        if (flag.position.y > flagBottom.position.y)
        {
            flag.position = Vector3.MoveTowards(flag.position, flagBottom.position, flagSlideSpeed * Time.deltaTime);
        }
    }
}
