using UnityEngine;

/// <summary>
/// Base class for interactive blocks (bricks, question blocks).
/// Handles hit-from-below animation and item spawning.
/// </summary>
public abstract class BlockBase : MonoBehaviour
{
    [SerializeField] protected float bumpHeight = 0.3f;
    [SerializeField] protected float bumpSpeed = 8f;
    [SerializeField] protected bool isUsed;

    protected Vector3 originalPosition;
    protected bool isBumping;
    protected float bumpProgress;

    protected virtual void Awake()
    {
        originalPosition = transform.position;
    }

    protected virtual void Update()
    {
        if (isBumping)
        {
            bumpProgress += Time.deltaTime * bumpSpeed;
            float yOffset = Mathf.Sin(bumpProgress * Mathf.PI) * bumpHeight;
            transform.position = originalPosition + Vector3.up * yOffset;

            if (bumpProgress >= 1f)
            {
                isBumping = false;
                bumpProgress = 0f;
                transform.position = originalPosition;
            }
        }
    }

    public virtual void OnHitFromBelow(PlayerController player)
    {
        if (isUsed) return;
        isBumping = true;
        bumpProgress = 0f;
    }
}
