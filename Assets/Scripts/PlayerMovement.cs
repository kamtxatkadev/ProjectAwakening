using System;
using UnityEngine;

[RequireComponent(
    typeof(Rigidbody)
    )]
public class PlayerMovement : MonoBehaviour
{
    [Header("DebugMode")]
    [SerializeField] private bool printPlayerInfo = true;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8.3f;

    [Header("Fall with style")]
    [SerializeField] private float maxFallSpeed = -20.9f;
    [SerializeField] private float fallMultiplier = 4.5f;
    [SerializeField] private float jumpLowMultiplier = 10f;
    [SerializeField] private float jumpHighMultiplier = 3.75f;

    [Header("Jumping")]
    [SerializeField] private float jumpSpeed = 20f;

    [Header("Roll")]
    [SerializeField] private float rollSpeed = 20f;
    [SerializeField] private float rollDuration = 0.26f;
    [SerializeField] private float rollCooldown = 0.5f;

    [Header("Combat")]
    [SerializeField] private float attackCooldown = 0.4f;

    [Header("Enviroment Check")]
    [SerializeField] private float groundedBoxHeight = 0.1f;
    [SerializeField] private LayerMask groundMask = 0;


    // Flat Movement
    private Vector3 charDir;
    private Vector3 inputDir;
    private bool lockedDirection;

    // Jump
    private float gravityScale;
    private bool inputJump;
    private bool inputJumpHold;
    private bool wantJump;
    private bool canJump;
    private bool willJump;
    private bool doingJump;
    private bool onGround;
    private Vector3 groundedBoxSize;
    private Vector3 groundedBoxOffset;

    // Roll
    private bool inputRoll;
    private bool wantRoll;
    private bool canRoll;
    private bool willRoll;
    private bool doingRoll;
    private bool chargedRoll;
    private float rollDurationTimer;
    private float rollCooldownTimer;

    // Attack
    private bool inputAttack;
    private bool wantAttack;
    private bool canAttack;
    private bool willAttack;
    private bool doingAttack;
    private float attackCooldownTimer;

    // New Rb velocity
    private Vector3 newRbVelocity;

    // Components
    private Rigidbody rb;
    private BoxCollider col;
    // TODO:: manage animator in PlayerAnimation
    private Animator animator;

    // Debugging
    private GUIStyle customGUIStyle;


    private void Awake()
    {
        // Get components
        rb = GetComponent<Rigidbody>();
        col = GetComponent<BoxCollider>();
        animator = GetComponentInChildren<Animator>();

        // Start char dir : right
        inputDir = new Vector3(1.0f, 0f, 0f);
        TurnCharacter();

        // Colliders
        Vector3 colSize = col.bounds.size;
        groundedBoxSize = new Vector3(colSize.x - 0.05f, groundedBoxHeight, colSize.z - 0.05f);
        groundedBoxOffset = (Vector3.down * groundedBoxSize.y) * 0.5f;

        // GUI
        customGUIStyle = new GUIStyle();
        customGUIStyle.normal.textColor = Color.white;
        customGUIStyle.fontSize = 20;
    }

    private void Update()
    {
        // Get direction input
        int inputLeft = (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKey(KeyCode.LeftArrow)) ? 1 : 0;
        int inputRight = (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKey(KeyCode.RightArrow)) ? 1 : 0;
        int inputTop = (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKey(KeyCode.UpArrow)) ? 1 : 0;
        int inputDown = (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKey(KeyCode.DownArrow)) ? 1 : 0;
        int inputX = inputRight - inputLeft;
        int inputZ = inputTop - inputDown;

        inputDir.Set(inputX, 0, inputZ);
        inputDir.Normalize();

        // Get actions input
        inputJump = Input.GetKeyDown(KeyCode.Z);
        inputJumpHold = Input.GetKey(KeyCode.Z);
        inputRoll = Input.GetKeyDown(KeyCode.C);
        inputAttack = Input.GetKeyDown(KeyCode.X);
        if (Input.GetKeyDown(KeyCode.H)) GetHit();
        if (Input.GetKeyDown(KeyCode.K)) Die();

        // Use inputs
        if (!lockedDirection)
        {
            if ((inputDir != Vector3Int.zero) && inputDir != charDir)
                TurnCharacter();
        }

        if (inputJump)
            wantJump = true;

        if (inputRoll)
            wantRoll = true;

        if (inputAttack)
            wantAttack = true;

        // TODO:: do this in PlayerAnimation
        // Update animation
        animator.SetFloat("Speed", rb.velocity.sqrMagnitude);
        animator.SetBool("onGround", onGround);
    }

