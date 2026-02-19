using UnityEngine;

/// <summary>
/// Breakable brick block. Breaks when hit from below.
/// Spawns particle debris on destruction.
/// </summary>
public class BrickBlock : BlockBase
{
    [SerializeField] private GameObject debrisPrefab;
    [SerializeField] private int debrisCount = 4;
    [SerializeField] private float debrisForce = 5f;

    public override void OnHitFromBelow(PlayerController player)
    {
        base.OnHitFromBelow(player);

        // Break the brick
        SpawnDebris();
        GameManager.Instance?.AddScore(50);
        GameManager.Instance?.PlayBrickBreakSFX();
        Destroy(gameObject);
    }

    private void SpawnDebris()
    {
        if (debrisPrefab == null) return;

        for (int i = 0; i < debrisCount; i++)
        {
            GameObject piece = Instantiate(debrisPrefab, transform.position, Quaternion.identity);
            Rigidbody2D debrisRb = piece.GetComponent<Rigidbody2D>();
            if (debrisRb != null)
            {
                float xDir = (i % 2 == 0) ? -1f : 1f;
                float yDir = (i < 2) ? 1.5f : 1f;
                Vector2 force = new Vector2(xDir * debrisForce * Random.Range(0.5f, 1f),
                                            yDir * debrisForce);
                debrisRb.AddForce(force, ForceMode2D.Impulse);
                debrisRb.AddTorque(Random.Range(-10f, 10f));
            }
            Destroy(piece, 2f);
        }
    }
}
