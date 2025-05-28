using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    // === SERIALIZED FIELDS ===
    [SerializeField] GameObject targetObject;
    [SerializeField] float      smoothTime = 0.15f;

    // === PRIVATE FIELDS ===
    float distanceToTarget;
    Vector3 velocity;

    // === INITIALIZATION METHODS ===
    // Caches the target object if not set in the inspector
    void Awake()
    {
        if (!targetObject)
            targetObject = GameObject.FindWithTag("Player");
    }

    // Stores the initial horizontal offset to the target
    void Start()
    {
        if (targetObject)
            distanceToTarget = transform.position.x - targetObject.transform.position.x;
    }

    // === UPDATE METHODS ===
    // Smoothly follows the target each frame in LateUpdate
    void LateUpdate()
    {
        if (!targetObject) return;

        Vector3 desired = transform.position;
        desired.x = targetObject.transform.position.x + distanceToTarget;
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
    }
}