    private void TurnCharacter()
    {
        charDir = inputDir;

        animator.SetFloat("Horizontal", charDir.x);
        animator.SetFloat("Vertical", charDir.z);
    }

    private void FixedUpdate()
    {
        newRbVelocity = rb.velocity;

        UpdateEnvironmentChecking();
        UpdateOngoingActions();
        DecideFutureActions();

        // Clear want-to stated
        wantJump = false;
        wantRoll = false;
        wantAttack = false;

        // Do actions
        MoveFlat();
        if (willJump) Jump();
        if (willRoll) Roll();
        if (willAttack) Attack();

        // Fall with style
        AdaptPlayerGravity();
        LimitFallingSpeed();

        // Apply all changes
        rb.velocity = newRbVelocity;
    }

    private void UpdateEnvironmentChecking()
    {
        // On ground
        Vector3 groundedBoxCenter = transform.position + groundedBoxOffset;
        Collider[] cols = Physics.OverlapBox(groundedBoxCenter, groundedBoxSize * 0.5f, Quaternion.identity, groundMask);
        onGround = cols.Length != 0;
    }

    private void UpdateOngoingActions()
    {
        // Update jump
        if (doingJump && rb.velocity.y <= 0f && onGround)
            EndJump();

        // Update dash
        if (doingRoll)
        {
            rollDurationTimer -= Time.fixedDeltaTime;
            if (rollDurationTimer <= 0f)
                EndRoll();
        }

        if (!chargedRoll && onGround)
            chargedRoll = true;

        if (rollCooldownTimer > 0f)
            rollCooldownTimer -= Time.fixedDeltaTime;

        // Update combat
        // TODO delete on attack final version
        if (attackCooldownTimer > 0f)
            attackCooldownTimer -= Time.fixedDeltaTime;
    }

    private void DecideFutureActions()
    {
        // Can-do states
        canJump =
            onGround &&
            !doingJump && !doingRoll && !doingAttack;

        canRoll =
            chargedRoll && (rollCooldownTimer <= 0f) && onGround && IsCloseToZero(rb.velocity.y) &&
            !doingRoll && !doingAttack;

        canAttack =
            (attackCooldownTimer <= 0f) && onGround && IsCloseToZero(rb.velocity.y) &&
            !doingAttack && !doingRoll;

        // Will-do states
        willJump = wantJump && canJump;
        willRoll = wantRoll && canRoll;
        willAttack = wantAttack && canAttack;

        // Action preferences
        willJump = willJump && !willRoll && !willAttack;
        //willRoll = willRoll;
        willAttack = willAttack && !willRoll;

        // End actions that are cancelled by other actions
        // There is none
    }

    private bool IsCloseToZero(float value)
    {
        return (Mathf.Abs(value) < 0.0001f);
    }

    #region ACTIONS

    private void MoveFlat()
    {
        if (doingRoll || doingAttack)
            return;

        newRbVelocity.Set(inputDir.x * moveSpeed, newRbVelocity.y, inputDir.z * moveSpeed);
    }

    private void Jump()
    {
        Debug.Log("Jump");

        // End timers

        // New states/actions
        doingJump = true;

        // Apply Rb velocity changes
        newRbVelocity.y = jumpSpeed;
    }

    private void EndJump()
    {
        Debug.Log("EndJump");

        doingJump = false;
    }

