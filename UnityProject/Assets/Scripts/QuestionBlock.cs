using UnityEngine;

/// <summary>
/// Question block that spawns coins or power-ups when hit from below.
/// </summary>
public class QuestionBlock : BlockBase
{
    public enum Contents { Coin, Mushroom, Star }

    [SerializeField] private Contents blockContents = Contents.Coin;
    [SerializeField] private GameObject coinEffectPrefab;
    [SerializeField] private GameObject mushroomPrefab;
    [SerializeField] private Sprite usedSprite;

    private SpriteRenderer spriteRenderer;

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public override void OnHitFromBelow(PlayerController player)
    {
        if (isUsed) return;
        base.OnHitFromBelow(player);

        isUsed = true;

        // Change to used block appearance
        if (usedSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = usedSprite;
        }

        // Spawn contents
        switch (blockContents)
        {
            case Contents.Coin:
                SpawnCoin();
                break;
            case Contents.Mushroom:
                SpawnMushroom();
                break;
        }
    }

    private void SpawnCoin()
    {
        GameManager.Instance?.AddCoin();
        GameManager.Instance?.AddScore(200);

        if (coinEffectPrefab != null)
        {
            GameObject coinFX = Instantiate(coinEffectPrefab, transform.position + Vector3.up, Quaternion.identity);
            Destroy(coinFX, 0.5f);
        }
    }

    private void SpawnMushroom()
    {
        if (mushroomPrefab != null)
        {
            Instantiate(mushroomPrefab, transform.position + Vector3.up, Quaternion.identity);
        }
        GameManager.Instance?.PlayPowerupSFX();
    }
}
