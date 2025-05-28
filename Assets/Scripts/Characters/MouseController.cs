﻿using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

[RequireComponent(typeof(Rigidbody2D))]
public class MouseController : MonoBehaviour
{
    // === SERIALIZED FIELDS ===

    [Header("Movimiento")]
    [SerializeField] float jetpackForce = 75f;
    [SerializeField] float forwardSpeed = 3f;
    public float ForwardSpeed => forwardSpeed;
    [SerializeField] float maxUpwardSpeed = 5f;

    [Header("Velocidad progresiva")]
    [SerializeField] bool useProgressiveSpeed = true;
    [SerializeField, Range(1f, 3f)] float maxSpeedMultiplier = 3f;

    [Header("Ground Check (auto)")]
    [SerializeField] Transform groundCheckTransform;
    [SerializeField] float groundRadius = 0.1f;
    [SerializeField] LayerMask groundLayer;

    [Header("FX (auto)")]
    [SerializeField] ParticleSystem jetpack;
    [SerializeField] AudioSource jetpackAudio;
    [SerializeField] AudioSource footstepsAudio;

    [Header("UI (auto)")]
    [SerializeField] string coinsPath  = "Canvas/Image/coinsCollected";
    [SerializeField] string metersPath = "Canvas/Image/meters";
    [SerializeField] Button restartButton;

    [Header("Sonido")]
    [SerializeField] AudioClip coinClip;

    // === SKIN SETTINGS ===

    [Header("Animador por código")]
    [SerializeField] FrameByFrameAnimator frameAnimator;

    [Header("Skin inicial de reserva (si no existe SkinManager)")]
    [SerializeField] string initialSkinCategory = "default";

    // === RESULT SCREEN UI ===

    [Header("Pantalla de resultado")]
    [SerializeField] GameObject resultScreen;
    [SerializeField] Text distanceText;
    [SerializeField] Text coinText;
    [SerializeField] Button retryButton;
    [SerializeField] Button menuButton;

    // === PAUSE UI ===

    [Header("Pause UI")]
    [SerializeField] Button     pauseButton;
    [SerializeField] GameObject uiPauseScreen;
    [SerializeField] Button     continueButton;

    // === PRIVATE FIELDS ===

    Rigidbody2D rb;
    Text coinsLabel, metersLabel;
    bool  isGrounded, isDead;
    public bool IsDead => isDead;
    uint  coins;
    int   startX;
    bool  resultScreenShown;
    bool  isPaused;

    // === UNITY LIFECYCLE METHODS ===

    // Sets up component references, UI elements, and the initial skin.
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        AutoAssignReferences();

        string category = SkinManager.I ? SkinManager.I.SelectedSkin : initialSkinCategory;
        frameAnimator?.SetSkin(ValidateSkin(category));

        startX = Mathf.FloorToInt(transform.position.x);
        FormatCoins();
        UpdateMeters();

        if (restartButton) restartButton.gameObject.SetActive(false);

        if (pauseButton)    pauseButton.onClick.AddListener(PauseGame);
        if (continueButton) continueButton.onClick.AddListener(ResumeGame);

