using System.Collections;
using UnityEngine;
using UnityEngine.U2D.Animation;

[RequireComponent(typeof(SpriteResolver))]
public class FrameByFrameAnimator : MonoBehaviour
{
    // === SERIALIZED FIELDS ===
    [Header("Skins")]
    [SerializeField] string currentSkin = "default";

    [Header("Run Animation")]
    [SerializeField] int   runFrameCount = 2;
    [SerializeField] float runFrameRate  = 10f;
    [SerializeField] float dieDelay      = 0.5f;

    [Header("Sprite Labels")]
    [SerializeField] string labelFly  = "fly";
    [SerializeField] string labelFall = "fall";
    [SerializeField] string labelDie0 = "die_0";
    [SerializeField] string labelDie1 = "die_1";

    [Header("Ground Detection")]
    [SerializeField] Transform groundCheck;
    [SerializeField] float     groundRadius = 0.1f;
    [SerializeField] LayerMask groundLayer;

    // === PRIVATE FIELDS ===
    SpriteResolver resolver;
    int   currentRunFrame;
    float timer;
    float frameTime;

    bool isGrounded;
    bool lastGrounded;

    bool isDead;
    bool isFallDead;
    bool hasPlayedDie1;

    // === UNITY CALLBACKS ===
    void Awake()
    {
        // Initialize animator and set initial label
        resolver         = GetComponent<SpriteResolver>();
        frameTime        = 1f / runFrameRate;
        currentRunFrame  = 0;
        resolver.SetCategoryAndLabel(currentSkin, labelFly);
    }

    void Update()
    {
        // Update animation each frame
        UpdateGroundedStatus();

        if (isDead)
        {
            HandleDeathAnimation();
            return;
        }

        HandleGroundTransition();

        if (isGrounded)
            AnimateRun();
    }

    // === ANIMATION ===
    void AnimateRun()
    {
        // Advance run animation frames while grounded
        timer += Time.deltaTime;
        if (timer >= frameTime)
        {
            timer = 0f;
            currentRunFrame = (currentRunFrame + 1) % runFrameCount;
            resolver.SetCategoryAndLabel(currentSkin, $"run_{currentRunFrame}");
        }
    }

    IEnumerator PlayDie1AfterDelay()
    {
        // Wait and switch to second death frame
        if (hasPlayedDie1) yield break;

        yield return new WaitForSeconds(dieDelay);
        resolver.SetCategoryAndLabel(currentSkin, labelDie1);
        hasPlayedDie1 = true;
    }

    // === STATE CHECKS ===
    void UpdateGroundedStatus()
    {
        // Determine whether the character is touching the ground
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayer);
    }

    // === SKIN MANAGEMENT ===
    public void SetSkin(string newSkin)
    {
        // Change skin category safely
        currentSkin = newSkin;

        if (!EnsureResolver()) return;

        resolver.SetCategoryAndLabel(
            currentSkin,
            isGrounded ? $"run_{currentRunFrame}" : labelFly
        );
    }

    public bool IsValidCategory(string categoryName, string testLabel = "fly")
    {
        // Test if a sprite category exists in the library
        if (!EnsureResolver()) return false;

        var renderer = GetComponent<SpriteRenderer>();
        if (renderer == null) return false;

        renderer.sprite = null;
        resolver.SetCategoryAndLabel(categoryName, testLabel);
        return renderer.sprite != null;
    }

    // === DEATH HANDLING ===
    public void TriggerDeath()
    {
        // Start death animation sequence
        isDead        = true;
        isFallDead    = !isGrounded;
        hasPlayedDie1 = false;

        if (isFallDead)
        {
            resolver.SetCategoryAndLabel(currentSkin, labelFall);
        }
        else
        {
            resolver.SetCategoryAndLabel(currentSkin, labelDie0);
            StartCoroutine(PlayDie1AfterDelay());
        }
    }

    // === INTERNAL HELPERS ===
    void HandleDeathAnimation()
    {
        // Manage transition from fall to ground death frames
        if (!isFallDead) return;

        resolver.SetCategoryAndLabel(currentSkin, labelFall);

        if (!isGrounded) return;

        isFallDead = false;
        resolver.SetCategoryAndLabel(currentSkin, labelDie0);
        StartCoroutine(PlayDie1AfterDelay());
    }

    void HandleGroundTransition()
    {
        // Switch animation when landing or taking off
        if (isGrounded == lastGrounded) return;

        if (isGrounded)
        {
            currentRunFrame = 0;
            timer = 0f;
            resolver.SetCategoryAndLabel(currentSkin, $"run_{currentRunFrame}");
        }
        else
        {
            resolver.SetCategoryAndLabel(currentSkin, labelFly);
        }

        lastGrounded = isGrounded;
    }

    bool EnsureResolver()
    {
        // Guarantee resolver reference exists
        if (resolver == null)
            resolver = GetComponent<SpriteResolver>();

        return resolver != null;
    }
}