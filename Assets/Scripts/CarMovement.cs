﻿using UnityEngine;

[System.Serializable]
public class PlayerInputConfig
{
    public KeyCode leftKey;
    public KeyCode rightKey;
    public KeyCode jumpKey;
    public KeyCode boostKey;
    public bool isLeftPlayer = false;
}

[System.Serializable]
public class TestInputs
{
    public bool leftKeyPressed = false;
    public bool rightKeyPressed = false;
    public bool jumpKeyPressed = false;
    public bool boostKeyPressed = false;
}

[RequireComponent(typeof(Rigidbody2D))]
public class CarMovement : MonoBehaviour
{
    public PlayerInputConfig inputConfig;
    public TestInputs testInputs; // can be used to actionate the car movement in test scripts

    private float storedHorizontalVelocity = 0f;

    private float acceleration = 80f;
    private float decelerationGrounded = 8f;
    private float decelerationAirborne = 3f;
    private float maxSpeed = 16f;
    private float jumpingPower = 35f;

    private bool isFacingRight = true;
    private int flipsLeft = 0;
    private bool isFlipping = false;
    private bool canFlip = false;

    private float jumpTimer;
    private float jumpTimeWindow = 2.5f;
    private float flipTorquePower = 35f;
    private float airboneTorquePower = 3f;
    private float boostPower = 52f;
    private float boostDrainRate = 30f;
    private float defaultBoostAmount = 45f;
    public float remainingBoost = 33f;

    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;

    private Animator animator;
    private int selectedCar;
    private SpriteRenderer spriteRenderer;
    public Sprite[] carIdleSprites;

    // INPUT BUFFER
    private Vector2 latestInput = Vector2.zero;
    private bool jumpPressed = false;
    private bool boostHeld = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        rb.linearDamping = 0.5f;
        rb.angularDamping = 1.1f;

        animator = GetComponent<Animator>();
        selectedCar = PlayerPrefs.GetInt("SelectedCar", 0);
        animator.SetInteger("carIndex", selectedCar);
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (inputConfig.isLeftPlayer)
        {
            FlipFacing();
        }
    }

    private void Update()
    {
        float h = 0f;
        if (Input.GetKey(inputConfig.leftKey) || testInputs.leftKeyPressed) h -= 1f;
        if (Input.GetKey(inputConfig.rightKey) || testInputs.rightKeyPressed) h += 1f;

        bool jump = Input.GetKeyDown(inputConfig.jumpKey) || testInputs.jumpKeyPressed;
        bool boost = Input.GetKey(inputConfig.boostKey) || testInputs.boostKeyPressed;

        SetInput(h, jump, boost);

        //checking for animator parameters
        bool isBoosting = (Input.GetKey(inputConfig.boostKey) || testInputs.boostKeyPressed) && remainingBoost > 0f;
        bool isMoving = Input.GetKey(inputConfig.leftKey) || testInputs.leftKeyPressed || Input.GetKey(inputConfig.rightKey) || testInputs.rightKeyPressed || isBoosting;
        bool isGrounded = IsGrounded();
        animator.SetBool("isMoving", isMoving);
        animator.SetBool("isBoosting", isBoosting);
        animator.SetBool("isGrounded", isGrounded);

        if (!isMoving)
        {
            animator.enabled = false;
            spriteRenderer.sprite = carIdleSprites[selectedCar];
        }
        else
        {
            animator.enabled = true;
        }

    }

    void SetInput(float h, bool jump, bool boost)
    {
        latestInput = new Vector2(h, 0f);

        if (jump && IsGrounded())
        {
            jumpPressed = true;
            canFlip = true;
            jumpTimer = Time.time;
        }

        if (jump && canFlip)
        {
            canFlip = false;
            flipsLeft = 1;
        }

        if (Time.time - jumpTimer > jumpTimeWindow)
        {
            flipsLeft = 0;
        }

        if (flipsLeft > 0 && jump && !IsGrounded())
        {
            flipsLeft--;
            FlipCar();
        }

        boostHeld = boost;

        // Airborne angular control
        if (!IsGrounded() && !isFlipping)
        {
            float angularVelocity = airboneTorquePower * 100f;
            if (h < 0)
            {
                rb.angularVelocity = angularVelocity;
            }
            else if (h > 0)
            {
                rb.angularVelocity = -angularVelocity;
            }
            else
            {
                rb.angularVelocity = 0f;
            }
        }
    }

    private void FixedUpdate()
    {
        storedHorizontalVelocity = rb.linearVelocity.x;

        if (IsGrounded())
        {
            if (latestInput.x != 0 && Mathf.Abs(storedHorizontalVelocity) < maxSpeed)
            {
                storedHorizontalVelocity += latestInput.x * acceleration * Time.fixedDeltaTime;
            }
            else
            {
                storedHorizontalVelocity = Mathf.MoveTowards(storedHorizontalVelocity, 0f, decelerationGrounded * Time.fixedDeltaTime);
            }
        }
        else
        {
            storedHorizontalVelocity = Mathf.MoveTowards(storedHorizontalVelocity, 0f, decelerationAirborne * Time.fixedDeltaTime);
        }

        rb.linearVelocity = new Vector2(storedHorizontalVelocity, rb.linearVelocity.y);

        if (jumpPressed)
        {
            rb.AddForce(Vector2.up * jumpingPower, ForceMode2D.Impulse);
            jumpPressed = false; // consume jump
        }

        if (boostHeld)
        {
            ApplyBoost();
        }
    }

    private void ApplyBoost()
    {
        if (isFlipping || remainingBoost <= 0f) return;

        Vector2 boostForce = -transform.right * boostPower;
        boostForce = isFacingRight ? boostForce : -boostForce;
        boostForce *= IsGrounded() ? 1.5f : 1f;

        rb.AddForce(boostForce, ForceMode2D.Force);

        // drain boost
        remainingBoost -= boostDrainRate * Time.fixedDeltaTime;
        if (remainingBoost <= 0f)
        {
            remainingBoost = 0f;
        }
    }

    private void FlipCar()
    {
        isFlipping = true;

        float torqueDirection = 0f;
        if (latestInput.x < 0)
        {
            torqueDirection = 1f;
        }
        else if (latestInput.x > 0)
        {
            torqueDirection = -1f;
        }

        rb.AddForce(Vector2.down * jumpingPower * 0.25f, ForceMode2D.Impulse);

        float angleOffset = 5f;
        float totalRotation = rb.rotation + torqueDirection * angleOffset;
        Vector2 forceDir = Quaternion.Euler(0, 0, totalRotation) * new Vector2(-torqueDirection * 25f, 0f);
        rb.AddForce(forceDir, ForceMode2D.Impulse);

        rb.angularVelocity = 0f;
        rb.AddTorque(torqueDirection * flipTorquePower, ForceMode2D.Impulse);

        if (torqueDirection == 0f)
        {
            rb.AddForce(transform.up * (jumpingPower * 0.4f), ForceMode2D.Impulse);
            isFlipping = false;
        }

        Invoke(nameof(ResetFlippingStatus), 0.5f);
    }

    private void ResetFlippingStatus()
    {
        isFlipping = false;
    }

    public void ResetBoost()
    {
        remainingBoost = defaultBoostAmount;
    }

    private bool IsGrounded()
    {
        return Physics2D.OverlapCircle(groundCheck.position, 0.9f, groundLayer);
    }

    private void FlipFacing()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
}
