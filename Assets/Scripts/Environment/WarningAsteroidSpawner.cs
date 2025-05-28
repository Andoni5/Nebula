using UnityEngine;

public class WarningAsteroidSpawner : MonoBehaviour
{
    // === SERIALIZED FIELDS ===
    [Header("References")]
    [SerializeField] Transform  player;
    [SerializeField] GameObject warningAsteroidPrefab;

    [Header("Vertical Limits")]
    [SerializeField] float floorY   = -1.35f;
    [SerializeField] float ceilingY =  1.35f;

    [Header("Timing")]
    [SerializeField] Vector2Int timeRange = new Vector2Int(3, 6);

    // === PRIVATE FIELDS ===
    float timer, nextThreshold;
    MouseController mc;

    // === UNITY LIFECYCLE METHODS ===
    // Sets up references and chooses the first spawn threshold
    void Start()
    {
        if (!player) player = GameObject.FindWithTag("Player").transform;
        mc = player.GetComponent<MouseController>();
        SetNextThreshold();
    }

    // Monitors player position and spawns a warning when appropriate
    void Update()
    {
        if (mc != null && mc.IsDead)
        {
            timer = 0f;
            return;
        }

        bool onFloor   = player.position.y <= floorY;
        bool onCeiling = player.position.y >= ceilingY;

        if (onFloor || onCeiling)
        {
            timer += Time.deltaTime;
            if (timer >= nextThreshold)
            {
                SpawnWarning();
                SetNextThreshold();
            }
        }
        else
        {
            timer = 0f;
        }
    }

    // === SPAWNER UTILITIES ===
    // Resets the timer and picks a new random interval
    void SetNextThreshold()
    {
        timer = 0f;
        nextThreshold = Random.Range(timeRange.x, timeRange.y);
    }

    // Instantiates the warning asteroid at the right edge, aligned with the player
    void SpawnWarning()
    {
        Vector3 edge = Camera.main.ViewportToWorldPoint(new Vector3(1f, 0f, 0f));
        Vector3 pos  = new Vector3(edge.x - 0.5f, player.position.y, 0f);
        Instantiate(warningAsteroidPrefab, pos, Quaternion.identity);
    }
}