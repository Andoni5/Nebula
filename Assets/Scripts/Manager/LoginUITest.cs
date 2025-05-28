using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;

public class LoginUITest : MonoBehaviour
{
    // === SERIALIZED FIELDS ===
    [Header("Wire desde builder")]
    [SerializeField] private InputField emailField;
    [SerializeField] private InputField passwordField;
    [SerializeField] private Text        feedbackLabel;
    [SerializeField] private Button      loginButton;
    [SerializeField] private Button      offlineButton;
    [SerializeField] private Button      registerButton;

    [Header("Loading Screen")]
    [SerializeField] private GameObject loadingScreen;   // Loading screen root
    [SerializeField] private Slider     loadingBar;      // Progress bar reference

    // === PRIVATE FIELDS ===
    private PlayerStatsDAO statsDAO;

    private const float AUTO_PROGRESS_LIMIT = 0.85f;
    private const float AUTO_PROGRESS_SPEED = 0.25f;
    private const float FINAL_BOOST_TIME    = 0.4f;

    private bool loginFinished = false;
    private bool loginFailed   = false;

    // === UNITY LIFECYCLE METHODS ===
    // Coroutine that initializes the UI, DAO, and attempts an automatic login
    private IEnumerator Start()
    {
        yield return new WaitUntil(() => AuthManager.I != null);

        // Warn if loading screen references are missing
        if (loadingScreen == null || loadingBar == null)
            Debug.LogWarning("[LoginUITest] ⚠️  Assign LoadingScreen and/or LoadingBar in the Inspector.");

        // Initialize loading screen if present
        if (loadingScreen != null && loadingBar != null)
        {
            loadingScreen.SetActive(true);
            loadingBar.gameObject.SetActive(true);
            loadingBar.value = 0f;
            StartCoroutine(AutoFillRoutine());
        }

        statsDAO = new PlayerStatsDAO(AuthManager.I.Url, AuthManager.I.AnonKey);
        Debug.Log("[LoginUITest] DAO initialized");

        TryAutoLogin();
        registerButton.onClick.AddListener(DoRegister);
    }

    // === LOADING SCREEN METHODS ===
    // Gradually auto-fills the loading bar until login finishing flags are set
    private IEnumerator AutoFillRoutine()
    {
        while (!loginFinished && !loginFailed)
        {
            if (loadingBar != null && loadingBar.value < AUTO_PROGRESS_LIMIT)
                loadingBar.value += Time.deltaTime * AUTO_PROGRESS_SPEED;

            yield return null;
        }
    }

    // Finishes filling the loading bar and loads the main menu scene
    private IEnumerator FinishLoadingAndLoadMenu()
    {
        loginFinished = true;   // stops AutoFillRoutine

        if (loadingBar != null)
        {
            float start = loadingBar.value;
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime / FINAL_BOOST_TIME;
                loadingBar.value = Mathf.Lerp(start, 1f, t);
                yield return null;
            }
            yield return new WaitForSeconds(0.15f);
        }

