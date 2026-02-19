using UnityEngine;

/// <summary>
/// Invisible trigger zone placed below the level.
/// Kills the player when they fall into a pit.
/// </summary>
public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null && !player.IsDead)
            {
                player.Die();
            }
        }

        // Destroy any enemy that falls off the map
        if (other.CompareTag("Enemy"))
        {
            Destroy(other.gameObject);
        }
    }
}