        if (uiPauseScreen) uiPauseScreen.SetActive(false);
    }

    // Handles movement, input, meters update, and death logic each physics tick.
    void FixedUpdate()
    {
        if (isPaused) return;                                 // Stop all logic while paused

        bool jetActive = Input.GetButton("Fire1") && !isDead;

        if (jetActive && rb.linearVelocity.y < maxUpwardSpeed)
            rb.AddForce(Vector2.up * jetpackForce);

        if (!isDead)
            rb.linearVelocity = new Vector2(forwardSpeed * SpeedMultiplier(),
                                            rb.linearVelocity.y);

        UpdateGroundedStatus();
        AdjustJetpack(jetActive);
        AdjustAudio(jetActive);
        UpdateMeters();

        if (isDead && isGrounded && !resultScreenShown)
        {
            ShowResultScreen();
            resultScreenShown = true;
        }
    }

    // Delegates collision events to coin collection or hazard handling.
    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Coins")) CollectCoin(col);
        else                         HitByLaser(col);
    }

    // === INITIALIZATION HELPERS ===

    // Verifies that a skin category exists in the animator or falls back to “default”.
    string ValidateSkin(string requested) =>
        frameAnimator && frameAnimator.IsValidCategory(requested) ? requested : "default";

    // Finds and assigns references that may not have been set in the Inspector.
    void AutoAssignReferences()
    {
        if (!groundCheckTransform)
            groundCheckTransform = transform.Find("groundCheck");

        if (!jetpack)
            jetpack = GetComponentInChildren<ParticleSystem>();

        if (groundLayer == 0)
        {
            int idx = LayerMask.NameToLayer("Ground");
            if (idx != -1) groundLayer = 1 << idx;
        }

        if (!coinsLabel && GameObject.Find(coinsPath))
            coinsLabel = GameObject.Find(coinsPath).GetComponent<Text>();

        if (!metersLabel && GameObject.Find(metersPath))
            metersLabel = GameObject.Find(metersPath).GetComponent<Text>();

        if (!frameAnimator)
            frameAnimator = GetComponent<FrameByFrameAnimator>();
    }

    // === MOVEMENT & PHYSICS METHODS ===

    // Updates the grounded flag using an overlap circle test.
    void UpdateGroundedStatus()
    {
        isGrounded = groundCheckTransform &&
                     Physics2D.OverlapCircle(groundCheckTransform.position,
                                             groundRadius, groundLayer);
    }

    // Adjusts jetpack particle emission based on player state.
    void AdjustJetpack(bool active)
    {
        if (!jetpack) return;

        var em = jetpack.emission;
        em.enabled = !isGrounded;
#if UNITY_6_0_OR_NEWER
        var rate = em.rateOverTime;
        rate.constant = active ? 300f : 75f;
        em.rateOverTime = rate;
#else
        em.rateOverTime = active ? 300f : 75f;
#endif
    }

    // Toggles footstep and jetpack audio depending on movement and state.
    void AdjustAudio(bool jetActive)
    {
        if (footstepsAudio) footstepsAudio.enabled = !isDead && isGrounded;

        if (jetpackAudio)
        {
            jetpackAudio.enabled = !isDead && !isGrounded;
            jetpackAudio.volume  = jetActive ? 1f : 0.5f;
        }
    }

    // Calculates progressive forward-speed multiplier based on distance travelled.
    float SpeedMultiplier()
    {
        if (!useProgressiveSpeed) return 1f;

        int metres = CurrentMeters();                       // Uses existing meter counter
        if (metres < 100) return 1f;

        // After the first 100 m, every additional 100 m adds +0.2 to the 1.3 base
        int stage = (metres - 100) / 100;                   // 0 → 100 m, 1 → 200 m, etc.
        float mult = 1.3f + stage * 0.2f;
        return Mathf.Clamp(mult, 1f, maxSpeedMultiplier);
    }

    // === UI & COUNTER METHODS ===

    // Displays the current coin count padded to three digits.
    void FormatCoins()
    {
        if (coinsLabel)
            coinsLabel.text = coins.ToString("D3");
    }

    // Updates the on-screen distance counter.
    void UpdateMeters()
    {
        if (!metersLabel) return;
        float raw = (transform.position.x - startX) * 1.5f;   // 50 % faster than x-position
        int metres = Mathf.Max(0, Mathf.FloorToInt(raw));
        metersLabel.text = $"M {metres:D3}";
    }

    // Retrieves the current distance in meters for gameplay calculations.
    int CurrentMeters() =>
        Mathf.Max(0, Mathf.FloorToInt((transform.position.x - startX) * 1.5f));

    // === GAMEPLAY EVENT METHODS ===

    // Collects a coin, updates the UI, and plays a sound effect.
    void CollectCoin(Collider2D coin)
    {
        coins++;
        FormatCoins();
        Destroy(coin.gameObject);
        if (coinClip) AudioSource.PlayClipAtPoint(coinClip, transform.position);
    }

    // Handles laser collision, triggering death and death animation.
    void HitByLaser(Collider2D laser)
    {
        if (!isDead)
            laser.GetComponent<AudioSource>()?.Play();

        isDead = true;
        frameAnimator?.TriggerDeath();
    }

    // Reloads the current level for a fresh run.
    public void RestartGame() =>
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);

    // === RESULT SCREEN METHODS ===

    // Shows the result panel and begins saving session statistics.
    void ShowResultScreen()
    {
        if (!resultScreen) return;

        resultScreen.SetActive(true);

        int metres = CurrentMeters();
        if (distanceText) distanceText.text = $"{metres}M";
        if (coinText)     coinText.text = $"MONEDAS: {coins}";

        retryButton?.onClick.AddListener(RestartGame);
        menuButton?.onClick.AddListener(ReturnToMenu);

        // Save statistics asynchronously
        StartCoroutine(SaveSessionStats(metres, (int)coins));
    }

    // Returns to the main menu scene.
    void ReturnToMenu() => SceneManager.LoadScene("menutest1");

    // === CHALLENGE & STATISTICS METHODS ===

    // Loads cached daily challenges and invokes a callback with the list.
    IEnumerator LoadCachedChallenges(Action<IReadOnlyList<DailyChallengeDTO>> cb)
	{
		var path = Path.Combine(Application.persistentDataPath,
								"offline_db", "daily_challenges.json");

		if (!File.Exists(path) || new FileInfo(path).Length == 0)
		{
			// Plantilla guardada como Resources/daily_challenges
			var text = Resources.Load<TextAsset>("daily_challenges");
			if (text != null)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, text.text);
				Debug.Log("[Cache-Challenges] template copied");
			}
		}

		var tbl = new JsonTable<DailyChallengeDTO>(path);
		IReadOnlyList<DailyChallengeDTO> list = null;
		yield return tbl.Load(() => list = tbl.All,
							  _  => Debug.LogWarning("[Cache-Challenges] load-fail"));

		cb?.Invoke(list ?? new List<DailyChallengeDTO>());
	}


    // Adds a completed challenge ID to local storage and cleans caches.
    IEnumerator AppendCompletedLocally(int id)
    {
        // 1) Save to completed cache
        var compPath  = Path.Combine(Application.persistentDataPath,
                                     "offline_db",
                                     $"{AuthManager.I.UserId}-completed_challenges.json");
        var compTable = new JsonTable<int>(compPath);

        yield return compTable.Load(() => { }, _ => { });
        if (!compTable.All.Contains(id))
            compTable.Add(id);

        yield return compTable.Save(
            () => Debug.Log($"[Cache-Completed] +{id}"),
            e  => Debug.LogWarning("[Cache-Completed] " + e)
        );

        // 2) Remove from pending daily cache
        var dailyPath = Path.Combine(Application.persistentDataPath,
                                     "offline_db",
                                     "daily_challenges.json");
        if (File.Exists(dailyPath))
        {
            try
            {
                var list = JsonConvert
                           .DeserializeObject<List<DailyChallengeDTO>>(File.ReadAllText(dailyPath));
                if (list.RemoveAll(d => d.id == id) > 0)
                    File.WriteAllText(dailyPath,
                        JsonConvert.SerializeObject(list, Formatting.Indented));
                Debug.Log($"[Cache-Completed] Removed mission {id} from daily_challenges.json");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Cache-Completed] Error cleaning daily cache: " + e.Message);
            }
        }
    }

    // Saves session statistics, calculates mission rewards, and syncs with backend.
    IEnumerator SaveSessionStats(int sessionDistance, int sessionCoins)
    {
		yield return new WaitForFixedUpdate();

		// Si durante ese frame se avanzó algo más, usa el mayor de los dos
		sessionDistance = Mathf.Max(sessionDistance, CurrentMeters());
		
        int challengeReward = 0;                                  // Coins from completed missions

        // Wait for AuthManager to be ready
        yield return new WaitUntil(() => AuthManager.I != null);

        string uid  = AuthManager.I.UserId;
        string url  = AuthManager.I.Url;
        string anon = AuthManager.I.AnonKey;

        var repo = new PlayerStatsRepo(uid, new PlayerStatsDAO(url, anon));

        // STEP 1: Load existing stats
        PlayerStatsDTO stats = null;
        bool loaded = false;
        bool online = Application.internetReachability != NetworkReachability.NotReachable;

        if (online)
        {
            yield return repo.Sync(dto => { stats = dto; loaded = true; },
                                   err => { Debug.LogWarning($"[Stats] Sync fail: {err}"); loaded = true; });
        }
        if (!online || stats == null)
        {
            yield return repo.Get(AuthManager.I.AccessToken,
                                  dto => { stats = dto; loaded = true; },
                                  err => { Debug.LogWarning($"[Stats] Local get fail: {err}"); loaded = true; });
        }
        yield return new WaitUntil(() => loaded);

        /*────────── MISSIONS ──────────*/
        var dailyDao     = new DailyChallengesDAO(AuthManager.I.Url, AuthManager.I.AnonKey);
        var completedDao = new CompletedChallengesDAO(
            AuthManager.I.Url,
            AuthManager.I.AnonKey,
            AuthManager.I.AccessToken
        );

        // 1️⃣ Active missions
        IReadOnlyList<DailyChallengeDTO> active = null;
        yield return dailyDao.GetActiveChallenges(
            list => {
                active = list;
                Debug.Log($"[Mission] active = {active?.Count ?? 0}");

                // Cache missions locally for offline use
                try
                {
                    var cachePath = Path.Combine(Application.persistentDataPath,
                                                "offline_db",
                                                "daily_challenges.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                    File.WriteAllText(cachePath, JsonConvert.SerializeObject(active, Formatting.Indented));
                    Debug.Log("[Cache-Challenges] Saved active missions to cache");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Cache-Challenges] save-fail " + ex.Message);
                }
            },
            e => {
                Debug.LogWarning("[Mission] fail " + e);
                active = new List<DailyChallengeDTO>();
            });

        // Offline fallback if there are no active missions
        if (active == null || active.Count == 0)
        {
            Debug.Log("[Mission] Falling back to cached challenges …");
            yield return LoadCachedChallenges(list => active = list);
        }

        // 2️⃣ Completed missions
        HashSet<int> done = null;
        yield return completedDao.GetCompleted(
            ids => {
                done = ids;
                var doneStr = (done != null && done.Count > 0) ? string.Join(",", done) : "";
                Debug.Log($"[Mission] done => {doneStr}");
            },
            e => {
                Debug.LogWarning("[Mission] done-fail " + e);
                done = null;
            });

        // Offline fallback for completed missions
        if (done == null)
        {
            Debug.Log("[Mission] Falling back to cached completed challenges …");
            var compPath = Path.Combine(Application.persistentDataPath,
                                        "offline_db",
                                        $"{uid}-completed_challenges.json");
            var compTbl = new JsonTable<int>(compPath);

            yield return compTbl.Load(
                () => { },       // ok
                _  => { }        // err
            );
            done = new HashSet<int>(compTbl.All);
        }

        Debug.Log($"[Mission] Evaluating {active.Count} missions; previous completions = {done.Count}");

        // 3️⃣ Evaluate challenges achieved this run
        foreach (var ch in active.Where(c => !done.Contains(c.id)))
        {
            bool ok = (ch.challenge_type == "WALK"  && sessionDistance >= ch.amount_needed) ||
                      (ch.challenge_type == "COINS" && sessionCoins    >= ch.amount_needed);
            if (!ok) continue;

            // Reward
            challengeReward += ch.reward_coins;
            Debug.Log($"[Mission] Completed! id={ch.id} +{ch.reward_coins}🪙");

            // Insert into completed_challenges (online)
            bool inserted = false;
            yield return completedDao.InsertCompleted(
                ch.id,
                uid,
                () => {
                    inserted = true;
                    CompletedChallengesDAO.AppendToLocalCache(uid, ch.id);
                    NebulaMenuUI.Instance?.RemoveMissionFromUI(ch.id);
                },
                e => { Debug.LogWarning("[Mission] insert fail " + e); inserted = false; }
            );

            // Always save offline if insertion failed (no network or backend error)
            if (!inserted)
            {
                Debug.Log($"[Mission] Saving completed {ch.id} locally");
                yield return AppendCompletedLocally(ch.id);
            }
        }

        // STEP 2: Create default stats DTO if none existed
        if (stats == null)
        {
            stats = new PlayerStatsDTO
            {
                user_id               = uid,
                best_distance         = sessionDistance,
                best_coins_earned     = sessionCoins + challengeReward,
                total_sessions        = 1,
                total_distance        = sessionDistance,
                total_coins_collected = sessionCoins + challengeReward,
                total_coins_spent     = 0,
                challenges_completed  = (challengeReward > 0 ? 1 : 0),
                actual_skin           = SkinManager.I ? SkinManager.I.SelectedSkin : "default",
                updated_at            = DateTime.UtcNow
            };
        }
        else
        {
            // STEP 3: Aggregate updated statistics
            stats.total_sessions         += 1;
            stats.total_distance         += sessionDistance;
            stats.total_coins_collected  += sessionCoins + challengeReward;

            if (sessionDistance > stats.best_distance)
                stats.best_distance = sessionDistance;

            if (sessionCoins + challengeReward > stats.best_coins_earned)
                stats.best_coins_earned = sessionCoins + challengeReward;

            if (challengeReward > 0)
                stats.challenges_completed += 1;

            stats.updated_at = DateTime.UtcNow;
        }

        // Add mission rewards to local coin count and update UI
        coins += (uint)challengeReward;
        if (coinText)
            coinText.text = $"MONEDAS: {coins}";

        // STEP 4: Save statistics
        yield return repo.Save(
            stats,
            () => Debug.Log("✅ Stats saved."),
            err => Debug.LogWarning($"❌ Failed to save stats: {err}")
        );
    }

    // === PAUSE METHODS ===

    // Pauses gameplay, audio, and UI interactions.
    void PauseGame()
    {
        if (isPaused) return;
        isPaused = true;

        Time.timeScale      = 0f;
        AudioListener.pause = true;                   // Pauses music and SFX

        if (uiPauseScreen) uiPauseScreen.SetActive(true);
        if (pauseButton)   pauseButton.interactable = false;
    }

    // Resumes gameplay, audio, and UI interactions.
    void ResumeGame()
    {
        if (!isPaused) return;
        isPaused = false;

        Time.timeScale      = 1f;
        AudioListener.pause = false;                  // Resumes music and SFX

        if (uiPauseScreen) uiPauseScreen.SetActive(false);
        if (pauseButton)   pauseButton.interactable = true;
    }
}