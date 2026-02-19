using UnityEngine;

/// <summary>
/// Controls the Mario player character - movement, jumping, physics, and state.
/// Attach to the Player GameObject with a Rigidbody2D and BoxCollider2D.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 9f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float deceleration = 16f;

    [Header("Jumping")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float jumpCutMultiplier = 0.4f;
    [SerializeField] private float fallGravityMultiplier = 2.5f;
    [SerializeField] private float maxFallSpeed = 18f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.15f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.8f, 0.1f);
    [SerializeField] private LayerMask groundLayer;

    [Header("Head Check")]
    [SerializeField] private Transform headCheck;
    [SerializeField] private Vector2 headCheckSize = new Vector2(0.6f, 0.1f);

    // Components
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    // State
    private float horizontalInput;
    private bool isRunning;
    private bool isGrounded;
    private bool wasGrounded;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool isJumping;
    private bool isDead;
    private bool isFacingRight = true;
    private bool reachedFlag;

    // Animation hashes
    private static readonly int AnimIsRunning = Animator.StringToHash("isRunning");
    private static readonly int AnimIsJumping = Animator.StringToHash("isJumping");
    private static readonly int AnimAbsSpeed = Animator.StringToHash("absSpeed");
    private static readonly int AnimIsDead = Animator.StringToHash("isDead");

    public bool IsDead => isDead;
    public bool IsGrounded => isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (isDead || reachedFlag) return;

        GatherInput();
        CheckGround();
        HandleJumpBuffer();
        HandleCoyoteTime();
        FlipSprite();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        if (reachedFlag)
        {
            HandleFlagWalk();
            return;
        }

        ApplyMovement();
        ApplyJump();
        ApplyGravity();
    }

    private void GatherInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferTimer = jumpBufferTime;
        }

        if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0 && isJumping)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
            isJumping = false;
        }
    }

    private void CheckGround()
    {
        wasGrounded = isGrounded;
        isGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);

        if (isGrounded && !wasGrounded)
        {
            isJumping = false;
        }
    }

    private void HandleJumpBuffer()
    {
        if (jumpBufferTimer > 0)
            jumpBufferTimer -= Time.deltaTime;
    }

    private void HandleCoyoteTime()
    {
        if (isGrounded)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer -= Time.deltaTime;
    }

    private void ApplyMovement()
    {
        float targetSpeed = horizontalInput * (isRunning ? runSpeed : walkSpeed);
        float speedDiff = targetSpeed - rb.linearVelocity.x;
        float accelRate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
        float movement = speedDiff * accelRate * Time.fixedDeltaTime;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x + movement, rb.linearVelocity.y);
    }

    private void ApplyJump()
    {
        if (jumpBufferTimer > 0 && coyoteTimer > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferTimer = 0;
            coyoteTimer = 0;
            isJumping = true;
            isGrounded = false;
        }
    }

    private void ApplyGravity()
    {
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallGravityMultiplier - 1) * Time.fixedDeltaTime;
        }

        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
        }
    }

    private void FlipSprite()
    {
        if (horizontalInput > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (horizontalInput < 0 && isFacingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;
        animator.SetBool(AnimIsJumping, !isGrounded);
        animator.SetFloat(AnimAbsSpeed, Mathf.Abs(rb.linearVelocity.x));
        animator.SetBool(AnimIsDead, isDead);
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(Vector2.up * 10f, ForceMode2D.Impulse);
        GetComponent<Collider2D>().enabled = false;

        if (animator != null)
            animator.SetBool(AnimIsDead, true);

        GameManager.Instance.OnPlayerDeath();
    }

    public void OnReachedFlag()
    {
        reachedFlag = true;
        rb.linearVelocity = Vector2.zero;
    }

    private void HandleFlagWalk()
    {
        rb.linearVelocity = new Vector2(2f, rb.linearVelocity.y);

        if (!isFacingRight)
            Flip();
    }

    public void StompBounce()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce * 0.65f);
    }

    public void CheckHeadHit()
    {
        Collider2D hit = Physics2D.OverlapBox(headCheck.position, headCheckSize, 0f, groundLayer);
        if (hit != null)
        {
            BlockBase block = hit.GetComponent<BlockBase>();
            if (block != null)
            {
                block.OnHitFromBelow(this);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }
        if (headCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(headCheck.position, headCheckSize);
        }
    }
}