        SceneManager.LoadScene("menutest1");
    }

    // Disables the loading screen and progress bar
    private void DisableLoadingScreen()
    {
        loginFailed = true;
        if (loadingScreen != null)
            loadingScreen.SetActive(false);
        if (loadingBar != null)
            loadingBar.gameObject.SetActive(false);
    }

    // === LOGIN METHODS ===
    // Triggered by UI: validates input and performs login
    public void DoLogin()
    {
        string email = emailField.text.Trim();
        string pass  = passwordField.text.Trim();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass))
        {
            feedbackLabel.text = "❌ Email o contraseña vacíos";
            return;
        }

        PerformLogin(email, pass);
    }

    // Executes the login call through AuthManager
    private void PerformLogin(string email, string pass)
    {
        feedbackLabel.text = "⌛ Login...";
        loginButton.interactable = false;

        AuthManager.I.Login(email, pass,
            token =>
            {
                PlayerPrefs.SetString("saved_email", email);
                PlayerPrefs.SetString("saved_password", pass);
                PlayerPrefs.Save();

                feedbackLabel.text = "✔ Logeado. Cargando stats.";
                StartCoroutine(statsDAO.GetStats(token, OnStatsOk, OnErr));
            },
            OnErr);
    }

    // Attempts offline mode or credential-based automatic login
    private void TryAutoLogin()
    {
        string savedEmail    = PlayerPrefs.GetString("saved_email", "");
        string savedPassword = PlayerPrefs.GetString("saved_password", "");
        bool   online        = Application.internetReachability != NetworkReachability.NotReachable;

        if (!online)
        {
            Debug.Log("📴 Offline. Entering offline mode.");
            DisableLoadingScreen();
            OfflineLoginHandler.Instance.EnterOfflineMode();
        }
        else if (!string.IsNullOrEmpty(savedEmail) && !string.IsNullOrEmpty(savedPassword))
        {
            Debug.Log("🌐 Attempting auto-login...");
            PerformLogin(savedEmail, savedPassword);
        }
        else
        {
            Debug.Log("[LoginUITest] No stored credentials. Hiding loading screen.");
            DisableLoadingScreen();
        }
    }

    // Saves current inputs to PlayerPrefs and triggers PerformLogin
    public void ManualLogin()
    {
        string email    = emailField.text.Trim();
        string password = passwordField.text.Trim();

        PlayerPrefs.SetString("saved_email", email);
        PlayerPrefs.SetString("saved_password", password);
        PlayerPrefs.Save();

        PerformLogin(email, password);
    }

    // Performs user registration through AuthManager
    public void DoRegister()
    {
        string email = emailField.text.Trim();
        string pass  = passwordField.text.Trim();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pass))
        {
            feedbackLabel.text = "❌ Email o contraseña vacíos";
            return;
        }

        feedbackLabel.text = "⌛ Registrando...";
        registerButton.interactable = false;

        AuthManager.I.Register(email, pass,
            token =>
            {
                feedbackLabel.text = "✔ Registrado. Cargando stats.";

                PlayerPrefs.SetString("saved_email", email);
                PlayerPrefs.SetString("saved_password", pass);
                PlayerPrefs.Save();

                StartCoroutine(statsDAO.GetStats(token, OnStatsOk, OnErr));
            },
            OnErr);
    }

    // Handles login or registration errors
    private void OnErr(string raw)
    {
        Debug.LogError("❌ Login error: " + raw);

        if (feedbackLabel != null && !feedbackLabel.gameObject.activeSelf)
            feedbackLabel.gameObject.SetActive(true);

        string prettyMsg = ParseSupabaseError(raw);

        if (prettyMsg.ToLower().Contains("token vacío") || prettyMsg.ToLower().Contains("token vacio"))
            prettyMsg = "Registration completed, verify your email address";

        feedbackLabel.text = "❌ " + prettyMsg;

        loginButton.interactable  = true;
        registerButton.interactable = true;

        DisableLoadingScreen();   // hides loading screen
    }

    // === STATS CALLBACKS ===
    // Processes stats after a successful authentication
    private void OnStatsOk(PlayerStatsDTO remoteDto)
    {
        var localRepo = new PlayerStatsRepo(AuthManager.I.UserId, statsDAO);

        // ───────────────────────────────
        // 1) The user HAS NO row in player_stats
        // ───────────────────────────────
        if (remoteDto == null)
        {
            remoteDto = new PlayerStatsDTO
            {
                user_id               = AuthManager.I.UserId,
                best_distance         = 0,
                best_coins_earned     = 0,
                total_sessions        = 0,
                total_distance        = 0,
                total_coins_collected = 0,
                total_coins_spent     = 0,
                challenges_completed  = 0,
                actual_skin           = "default",
                updated_at            = System.DateTime.UtcNow
            };

            // 1-A. Create remote row (upsert) then local JSON
            StartCoroutine(statsDAO.UpsertStats(remoteDto,
                () => StartCoroutine(localRepo.Save(
                        remoteDto,
                        () => {
                            Debug.Log("✅ Initial stats created (remote + local)");
                            StartCoroutine(FinishLoadingAndLoadMenu());
                        },
                        err => {
                            Debug.LogWarning("⚠ Error saving local stats: " + err);
                            StartCoroutine(FinishLoadingAndLoadMenu());
                        },
                        pushRemote:false,
                        touchTimestamp:false)),
                err => {
                    Debug.LogWarning("⚠ Error creating remote stats: " + err);
                    // No network → at least create local JSON for offline mode
                    StartCoroutine(localRepo.Save(
                        remoteDto,
                        () => StartCoroutine(FinishLoadingAndLoadMenu()),
                        err2 => StartCoroutine(FinishLoadingAndLoadMenu()),
                        pushRemote:false,
                        touchTimestamp:false));
                }));

            return;   // nothing else to do on first login
        }

        // ───────────────────────────────
        // 2) The user ALREADY has remote stats
        // ───────────────────────────────
        Debug.Log($"📊 Stats received: SKIN {remoteDto.actual_skin}, " +
                  $"COINS {remoteDto.total_coins_collected - remoteDto.total_coins_spent}");

        StartCoroutine(localRepo.Get(AuthManager.I.UserId, localDto =>
        {
            // 2-A. Choose the newest version
            bool remoteIsNewer = remoteDto.updated_at > localDto.updated_at;
            var  finalDto      = remoteIsNewer ? remoteDto : localDto;

            Debug.Log($"[SyncDecision] Using: {(remoteIsNewer ? "🌐 REMOTE" : "💾 LOCAL")}");

            // 2-B. PlayerPrefs
            PlayerPrefs.SetInt   ("total_coins",
                                  (int)(finalDto.total_coins_collected - finalDto.total_coins_spent));
            PlayerPrefs.SetString("actual_skin", finalDto.actual_skin);
            PlayerPrefs.Save();

            // 2-C. Save the final DTO locally
            StartCoroutine(localRepo.Save(
                finalDto,
                () => {
                    Debug.Log("✅ Local save completed after login");
                    StartCoroutine(SyncInventoryAndThenLoadMenu());
                },
                err => {
                    Debug.LogWarning("⚠ Error saving locally after login: " + err);
                    StartCoroutine(SyncInventoryAndThenLoadMenu());
                },
                pushRemote:false,
                touchTimestamp:false));

            // 2-D. If local is newer and differs, push it
            bool valuesDiffer =
                remoteDto.best_distance         != localDto.best_distance          ||
                remoteDto.best_coins_earned     != localDto.best_coins_earned      ||
                remoteDto.total_sessions        != localDto.total_sessions         ||
                remoteDto.total_distance        != localDto.total_distance         ||
                remoteDto.total_coins_collected != localDto.total_coins_collected  ||
                remoteDto.total_coins_spent     != localDto.total_coins_spent      ||
                remoteDto.challenges_completed  != localDto.challenges_completed   ||
                remoteDto.actual_skin           != localDto.actual_skin;

            if (!remoteIsNewer && valuesDiffer)
            {
                Debug.Log("⬆ Sync: Sending UPSERT with newer client data.");
                StartCoroutine(statsDAO.UpsertStats(finalDto,
                    ()  => Debug.Log("✅ Offline data synced successfully."),
                    err => Debug.LogWarning("⚠ Error syncing local data: " + err)));
            }

        },
        // 2-E. Failed to read local JSON → use remote as fallback
        err => {
            Debug.LogWarning("⚠ Could not load local JSON: " + err);

            StartCoroutine(localRepo.Save(remoteDto,
                () => {
                    Debug.Log("✅ Local save with remote data (fallback)");
                    SceneManager.LoadScene("menutest1");
                },
                err2 => {
                    Debug.LogWarning("⚠ Error saving remote as fallback: " + err2);
                    SceneManager.LoadScene("menutest1");
                },
                pushRemote:false,
                touchTimestamp:false));
        }));

        // 2-F. If everything went fine, the coroutines will load the menu
        StartCoroutine(FinishLoadingAndLoadMenu());
    }

    // === INVENTORY METHODS ===
    // Synchronizes inventory after stats handling and then loads the menu
    private IEnumerator SyncInventoryAndThenLoadMenu()
    {
        Debug.Log("🔁 [LoginUITest] Starting inventory sync...");

        var invRepo = new InventoryRepo(
            AuthManager.I.UserId,
            new InventoryDAO(AuthManager.I.Url, AuthManager.I.AnonKey)
        );

        List<InventoryDTO> inventoryResult = null;

        yield return StartCoroutine(invRepo.Sync(
            AuthManager.I.AccessToken,
            AuthManager.I.UserId,
            list =>
            {
                inventoryResult = list;
                Debug.Log("✅ [LoginUITest] Inventory sync success callback");
            },
            err =>
            {
                Debug.LogWarning($"❌ [LoginUITest] Error syncing inventory: {err}");
            }
        ));

        if (inventoryResult != null)
        {
            var skins = inventoryResult.ConvertAll(i => i.item_name);
            Debug.Log($"🎒 [LoginUITest] Inventory synced. Skins: {string.Join(", ", skins)}");

            if (SkinManager.I != null)
            {
                Debug.Log("🎨 [LoginUITest] Applying synced skins...");
                SkinManager.I.SetOwnedSkins(skins);
            }
            else
            {
                Debug.LogWarning("⚠ [LoginUITest] SkinManager.I is null.");
            }
        }
        else
        {
            Debug.LogWarning("⚠ [LoginUITest] Inventory null after sync.");
        }

        Debug.Log("🚀 [LoginUITest] Redirecting to 'menutest1'");
        SceneManager.LoadScene("menutest1");
    }

    // === HELPER METHODS ===
    // Attempts to extract the Supabase error message from JSON
    private string ParseSupabaseError(string raw)
    {
        try
        {
            var errObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(raw);
            if (errObj != null && errObj.ContainsKey("msg"))
                return errObj["msg"].ToString();
        }
        catch
        {
            // If not valid JSON, return raw text
        }
        return raw;
    }
}