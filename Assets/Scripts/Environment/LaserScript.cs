using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(Collider2D))]
public class LaserScript : MonoBehaviour
{
    // === SERIALIZED FIELDS ===
    [SerializeField] Sprite laserOnSprite;
    [SerializeField] Sprite laserOffSprite;
    [SerializeField] float  toggleInterval = 0.5f;
    [SerializeField] float  rotationSpeed  = 0f;

    // === PRIVATE FIELDS ===
    SpriteRenderer sr;
    Collider2D     col;
    bool           laserIsOn = true;
    Coroutine      routine;

    // === UNITY LIFECYCLE METHODS ===
    // Caches component references
    void Awake()
    {
        sr  = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
    }

    // Starts and stops the toggle coroutine
    void OnEnable()  => routine = StartCoroutine(ToggleRoutine());
    void OnDisable() { if (routine != null) StopCoroutine(routine); }

    // Rotates the laser when rotation is enabled
    void Update()
    {
        if (rotationSpeed != 0f)
            transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
    }

    // === TOGGLE ROUTINE ===
    // Alternates laser state at a fixed interval
    IEnumerator ToggleRoutine()
    {
        while (true)
        {
            SetState(laserIsOn);
            laserIsOn = !laserIsOn;
            yield return new WaitForSeconds(toggleInterval);
        }
    }

    // Enables/disables the collider and swaps the sprite
    void SetState(bool on)
    {
        col.enabled = on;
        sr.sprite   = on ? laserOnSprite : laserOffSprite;
    }
}