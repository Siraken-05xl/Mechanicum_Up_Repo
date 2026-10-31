using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    #region Referencias

    [Header("Referencias")]
    [SerializeField] Transform camTransform;
    [SerializeField] Animator playerAnimator;
    [SerializeField] Transform visualRoot;

    [Header("Audio de pasos")]
    [SerializeField] AudioSource footstepSource;
    [SerializeField] AudioClip footstepClip;

    #endregion

    #region Movimiento

    [Header("Movimiento")]
    [SerializeField] float speed = 10f;
    [SerializeField] float rotationSpeed = 720f;

    Vector2 moveInput;
    bool inputEnabled = true;

    int facingDirection = 1;

    Quaternion initialVisualRotation;
    Quaternion targetVisualRotation;

    #endregion

    #region Salto

    [Header("Salto")]
    [SerializeField] float jumpForce = 8f;
    [SerializeField] float fallMultiplier = 2.8f;
    [SerializeField] float jumpCutMultiplier = 2f;

    [Header("Respuesta del salto")]
    [SerializeField] float jumpBufferTime = 0.12f;
    [SerializeField] float coyoteTime = 0.12f;

    bool jumpHeld;
    float jumpBufferCounter;
    float coyoteTimeCounter;

    #endregion

    #region GroundCheck

    [Header("GroundCheck")]
    [SerializeField] Transform groundCheck;
    [SerializeField] float groundCheckRadius = 0.2f;
    [SerializeField] LayerMask groundLayer;

    bool isGrounded;
    bool wasGrounded;

    #endregion

    #region Dash

    [Header("Dash")]
    [SerializeField] float dashSpeed = 25f;
    [SerializeField] float dashDuration = 0.18f;
    [SerializeField] float dashCooldown = 0.7f;
    [SerializeField] bool allowAirDash = true;

    bool isDashing;
    float dashTimer;
    float dashCooldownTimer;

    int dashDirection = 1;

    #endregion

    #region Estado

    [Header("Estado")]
    private bool isDead = false;

    [HideInInspector]
    public bool isRespawning = false;

    #endregion

    #region PowerUp Visual

    [Header("PowerUp Visual")]
    [SerializeField] private GameObject electricPowerVisualPrefab;
    [SerializeField] private string powerAnchorName = "PowerUpHolder";

    #endregion

    #region Respawn VFX

    [Header("Respawn VFX")]
    [SerializeField] private GameObject respawnVfxPrefab;

    #endregion

    #region Partículas

    [Header("Partículas")]
    [SerializeField] ParticleSystem runParticles;
    [SerializeField] float minMoveVelocity = 0.1f;

    #endregion

    #region Audio Muerte

    [Header("Audio Muerte")]
    [SerializeField] AudioSource deathAudioSource;
    [SerializeField] AudioClip deathClip;

    #endregion

    Rigidbody rb;

    #region Inicialización

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.freezeRotation = true;

        rb.constraints |= RigidbodyConstraints.FreezePositionZ;

        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>();

        if (visualRoot == null && playerAnimator != null)
            visualRoot = playerAnimator.transform;

        if (visualRoot != null)
        {
            initialVisualRotation = visualRoot.localRotation;
            targetVisualRotation = initialVisualRotation;
        }

        if (camTransform == null && Camera.main != null)
            camTransform = Camera.main.transform;
    }

    #endregion

    #region Update

    private void Update()
    {
        if (isDead) return;

        CheckIfGrounded();

        HandleVisualRotation();

        HandleJumpTimers();

        UpdateAnimator();

        HandleFootsteps();

        HandleRunParticles();
    }

    private void FixedUpdate()
    {
        if (isDead) return;

        HandleDashCooldown();

        HandleJump();

        HandleMovement();

        HandleFalling();
    }

    #endregion

    #region Movimiento

    void HandleMovement()
    {
        if (isDashing)
        {
            dashTimer -= Time.fixedDeltaTime;

            rb.linearVelocity = new Vector3(
                dashDirection * dashSpeed,
                rb.linearVelocity.y,
                0f
            );

            if (dashTimer <= 0f)
            {
                isDashing = false;
            }

            return;
        }

        float horizontalMovement = inputEnabled
            ? moveInput.x
            : 0f;

        rb.linearVelocity = new Vector3(
            horizontalMovement * speed,
            rb.linearVelocity.y,
            0f
        );
    }

    #endregion

    #region Rotación Visual

    void HandleVisualRotation()
    {
        if (visualRoot == null) return;

        targetVisualRotation = facingDirection > 0
            ? initialVisualRotation
            : initialVisualRotation * Quaternion.Euler(0f, 180f, 0f);

        visualRoot.localRotation = Quaternion.RotateTowards(
            visualRoot.localRotation,
            targetVisualRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    #endregion

    #region Salto

    public void OnJump(InputAction.CallbackContext context)
    {
        if (isDead || !inputEnabled) return;

        if (context.performed)
        {
            jumpHeld = true;
            jumpBufferCounter = jumpBufferTime;
        }

        if (context.canceled)
        {
            jumpHeld = false;
        }
    }

    void HandleJumpTimers()
    {
        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else if (coyoteTimeCounter > 0f)
        {
            coyoteTimeCounter -= Time.deltaTime;
        }
    }

    void HandleJump()
    {
        if (jumpBufferCounter <= 0f) return;

        if (coyoteTimeCounter <= 0f) return;

        if (isDashing) return;

        Jump();
    }

    void Jump()
    {
        rb.linearVelocity = new Vector3(
            rb.linearVelocity.x,
            0f,
            0f
        );

        rb.AddForce(
            Vector3.up * jumpForce,
            ForceMode.Impulse
        );

        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;

        isGrounded = false;

        if (playerAnimator)
            playerAnimator.SetTrigger("JumpStart");
    }

    void HandleFalling()
    {
        if (isGrounded || isDashing) return;

        Vector3 velocity = rb.linearVelocity;

        if (velocity.y < 0f)
        {
            velocity.y += Physics.gravity.y *
                          (fallMultiplier - 1f) *
                          Time.fixedDeltaTime;
        }
        else if (velocity.y > 0f && !jumpHeld)
        {
            velocity.y += Physics.gravity.y *
                          (jumpCutMultiplier - 1f) *
                          Time.fixedDeltaTime;
        }

        rb.linearVelocity = velocity;
    }

    #endregion

    #region GroundCheck

    void CheckIfGrounded()
    {
        if (groundCheck == null) return;

        wasGrounded = isGrounded;

        isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundCheckRadius,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );

        if (!wasGrounded && isGrounded)
        {
            if (playerAnimator)
                playerAnimator.SetTrigger("JumpEnd");

            if (rb.linearVelocity.y < 0f)
            {
                rb.linearVelocity = new Vector3(
                    rb.linearVelocity.x,
                    0f,
                    0f
                );
            }
        }
    }

    #endregion

    #region Dash

    public void OnDash(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        if (isDead || !inputEnabled) return;

        if (isDashing || dashCooldownTimer > 0f)
            return;

        if (!allowAirDash && !isGrounded)
            return;

        StartDash();
    }

    void StartDash()
    {
        dashDirection = Mathf.Abs(moveInput.x) > 0.01f
            ? (moveInput.x > 0f ? 1 : -1)
            : facingDirection;

        facingDirection = dashDirection;

        isDashing = true;

        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        rb.linearVelocity = new Vector3(
            dashDirection * dashSpeed,
            rb.linearVelocity.y,
            0f
        );
    }

    void HandleDashCooldown()
    {
        if (dashCooldownTimer <= 0f) return;

        dashCooldownTimer -= Time.fixedDeltaTime;

        if (dashCooldownTimer < 0f)
            dashCooldownTimer = 0f;
    }

    #endregion

    #region Animator

    void UpdateAnimator()
    {
        if (!playerAnimator) return;

        float animationSpeed = isDashing
            ? 1f
            : Mathf.Abs(moveInput.x);

        playerAnimator.SetFloat("Speed", animationSpeed);

        playerAnimator.SetBool("IsGrounded", isGrounded);
    }

    #endregion

    #region Audio

    void HandleFootsteps()
    {
        if (!playerAnimator || !footstepSource || !footstepClip)
            return;

        bool walking =
            playerAnimator.GetFloat("Speed") > 0.1f &&
            isGrounded &&
            !isDashing;

        if (walking && !footstepSource.isPlaying)
        {
            footstepSource.clip = footstepClip;
            footstepSource.loop = true;
            footstepSource.Play();
        }
        else if (!walking && footstepSource.isPlaying)
        {
            footstepSource.Stop();
        }
    }

    #endregion

    #region Input

    public void OnMove(InputAction.CallbackContext ctx)
    {
        if (isDead || !inputEnabled) return;

        moveInput = ctx.ReadValue<Vector2>();

        if (moveInput.x > 0.01f)
        {
            facingDirection = 1;
        }
        else if (moveInput.x < -0.01f)
        {
            facingDirection = -1;
        }
    }

    public void SetInputEnabled(bool value)
    {
        inputEnabled = value;

        if (!value)
        {
            moveInput = Vector2.zero;
            jumpHeld = false;
            jumpBufferCounter = 0f;

            isDashing = false;
            dashTimer = 0f;

            if (rb != null)
            {
                rb.linearVelocity = new Vector3(
                    0f,
                    rb.linearVelocity.y,
                    0f
                );
            }
        }
    }

    #endregion

    #region Partículas

    void HandleRunParticles()
    {
        if (!runParticles) return;

        float horizontalVelocity = Mathf.Abs(rb.linearVelocity.x);

        bool shouldPlay =
            isGrounded &&
            horizontalVelocity > minMoveVelocity &&
            !isDead &&
            !isDashing;

        if (shouldPlay && !runParticles.isPlaying)
        {
            runParticles.Play();
        }
        else if (!shouldPlay && runParticles.isPlaying)
        {
            runParticles.Stop();
        }
    }

    #endregion

    #region Muerte

    public bool IsDead() => isDead;

    public void Die(string cause = "")
    {
        if (isDead) return;

        isDead = true;

        isDashing = false;

        if (footstepSource && footstepSource.isPlaying)
            footstepSource.Stop();

        if (deathClip)
        {
            AudioSource.PlayClipAtPoint(
                deathClip,
                transform.position
            );
        }

        MinecartController cart =
            GetComponentInParent<MinecartController>();

        if (cart != null)
            Destroy(cart.gameObject);

        transform.SetParent(null);

        PlayerManager.Instance.OnPlayerDeath();

        Destroy(gameObject);
    }

    #endregion

    #region PowerUp Visual

    public void ApplyPowerUpVisuals()
    {
        if (PlayerInventory.Instance == null ||
            !PlayerInventory.Instance.hasElectricPower)
            return;

        Transform anchor = FindChildRecursive(
            transform,
            powerAnchorName
        );

        if (anchor == null ||
            electricPowerVisualPrefab == null ||
            anchor.childCount > 0)
            return;

        GameObject visual = Instantiate(
            electricPowerVisualPrefab,
            anchor.position,
            anchor.rotation,
            anchor
        );

        visual.name = electricPowerVisualPrefab.name;
    }

    private Transform FindChildRecursive(
        Transform parent,
        string name
    )
    {
        if (parent.name == name)
            return parent;

        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, name);

            if (result != null)
                return result;
        }

        return null;
    }

    #endregion

    #region Respawn VFX

    public void PlayRespawnVFX()
    {
        if (respawnVfxPrefab == null) return;

        GameObject vfx = Instantiate(
            respawnVfxPrefab,
            transform.position,
            Quaternion.identity
        );

        Destroy(vfx, 5f);
    }

    #endregion
}