    private void Roll()
    {
        Debug.Log("Roll");

        animator.SetTrigger("Roll");

        // End timers

        // Start timers
        rollDurationTimer = rollDuration;
        rollCooldownTimer = rollCooldown;

        // New states/actions
        doingRoll = true;
        lockedDirection = true;

        // Apply Rb velocity changes
        newRbVelocity.x = rollSpeed * charDir.x;
        newRbVelocity.y = 0f;
        newRbVelocity.z = rollSpeed * charDir.z;
        gravityScale = 0f;
    }

    private void EndRoll()
    {
        Debug.Log("EndRoll");

        doingRoll = false;
        lockedDirection = false;
    }

    private void Attack()
    {
        Debug.Log("Attack");

        // End timers

        // Start timers
        attackCooldownTimer = attackCooldown;

        // New states/actions
        doingAttack = true;
        lockedDirection = true;

        // Apply Rb velocity changes
        // TODO try to delete this and do it somewhere else
        newRbVelocity.x = 0f;

        // TODO:: delete this
        EndAttack();
    }

    public void EndAttack()
    {
        Debug.Log("EndAttack");

        doingAttack = false;
        lockedDirection = false;
    }

    private void GetHit()
    {
        Debug.Log("Get hit");

        CancelAllActions();
        gravityScale = 0f;
        rb.velocity = Vector2.zero;
        enabled = false;
    }

    private void RecoverFromHit()
    {
        Debug.Log("Recover from hit");

        enabled = true;
    }

    private void Die()
    {
        CancelAllActions();
        gravityScale = 0f;
        rb.velocity = Vector2.zero;
        enabled = false;
    }

    private void CancelAllActions()
    {
        if (doingJump) EndJump();
        if (doingRoll) EndRoll();
        if (doingAttack) EndAttack();
    }

    #endregion

    private void AdaptPlayerGravity()
    {
        if (doingRoll)
            return;

        if (newRbVelocity.y <= 0f)
            SetPlayerGravityScale(fallMultiplier);
        else
            SetPlayerGravityScale((inputJumpHold || inputJump) ? jumpHighMultiplier : jumpLowMultiplier);
    }

    private void SetPlayerGravityScale(float scale)
    {
        gravityScale = scale;
        rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
    }

    private void LimitFallingSpeed()
    {
        float limitFallSpeed = maxFallSpeed;

        float vSpeedChange = Physics2D.gravity.y * gravityScale * Time.fixedDeltaTime;
        float futureVSpeed = newRbVelocity.y + vSpeedChange;

        if (futureVSpeed < limitFallSpeed)
            newRbVelocity.y = limitFallSpeed - vSpeedChange;
    }

    private void OnDrawGizmos()
    {
        Vector3 groundedBoxCenter = transform.position + groundedBoxOffset;
        Gizmos.color = onGround ? Color.red : Color.green;
        Gizmos.DrawCube(groundedBoxCenter, groundedBoxSize);
    }

    private void OnGUI()
    {
        if (!printPlayerInfo) return;

        float roundedVelX = (float)Math.Round(rb.velocity.x, 2);
        float roundedVelY = (float)Math.Round(rb.velocity.y, 2);
        float roundedVelZ = (float)Math.Round(rb.velocity.z, 2);
        string velLabelText = "Velocity : (" + roundedVelX + ", " + roundedVelY + ", " + roundedVelZ + ")";
        GUI.Label(new Rect(20f, 20f, 500f, 20f), velLabelText, customGUIStyle);

        float roundePosX = (float)Math.Round(transform.position.x, 2);
        float roundePosY = (float)Math.Round(transform.position.y, 2);
        float roundePosZ = (float)Math.Round(transform.position.z, 2);
        string posLabelText = "Position : (" + roundePosX + ", " + roundePosY + ", " + roundePosZ + ")";
        GUI.Label(new Rect(20f, 60f, 500f, 20f), posLabelText, customGUIStyle);
    }
}

