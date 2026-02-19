using UnityEngine;

/// <summary>
/// Base enemy controller for Goomba and Koopa enemies.
/// Handles patrol movement, stomping, and player collision.
/// </summary>
public class EnemyController : MonoBehaviour
{
    public enum EnemyType { Goomba, Koopa }

    [Header("Settings")]
    [SerializeField] private EnemyType enemyType = EnemyType.Goomba;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float shellSpeed = 10f;
    [SerializeField] private float activationDistance = 16f;
    [SerializeField] private int scoreValue = 100;

    [Header("Ground Detection")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float checkRadius = 0.15f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private bool isActive;
    private bool isDead;
    private bool isShell;
    private bool shellMoving;
    private int moveDirection = -1;
    private Transform playerTransform;

    private static readonly int AnimIsDead = Animator.StringToHash("isDead");
    private static readonly int AnimIsShell = Animator.StringToHash("isShell");

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        playerTransform = FindFirstObjectByType<PlayerController>()?.transform;
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private void Update()
    {
        if (isDead && !isShell) return;

        // Activate when player gets close
        if (!isActive && playerTransform != null)
        {
            float dist = Mathf.Abs(playerTransform.position.x - transform.position.x);
            if (dist < activationDistance)
            {
                isActive = true;
                rb.bodyType = RigidbodyType2D.Dynamic;
            }
        }

        if (!isActive) return;

        // Check for wall/edge to turn around
        if (!isShell || !shellMoving)
        {
            bool hitWall = Physics2D.OverlapCircle(wallCheck.position, checkRadius, groundLayer);
            bool onEdge = !Physics2D.OverlapCircle(groundCheck.position, checkRadius, groundLayer);

            if (hitWall || onEdge)
            {
                moveDirection *= -1;
            }
        }

        // Flip sprite based on movement direction
        spriteRenderer.flipX = moveDirection > 0;
    }

    private void FixedUpdate()
    {
        if (!isActive || (isDead && !isShell)) return;

        if (isShell && shellMoving)
        {
            rb.linearVelocity = new Vector2(moveDirection * shellSpeed, rb.linearVelocity.y);
        }
        else if (!isShell)
        {
            rb.linearVelocity = new Vector2(moveDirection * moveSpeed, rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead && !isShell) return;

        // Player collision
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerController player = collision.gameObject.GetComponent<PlayerController>();
            if (player == null || player.IsDead) return;

            ContactPoint2D contact = collision.GetContact(0);
            float dotProduct = Vector2.Dot(contact.normal, Vector2.up);

            // Stomped from above
            if (dotProduct > 0.6f)
            {
                OnStomped(player);
            }
            else
            {
                // Player hit from side - check if it's a stationary shell
                if (isShell && !shellMoving)
                {
                    KickShell(player.transform.position.x < transform.position.x ? 1 : -1);
                    return;
                }

                // Damage player
                player.Die();
            }
        }

        // Shell kills other enemies
        if (isShell && shellMoving && collision.gameObject.CompareTag("Enemy"))
        {
            EnemyController other = collision.gameObject.GetComponent<EnemyController>();
            if (other != null && !other.isDead)
            {
                other.DieFromShell();
                GameManager.Instance?.AddScore(200);
            }
        }

        // Shell bounces off walls
        if (isShell && shellMoving)
        {
            if (collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                ContactPoint2D contact = collision.GetContact(0);
                if (Mathf.Abs(contact.normal.x) > 0.5f)
                {
                    moveDirection *= -1;
                }
            }
        }
    }

    private void OnStomped(PlayerController player)
    {
        GameManager.Instance?.AddScore(scoreValue);
        GameManager.Instance?.PlayStompSFX();
        player.StompBounce();

        if (enemyType == EnemyType.Goomba)
        {
            isDead = true;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            GetComponent<Collider2D>().enabled = false;

            if (animator != null)
                animator.SetBool(AnimIsDead, true);

            // Flatten sprite
            transform.localScale = new Vector3(transform.localScale.x, 0.3f, 1f);

            Destroy(gameObject, 0.5f);
        }
        else if (enemyType == EnemyType.Koopa)
        {
            if (!isShell)
            {
                // Enter shell state
                isShell = true;
                shellMoving = false;
                rb.linearVelocity = Vector2.zero;
                if (animator != null)
                    animator.SetBool(AnimIsShell, true);
            }
            else if (shellMoving)
            {
                // Stop the shell
                shellMoving = false;
                rb.linearVelocity = Vector2.zero;
            }
            else
            {
                // Kick the shell
                KickShell(player.transform.position.x < transform.position.x ? 1 : -1);
            }
        }
    }

    private void KickShell(int direction)
    {
        shellMoving = true;
        moveDirection = direction;
    }

    public void DieFromShell()
    {
        isDead = true;
        isShell = false;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = new Vector2(0, 5f);
        GetComponent<Collider2D>().enabled = false;
        spriteRenderer.flipY = true;
        Destroy(gameObject, 2f);
    }

    private void OnBecameInvisible()
    {
        if (isDead) Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (groundCheck != null)
            Gizmos.DrawWireSphere(groundCheck.position, checkRadius);
        if (wallCheck != null)
            Gizmos.DrawWireSphere(wallCheck.position, checkRadius);
    }
}
