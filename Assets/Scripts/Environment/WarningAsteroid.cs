using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class WarningAsteroid : MonoBehaviour
{
    // === SERIALIZED FIELDS ===
    [SerializeField] GameObject caution;
    [SerializeField] GameObject asterBad;

    [Header("Timings")]
    [SerializeField] float warningDuration = 1.5f;
    [SerializeField] float speedMultiplier = 2f;

    // === PRIVATE FIELDS ===
    Rigidbody2D rb;
    Collider2D  col2d;
    Camera      cam;
    float       halfSpriteW;
    bool        warningPhase = true;
    float       moveSpeed;

    // === UNITY LIFECYCLE METHODS ===
    // Initializes references, hides the asteroid, and schedules its spawn
    void Awake()
    {
        cam = Camera.main;
        rb  = GetComponent<Rigidbody2D>();

        col2d = GetComponent<Collider2D>() ?? gameObject.AddComponent<BoxCollider2D>();
        col2d.isTrigger = true;
        col2d.enabled   = false;

        caution.SetActive(true);
        asterBad.SetActive(false);

        var mc = GameObject.FindWithTag("Player").GetComponent<MouseController>();
        moveSpeed = mc.ForwardSpeed * speedMultiplier;

        halfSpriteW = caution.GetComponent<SpriteRenderer>().bounds.size.x * 0.5f;

        StartCoroutine(SwapToAsteroid());
    }

    // Keeps the warning icon aligned with the screen edge
    void LateUpdate()
    {
        if (!warningPhase) return;

        Vector3 edge = cam.ViewportToWorldPoint(new Vector3(1f, 0f, 0f));
        transform.position = new Vector3(edge.x - halfSpriteW, transform.position.y, 0f);
    }

    // === TRANSITION ROUTINE ===
    // Waits, then swaps the caution icon for a moving asteroid
    IEnumerator SwapToAsteroid()
    {
        yield return new WaitForSeconds(warningDuration);

        warningPhase = false;
        caution.SetActive(false);
        asterBad.SetActive(true);

        col2d.enabled = true;

        rb.gravityScale   = 0f;
        rb.linearVelocity = Vector2.left * moveSpeed;
    }

    // === INVISIBILITY CALLBACK ===
    // Destroys the asteroid once it exits the viewport
    void OnBecameInvisible() => Destroy(gameObject);
}