using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class FitToCamera : MonoBehaviour
{
    // === SERIALIZED FIELDS ===
    [SerializeField] Camera targetCamera;            // Uses Camera.main if null
    [SerializeField, Range(1f, 2f)] float scaleMultiplier = 1.2f;

    // === UNITY LIFECYCLE METHODS ===
    // Scales the object so it covers the entire orthographic viewport
    void Awake()
    {
        if (!targetCamera) targetCamera = Camera.main;

        float worldHeight = 2f * targetCamera.orthographicSize;
        float worldWidth  = worldHeight * targetCamera.aspect;

        Renderer rend = GetComponent<Renderer>();
        Vector3 meshSize;

        if (rend is SpriteRenderer sr && sr.sprite != null)
            meshSize = sr.sprite.bounds.size;
        else
            meshSize = GetComponent<MeshFilter>().sharedMesh.bounds.size;

        Vector3 newScale = transform.localScale;
        newScale.x = (worldWidth  / meshSize.x) * scaleMultiplier;
        newScale.y = (worldHeight / meshSize.y) * scaleMultiplier;
        transform.localScale = newScale;
    }
}