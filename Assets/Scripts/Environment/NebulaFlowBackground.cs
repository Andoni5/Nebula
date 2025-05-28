using System.Collections;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class NebulaFlowBackground : MonoBehaviour
{
    // === SERIALIZED FIELDS ===
    [Header("Camera & Quad")]
    [SerializeField] Camera cam;
    [SerializeField, Range(1f, 2f)] float oversize = 1.2f;

    [Header("Movement")]
    [SerializeField] float driftSpeed = 0.03f;
    [SerializeField] float parallaxX  = 0.4f;

    [Header("Color Cycle")]
    [SerializeField] float holdMin = 7f;
    [SerializeField] float holdMax = 18f;
    [SerializeField] float fadeDur = 3f;

    [Header("Brightness / Noise")]
    [SerializeField, Range(0f, 3f)] float brightness  = 0.5f;
    [SerializeField] float noiseZoom   = 8f;
    [SerializeField] float contrastPow = 2.2f;

    // === PRIVATE FIELDS ===
    Material mat;
    Vector2  uvOffset;

    Color currentA, currentB, targetA, targetB;
    float fadeTimer, fadeT;
    bool  fading;

    // === UNITY LIFECYCLE METHODS ===
    // Instantiates the procedural material and starts the color cycle
    void Awake()
    {
        if (!cam) cam = Camera.main;
        FitQuad();

        mat = new Material(Shader.Find("Unlit/NebulaFlowProceduralTileable"));
        mat.SetFloat("_Strength",  brightness);
        mat.SetFloat("_NoiseZoom", noiseZoom);
        mat.SetFloat("_Contrast",  contrastPow);
        GetComponent<MeshRenderer>().material = mat;

        currentA = RandomHSV();
        currentB = RandomHSV();
        mat.SetColor("_ColorA", currentA);
        mat.SetColor("_ColorB", currentB);

        StartCoroutine(ColorRoutine());
    }

    // Moves the quad with the camera and handles color fading
    void LateUpdate()
    {
        Vector3 cp = cam.transform.position;
        transform.position = new Vector3(cp.x, cp.y, transform.position.z);

        uvOffset.x = cp.x * parallaxX * 0.05f + Time.time * driftSpeed;
        mat.SetVector("_UVOffset", new Vector4(uvOffset.x, 0f, 0f, 0f));

        if (fading)
        {
            fadeTimer += Time.deltaTime;
            fadeT = Mathf.Clamp01(fadeTimer / fadeDur);

            mat.SetColor("_ColorA", Color.Lerp(currentA, targetA, fadeT));
            mat.SetColor("_ColorB", Color.Lerp(currentB, targetB, fadeT));

            if (fadeT >= 1f)
            {
                currentA = targetA;
                currentB = targetB;
                fading    = false;
            }
        }
    }

    // === COLOR ROUTINE ===
    // Holds current colors, then smoothly transitions to new random colors
    IEnumerator ColorRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(holdMin, holdMax));

            targetA   = RandomHSV();
            targetB   = RandomHSV();
            fadeTimer = 0f;
            fading    = true;

            yield return new WaitForSeconds(fadeDur);
        }
    }

    // === HELPER METHODS ===
    // Generates a bright random HSV color
    static Color RandomHSV()
    {
        float h = Random.value;
        float s = Random.Range(0.8f, 1f);
        float v = Random.Range(0.8f, 1f);
        return Color.HSVToRGB(h, s, v);
    }

    // Scales the quad so it fully covers (and slightly overflows) the viewport
    void FitQuad()
    {
        float worldH = 2f * cam.orthographicSize * oversize;
        float worldW = worldH * cam.aspect;
        Vector3 mesh = GetComponent<MeshFilter>().sharedMesh.bounds.size;
        transform.localScale = new Vector3(worldW / mesh.x, worldH / mesh.y, 1f);
    }
}