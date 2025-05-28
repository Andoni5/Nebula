using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.U2D.Animation;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Linq;
using System;
using System.IO;

public class NebulaMenuUI : MonoBehaviour
{
    // === SERIALIZED FIELDS ===
    [Header("Panel de selección de skins")]
    public GameObject skinsPanel;
    public Transform skinsContent;
    public Button skinButtonPrefab;
    public Image selectedSkinPreview;

    [Header("Botones Skins")]
    public Button playButton;
    public Button cosmeticsButton;
    public Button exitButton;
    public Button backButton;

    [Header("Ajustes")]
    public GameObject settingsPanel;
    public Button aButton;
    public Button backSettingsButton;
    public Button logoutButton;

    [Header("Achievements")]
    public GameObject achievementPanel;
    public Transform achievementContent;
    public GameObject achievementEntryPrefab;
    public Button achievementButton;
    public Button backAchievementButton;

    [Header("Nombre de la escena del juego")]
    [SerializeField] string gameSceneName = "GameScene";

    [Header("Monedas")]
    public Text starNumberText;

    [Header("Popup de confirmación")]
    public GameObject purchasePopup;
    public Text purchaseMessageText;
    public Button yesPurchaseButton;
    public Button noPurchaseButton;

    [Header("Daily Challenges / Misiones")]
    [SerializeField] Button missionsButton;
    [SerializeField] GameObject missionsPanel;
    [SerializeField] Button missionsBackButton;
    [SerializeField] Transform missionsContent;
    [SerializeField] GameObject missionButtonPrefab;
    [SerializeField] Sprite coinsIcon;
    [SerializeField] Sprite walkIcon;

    // === PRIVATE FIELDS ===
    public static NebulaMenuUI Instance { get; private set; }
    private List<string> ownedSkins = new();
    private InventoryRepo inventoryRepo;
    private int cachedCoins = 0;

    // === UNITY CALLBACKS ===
    // Unity lifecycle: called first to set the singleton instance
    void Awake()
    {
        Instance = this;
    }

    // Unity lifecycle: clean up the singleton reference when destroyed
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Unity lifecycle: initialise UI, listeners and coroutines
    void Start()
    {
        playButton?.onClick.AddListener(OnPlay);
        cosmeticsButton?.onClick.AddListener(OnOpenSkins);
        exitButton?.onClick.AddListener(OnExit);
        backButton?.onClick.AddListener(OnCloseSkins);

        skinsPanel?.SetActive(false);
        StartCoroutine(LoadInventoryAndPopulate());
        StartCoroutine(WaitForSkinLoad());
        UpdateStarCounter();

        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            StartCoroutine(LoadCosmeticsAndCache());
        }
        StartCoroutine(TrySyncPlayerStats());

        if (settingsPanel != null) settingsPanel.SetActive(false);

        aButton?.onClick.AddListener(OpenSettings);
        backSettingsButton?.onClick.AddListener(CloseSettings);
        logoutButton?.onClick.AddListener(Logout);

        achievementButton?.onClick.AddListener(OpenAchievements);
        backAchievementButton?.onClick.AddListener(CloseAchievements);

        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            StartCoroutine(MergeInventoryIfNeededOnline());
            StartCoroutine(PushOfflineCompleted());
            StartCoroutine(SyncMissionsCache());
        }

        missionsButton?.onClick.AddListener(OpenMissions);
        missionsBackButton?.onClick.AddListener(() => missionsPanel.SetActive(false));
    }

    // === PLAY FLOW METHODS ===
    // Begins the play sequence when the Play button is pressed
    public void OnPlay()
    {
        Debug.Log("[NebulaMenuUI] Play!");
        StartCoroutine(EnsureMissionsSyncedAndPlay());
    }

    // Ensures mission cache is up-to-date before loading the game scene
    IEnumerator EnsureMissionsSyncedAndPlay()
    {
        bool online = Application.internetReachability != NetworkReachability.NotReachable;
        if (online) yield return SyncMissionsCache();
        SceneManager.LoadScene(gameSceneName);
    }

    // Downloads daily challenges and saves them to local cache
    IEnumerator SyncMissionsCache()
    {
        yield return new WaitUntil(() => AuthManager.I != null);

        var dailyDao = new DailyChallengesDAO(AuthManager.I.Url, AuthManager.I.AnonKey);
        var compDao = new CompletedChallengesDAO(
            AuthManager.I.Url,
            AuthManager.I.AnonKey,
            AuthManager.I.AccessToken);

        List<DailyChallengeDTO> active = null;
        string err = null;

        yield return dailyDao.GetActiveChallenges(
            list => active = list,
            e => err = e);

        if (active == null)
        {
            Debug.LogWarning("[Missions-Sync] No se pudieron obtener misiones: " + err);
            yield break;
        }

        HashSet<int> done = null;
        yield return compDao.GetCompleted(
            ok => done = ok,
            e => Debug.LogWarning("[Missions-Sync] done fail: " + e));

        if (done != null)
            active = active.Where(c => !done.Contains(c.id)).ToList();

        var cache = new JsonTable<DailyChallengeDTO>("daily_challenges.json");
        cache.ReplaceAll(active);
        yield return cache.Save(
            () => Debug.Log($"[Missions-Sync] ✅ Cache actualizada ({active.Count} retos)."),
            e => Debug.LogWarning("[Missions-Sync] ❌ " + e));
    }

    // === SKINS UI METHODS ===
    // Opens the skins selection panel
    public void OnOpenSkins()
    {
        Debug.Log("[NebulaMenuUI] Abriendo panel de skins...");
        skinsPanel?.SetActive(true);
        PopulateSkins();
    }

    // Closes the skins selection panel
    public void OnCloseSkins()
    {
        Debug.Log("[NebulaMenuUI] Cerrando panel de skins...");
        skinsPanel?.SetActive(false);
    }

    // Creates skin buttons and previews inside the skins panel
    void PopulateSkins()
    {
        if (!skinButtonPrefab || !skinsContent || SkinManager.I == null) return;

        foreach (Transform c in skinsContent)
            Destroy(c.gameObject);

        string[] allSkins = SkinManager.I.GetAllSkins();
        List<string> ownedList = SkinManager.I.GetOwnedSkins();

        foreach (string cat in allSkins)
        {
            Button btn = Instantiate(skinButtonPrefab, skinsContent);

            var txt = btn.transform.Find("SkinName")?.GetComponent<Text>();
            if (txt) txt.text = cat.ToUpper();

            var img = btn.transform.Find("SkinImage")?.GetComponent<Image>();
            Sprite preview = SkinManager.I.IsSkinUnlocked(cat)
                ? SkinManager.I.GetPreviewSprite(cat)
                : SkinManager.I.GetPreviewSpriteStore(cat);
            if (preview && img) img.sprite = preview;

            btn.onClick.AddListener(() =>
            {
                var nameTxt = btn.transform.Find("SkinName")?.GetComponent<Text>();
                if (SkinManager.I.IsSkinUnlocked(cat))
                {
                    SkinManager.I.SetSkin(cat);
                    UpdatePrefabPreview(cat);
                    OnCloseSkins();
                }
                else if (nameTxt != null && int.TryParse(nameTxt.text, out int price))
                {
                    int coins = cachedCoins;
                    ShowPurchaseConfirmation(cat, price, coins, btn);
                }
                else
                {
                    if (nameTxt != null) nameTxt.text = "Cargando";
                    StartCoroutine(GetSkinPrice(cat, btn));
                }
            });
        }

        int total = allSkins.Length;
        int columns = 5;
        int rows = Mathf.CeilToInt((float)total / columns);

        GridLayoutGroup grid = skinsContent.GetComponent<GridLayoutGroup>();
        if (grid)
        {
            float totalHeight = rows * (grid.cellSize.y + grid.spacing.y);
            var contentRT = skinsContent.GetComponent<RectTransform>();
            Vector2 size = contentRT.sizeDelta;
            contentRT.sizeDelta = new Vector2(size.x, totalHeight);
        }
    }

    // Updates the preview images to reflect the selected skin
    void UpdatePrefabPreview(string category)
    {
        if (SkinManager.I == null) return;

        Sprite preview = SkinManager.I.GetPreviewSprite(category);
        if (!preview) return;

        if (skinButtonPrefab)
        {
            Image prefabImg = skinButtonPrefab.transform.Find("SkinImage")?.GetComponent<Image>();
            if (prefabImg) prefabImg.sprite = preview;
        }

        if (selectedSkinPreview) selectedSkinPreview.sprite = preview;
    }

    // Refreshes the preview when skin is changed externally
    public void RefreshSelectedSkinPreview()
    {
        string cat = SkinManager.I ? SkinManager.I.SelectedSkin : "default";
        UpdatePrefabPreview(cat);
    }

    // Waits until the skin manager is ready and then shows the preview
    IEnumerator WaitForSkinLoad()
    {
        yield return new WaitUntil(() => SkinManager.I != null && SkinManager.I.SkinReady);
        Debug.Log("[NebulaMenuUI] Actualizando preview con skin cargada: " + SkinManager.I.SelectedSkin);
        UpdatePrefabPreview(SkinManager.I.SelectedSkin);
    }

    // === COIN METHODS ===
    // Starts the coroutine that loads the coin count
    void UpdateStarCounter()
    {
        StartCoroutine(LoadCoinsFromRepo());
    }

    // Loads coins from the repository (online or offline)
    IEnumerator LoadCoinsFromRepo()
    {
        yield return new WaitUntil(() => AuthManager.I != null);

        string uid = AuthManager.I.UserId;
        var dao = new PlayerStatsDAO(AuthManager.I.Url, AuthManager.I.AnonKey);
        var repo = new PlayerStatsRepo(uid, dao);

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            yield return repo.Get(AuthManager.I.AccessToken,
                dto =>
                {
                    int coins = (int)(dto.total_coins_collected - dto.total_coins_spent);
                    cachedCoins = coins;
                    starNumberText.text = coins.ToString();
                    Debug.Log($"[UI] ✅ Coins offline: {coins}");
                },
                err =>
                {
                    starNumberText.text = "???";
                    cachedCoins = 0;
                    Debug.LogWarning("❌ Error al obtener coins offline: " + err);
                });
        }
        else
        {
            yield return repo.Sync(
                dto =>
                {
                    int coins = (int)(dto.total_coins_collected - dto.total_coins_spent);
                    cachedCoins = coins;
                    starNumberText.text = coins.ToString();
                    Debug.Log($"[UI] ✅ Coins tras sync: {coins}");
                },
                err =>
                {
                    starNumberText.text = "???";
                    cachedCoins = 0;
                    Debug.LogWarning("❌ Error en sync: " + err);
                });
        }
    }

    // === SETTINGS UI METHODS ===
    // Opens the settings panel
    public void OpenSettings()
    {
        Debug.Log("[NebulaMenuUI] Abriendo ajustes...");
        settingsPanel?.SetActive(true);
    }

    // Closes the settings panel
    public void CloseSettings()
    {
        Debug.Log("[NebulaMenuUI] Cerrando ajustes...");
        settingsPanel?.SetActive(false);
    }

    // Logs out the current user and loads the login scene
    public void Logout()
    {
        Debug.Log("[NebulaMenuUI] Cerrando sesión...");

        PlayerPrefs.DeleteKey("access_token");
        PlayerPrefs.DeleteKey("refresh_token");
        PlayerPrefs.DeleteKey("saved_email");
        PlayerPrefs.DeleteKey("saved_password");
        PlayerPrefs.Save();

        PersistentObjectsManager.DestroyAll();
        SceneManager.LoadScene("LoginPro", LoadSceneMode.Single);
    }

    // === ACHIEVEMENT METHODS ===
    // Opens the achievements panel
    void OpenAchievements()
    {
        Debug.Log("[NebulaMenuUI] Abriendo panel de logros...");
        achievementPanel?.SetActive(true);
        StartCoroutine(PopulateAchievements());
    }

    // Closes the achievements panel
    void CloseAchievements()
    {
        Debug.Log("[NebulaMenuUI] Cerrando panel de logros...");
        achievementPanel?.SetActive(false);
    }

    // Fills the achievements panel with current statistics
    IEnumerator PopulateAchievements()
    {
        foreach (Transform child in achievementContent)
            Destroy(child.gameObject);

        yield return new WaitUntil(() => AuthManager.I != null);

        string uid = AuthManager.I.UserId;
        var dao = new PlayerStatsDAO(AuthManager.I.Url, AuthManager.I.AnonKey);
        var repo = new PlayerStatsRepo(uid, dao);

        PlayerStatsDTO stats = null;
        bool done = false;

        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            yield return repo.Sync(
                dto => { stats = dto; done = true; },
                err => { Debug.LogWarning("❌ Error en Sync: " + err); done = false; });
        }

        if (!done)
        {
            yield return repo.Get(
                token: AuthManager.I.AccessToken,
                ok: dto => stats = dto,
                err => Debug.LogWarning("❌ Error al cargar logros offline: " + err));
        }

        if (stats == null) yield break;

        AddAchievement("Mejor distancia", $"{stats.best_distance} metros", "best_distance");
        AddAchievement("Max monedas en una sesion", $"{stats.best_coins_earned} monedas", "best_coins_earned");
        AddAchievement("Total de sesiones", $"{stats.total_sessions} sesiones", "total_sessions");
        AddAchievement("Distancia total recorrida", $"{stats.total_distance} metros", "total_distance");
        AddAchievement("Monedas recogidas", $"{stats.total_coins_collected} monedas", "total_coins_collected");
        AddAchievement("Monedas gastadas", $"{stats.total_coins_spent} monedas", "total_coins_spent");
        AddAchievement("Retos completados", $"{stats.challenges_completed} retos", "challenges_completed");
    }

    // Instantiates a single achievement entry in the UI
    void AddAchievement(string titulo, string valor, string iconoNombre)
    {
        if (!achievementEntryPrefab || !achievementContent) return;

        GameObject entry = Instantiate(achievementEntryPrefab, achievementContent);

        var titleText = entry.transform.Find("TitleText")?.GetComponent<Text>();
        var valueText = entry.transform.Find("ValueText")?.GetComponent<Text>();
        var iconImage = entry.transform.Find("Icon")?.GetComponent<Image>();

        if (titleText) titleText.text = titulo;
        if (valueText) valueText.text = valor;

        if (iconImage)
        {
            Sprite icon = Resources.Load<Sprite>($"logros/{iconoNombre}");
            if (icon) iconImage.sprite = icon;
            else Debug.LogWarning($"[Logros] No se encontró el icono para: {iconoNombre}");
        }
    }

    // === INVENTORY METHODS ===
    // Loads inventory from repo and populates skin list
    IEnumerator LoadInventoryAndPopulate()
    {
        yield return new WaitUntil(() => AuthManager.I != null);

        string uid = AuthManager.I.UserId;
        string token = AuthManager.I.AccessToken;

        var dao = new InventoryDAO(AuthManager.I.Url, AuthManager.I.AnonKey);
        var repo = new InventoryRepo(uid, dao);

        List<InventoryDTO> inventory = null;
        bool done = false;

        yield return repo.GetLocalOrSync(token, uid,
            onOk: list => { inventory = list; done = true; },
            onErr: err => { Debug.LogWarning("❌ No se pudo cargar el inventario: " + err); done = true; });

        yield return new WaitUntil(() => done);

        if (inventory != null)
        {
            ownedSkins = inventory.Select(i => i.item_name).Distinct().ToList();
            SkinManager.I.SetOwnedSkins(ownedSkins);
            PopulateSkins();
        }
    }

    // Downloads full cosmetics catalog and caches it locally
    IEnumerator LoadCosmeticsAndCache()
    {
        var dao = new CosmeticsDAO(AuthManager.I.Url, AuthManager.I.AnonKey);

        List<CosmeticItemDTO> fullCatalog = null;

        yield return dao.GetAllCosmetics(
            onOk: list => fullCatalog = list,
            onErr: err => Debug.LogWarning("❌ No se pudo obtener catálogo de cosméticos online: " + err));

        if (fullCatalog != null)
        {
            var table = new JsonTable<CosmeticItemDTO>("cosmetics.json");
            table.ReplaceAll(fullCatalog);
            yield return table.Save(
                () => Debug.Log("✅ Catálogo de cosméticos guardado en cache"),
                err => Debug.LogWarning("❌ Error guardando catálogo local: " + err));
        }
    }

    // Merges server and local inventory if needed
    IEnumerator MergeInventoryIfNeededOnline()
    {
        yield return new WaitUntil(() => AuthManager.I != null);

        string userId = AuthManager.I.UserId;
        string token = AuthManager.I.AccessToken;

        inventoryRepo = new InventoryRepo(
            userId,
            new InventoryDAO(AuthManager.I.Url, AuthManager.I.AnonKey));

        yield return inventoryRepo.MergeInventoryIfNeeded(
            token,
            userId,
            onSuccess: list =>
            {
                Debug.Log($"🎒 [NebulaMenuUI] Inventario fusionado correctamente. Total: {list.Count} skins");
                var skinNames = list.Select(i => i.item_name).Distinct().ToList();
                SkinManager.I?.SetOwnedSkins(skinNames);
                PopulateSkins();
            },
            onError: err => Debug.LogWarning("❌ [NebulaMenuUI] Error durante fusión de inventario: " + err));
    }

    // Returns the absolute path to the inventory cache file
    private string GetInventoryFilePath(string userId)
    {
        return Path.Combine(Application.persistentDataPath, "offline_db", $"{userId}-inventory.json");
    }

    // === PURCHASE METHODS ===
    // Requests the skin price from the server and updates the button text
    IEnumerator GetSkinPrice(string skinName, Button skinBtn)
    {
        var dao = new CosmeticsDAO(AuthManager.I.Url, AuthManager.I.AnonKey);

        yield return dao.GetCosmetic(skinName,
            item =>
            {
                var txt = skinBtn.transform.Find("SkinName")?.GetComponent<Text>();
                if (txt) txt.text = $"{item.price_coins}";

                var table = new JsonTable<CosmeticItemDTO>("cosmetics.json");
                table.ReplaceAllIfNeeded(item);
                _ = table.Save(
                    () => Debug.Log($"✅ Guardado {item.name} en cache"),
                    err => Debug.LogWarning("❌ Error guardando cosmetic local: " + err));
            },
            err =>
            {
                Debug.LogWarning("❌ No se pudo obtener precio online: " + err);
                StartCoroutine(LoadPriceOffline(skinName, skinBtn));
            });
    }

    // Loads the skin price from local cache when offline
    IEnumerator LoadPriceOffline(string skinName, Button skinBtn)
    {
        var table = new JsonTable<CosmeticItemDTO>("cosmetics.json");
        string loadError = null;

        yield return table.Load(() => { }, err => loadError = err);

        var txt = skinBtn.transform.Find("SkinName")?.GetComponent<Text>();

        if (!string.IsNullOrEmpty(loadError))
        {
            Debug.LogWarning("❌ No se pudo leer cosmetics.json: " + loadError);
            if (txt) txt.text = "No disponible";
            yield break;
        }

        var item = table.All.FirstOrDefault(i => i.name == skinName);
        if (item != null && txt != null)
        {
            txt.text = $"{item.price_coins}";
        }
        else if (txt != null)
        {
            txt.text = "No disponible";
            Debug.LogWarning($"❌ Precio de '{skinName}' no encontrado en cache.");
        }
    }

    // Shows a confirmation popup before purchasing a skin
    void ShowPurchaseConfirmation(string skinName, int precio, int coinsActuales, Button skinBtn)
    {
        if (!purchasePopup || !purchaseMessageText || !yesPurchaseButton || !noPurchaseButton) return;

        purchaseMessageText.text = $"\n¿Quieres comprar \n'{skinName.ToUpper()}'\npor {precio} monedas?\n" +
                                   $"\nActualmente tienes {coinsActuales}.";

        purchasePopup.SetActive(true);

        yesPurchaseButton.onClick.RemoveAllListeners();
        noPurchaseButton.onClick.RemoveAllListeners();

        yesPurchaseButton.onClick.AddListener(() =>
        {
            purchasePopup.SetActive(false);
            StartCoroutine(TryBuySkin(skinName, precio, skinBtn));
        });

        noPurchaseButton.onClick.AddListener(() => purchasePopup.SetActive(false));
    }

    // Attempts to buy a skin (online or offline) and updates stats and inventory
    IEnumerator TryBuySkin(string skinName, int precio, Button btn)
    {
        yield return new WaitUntil(() => AuthManager.I != null);
        var uid = AuthManager.I.UserId;
        var repo = new PlayerStatsRepo(uid, new PlayerStatsDAO(AuthManager.I.Url, AuthManager.I.AnonKey));

        PlayerStatsDTO stats = null;
        bool online = Application.internetReachability != NetworkReachability.NotReachable;

        if (online)
            yield return repo.Sync(dto => stats = dto, err => Debug.LogWarning(err));
        else
            yield return repo.Get(AuthManager.I.AccessToken, dto => stats = dto, err => Debug.LogWarning(err));

        if (stats == null)
        {
            Debug.LogWarning("❌ No se pudieron cargar stats para comprar skin.");
            yield break;
        }

        int coins = (int)(stats.total_coins_collected - stats.total_coins_spent);
        if (coins < precio)
        {
            Debug.Log("❌ No tienes suficientes monedas.");
            var txt = btn.transform.Find("SkinName")?.GetComponent<Text>();
            if (txt != null)
            {
                string originalText = skinName.ToUpper();
                txt.text = "Error";
                StartCoroutine(RestoreSkinName(txt, originalText));
            }
            yield break;
        }

        Debug.Log($"🪙 Comprando {skinName} por {precio} monedas");

        if (online)
        {
            var invReq = new UnityWebRequest($"{AuthManager.I.Url}/rest/v1/inventory", "POST");
            invReq.SetRequestHeader("apikey", AuthManager.I.AnonKey);
            invReq.SetRequestHeader("Authorization", "Bearer " + AuthManager.I.AccessToken);
            invReq.SetRequestHeader("Content-Type", "application/json");

            string json = JsonUtility.ToJson(new InventoryDTO { user_id = uid, item_name = skinName });
            invReq.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            invReq.downloadHandler = new DownloadHandlerBuffer();

            yield return invReq.SendWebRequest();

            if (invReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("❌ Error al guardar en inventario: " + invReq.error);
                yield break;
            }
        }
        else
        {
            Debug.Log("[Offline] Guardando skin en inventario local...");

            string filePath = GetInventoryFilePath(uid);
            string dir = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var invTable = new JsonTable<InventoryDTO>(filePath);
            string invErr = null;

            yield return invTable.Load(() => { }, err => invErr = err);
            if (!string.IsNullOrEmpty(invErr))
            {
                Debug.LogWarning("❌ Error cargando inventario local: " + invErr);
                yield break;
            }

            invTable.Add(new InventoryDTO
            {
                user_id = uid,
                item_name = skinName,
                acquired_at = DateTime.UtcNow
            });

            yield return invTable.Save(
                () => Debug.Log("✅ Inventario local actualizado"),
                err => Debug.LogWarning("❌ No se pudo guardar inventario offline: " + err));
        }

        stats.total_coins_spent += precio;
        stats.actual_skin = skinName;
        stats.updated_at = DateTime.UtcNow;

        yield return repo.Save(
            stats,
            () => Debug.Log("✅ Guardado previo a sync exitoso"),
            err => Debug.LogWarning("❌ Error al guardar antes del sync: " + err));

        if (online)
        {
            yield return repo.Save(
                stats,
                () => Debug.Log("✅ Skin comprada y stats actualizados"),
                err => Debug.LogWarning("❌ No se pudo guardar los stats: " + err));

            var invTable = new JsonTable<InventoryDTO>(GetInventoryFilePath(uid));
            string invErr = null;

            yield return invTable.Load(() => { }, err => invErr = err);
            if (string.IsNullOrEmpty(invErr))
            {
                invTable.Add(new InventoryDTO
                {
                    user_id = uid,
                    item_name = skinName,
                    acquired_at = DateTime.UtcNow
                });

                yield return invTable.Save(
                    () => Debug.Log("✅ Inventario local actualizado tras compra online"),
                    err => Debug.LogWarning("❌ No se pudo guardar inventario offline: " + err));

                var newOwnedSkins = invTable.All.Select(i => i.item_name).ToList();
                SkinManager.I.SetOwnedSkins(newOwnedSkins);
                PopulateSkins();
            }
        }
        else
        {
            string statsPath = Path.Combine(Application.persistentDataPath, "offline_db", $"{uid}-player_stats.json");
            var statsTable = new JsonTable<PlayerStatsDTO>(statsPath);
            string loadErr = null;

            yield return statsTable.Load(() => { }, err => loadErr = err);
            if (string.IsNullOrEmpty(loadErr))
            {
                statsTable.ReplaceAll(new List<PlayerStatsDTO> { stats });

                yield return statsTable.Save(
                    () => Debug.Log($"✅ Stats actualizados offline tras compra (skin: {skinName})"),
                    err => Debug.LogWarning("❌ Error guardando stats offline: " + err));
            }
        }

        SkinManager.I.SetSkin(skinName, saveToRepo: false);

        var updatedSkins = SkinManager.I.GetOwnedSkins();
        if (!updatedSkins.Contains(skinName)) updatedSkins.Add(skinName);
        SkinManager.I.SetOwnedSkins(updatedSkins);

        UpdatePrefabPreview(skinName);
        OnCloseSkins();
        UpdateStarCounter();
    }

    // Restores the button text after showing an error
    IEnumerator RestoreSkinName(Text txt, string original)
    {
        Color originalColor = txt.color;
        txt.color = Color.red;
        yield return new WaitForSeconds(1f);

        if (txt)
        {
            txt.text = original;
            txt.color = originalColor;
        }
    }

    // === MISSIONS UI METHODS ===
    // Opens the missions panel and populates it
    void OpenMissions()
    {
        Debug.Log("[NebulaMenuUI] Abriendo panel de misiones...");
        missionsPanel.SetActive(true);
        StartCoroutine(PopulateMissions());
    }

    // Populates the missions panel with daily challenges
    IEnumerator PopulateMissions()
    {
        foreach (Transform c in missionsContent) Destroy(c.gameObject);
        yield return new WaitUntil(() => AuthManager.I != null);

        var dao = new DailyChallengesDAO(AuthManager.I.Url, AuthManager.I.AnonKey);
        var cache = new JsonTable<DailyChallengeDTO>("daily_challenges.json");

        List<DailyChallengeDTO> list = null;
        string error = null;
        bool online = Application.internetReachability != NetworkReachability.NotReachable;

        if (online)
        {
            yield return dao.GetActiveChallenges(
                ok => list = ok,
                err => error = err);

            if (list != null)
            {
                var completedDao = new CompletedChallengesDAO(
                    AuthManager.I.Url,
                    AuthManager.I.AnonKey,
                    AuthManager.I.AccessToken);

                HashSet<int> completedIds = null;
                yield return completedDao.GetCompleted(
                    ids => completedIds = ids,
                    e => Debug.LogWarning("[Missions] Fallo completadas: " + e));

                if (completedIds != null)
                    list = list.Where(ch => !completedIds.Contains(ch.id)).ToList();

                var offlineCompleted = new HashSet<int>();
                var compPath = Path.Combine(
                    Application.persistentDataPath,
                    "offline_db",
                    $"{AuthManager.I.UserId}-completed_challenges.json");

                var compTable = new JsonTable<int>(compPath);
                yield return compTable.Load(() => { }, _ => { });

                offlineCompleted.UnionWith(compTable.All);
                if (completedIds != null) offlineCompleted.UnionWith(completedIds);
                list = list.Where(ch => !offlineCompleted.Contains(ch.id)).ToList();

                cache.ReplaceAll(list);
                yield return cache.Save(() => { }, _ => { });
            }
        }

        if (list == null)
        {
            yield return cache.Load(() => list = cache.All.ToList(), err => error = err);
        }

        if (list == null || list.Count == 0)
        {
            Debug.LogWarning("[Missions] Sin retos hoy. " + error);
            yield break;
        }

        foreach (var ch in list.OrderBy(c => c.id))
            CreateMissionEntry(ch);
    }

    // Creates a single mission entry button in the UI
    void CreateMissionEntry(DailyChallengeDTO ch)
    {
        var go = Instantiate(missionButtonPrefab, missionsContent);

        var descT = go.transform.Find("Description");
        if (descT) descT.GetComponent<Text>().text = ch.description;

        var iconT = go.transform.Find("Icon");
        if (iconT) iconT.GetComponent<Image>().sprite = ch.challenge_type == "WALK" ? walkIcon : coinsIcon;

        var rewT = go.transform.Find("RewardText");
        if (rewT) rewT.GetComponent<Text>().text = $"{ch.reward_coins} 🪙";

        var amtT = go.transform.Find("AmountText");
        if (amtT) amtT.GetComponent<Text>().text = ch.amount_needed.ToString();
    }

    // Removes a completed mission from the UI list
    public void RemoveMissionFromUI(int challengeId)
    {
        var buttons = missionsContent.GetComponentsInChildren<MissionButton>();
        foreach (var btn in buttons)
        {
            if (btn.ChallengeId == challengeId)
            {
                Destroy(btn.gameObject);
                break;
            }
        }
    }

    // === MISSIONS SYNC METHODS ===
    // Uploads locally completed missions when back online
    IEnumerator PushOfflineCompleted()
    {
        yield return new WaitUntil(() => AuthManager.I != null);

        string uid = AuthManager.I.UserId;
        string file = Path.Combine(Application.persistentDataPath, "offline_db", $"{uid}-completed_challenges.json");
        if (!File.Exists(file)) yield break;

        var tbl = new JsonTable<int>(file);
        yield return tbl.Load(() => { }, _ => { });

        var dao = new CompletedChallengesDAO(
            AuthManager.I.Url,
            AuthManager.I.AnonKey,
            AuthManager.I.AccessToken);

        foreach (var id in tbl.All)
            yield return dao.InsertCompleted(id, uid, () => { }, e => Debug.LogWarning(e));

        tbl.ReplaceAll(new List<int>());
        yield return tbl.Save(() => File.Delete(file), _ => { });
    }

    // === PLAYER STATS METHODS ===
    // Synchronises player statistics with the server
    IEnumerator TrySyncPlayerStats()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable) yield break;

        yield return new WaitUntil(() => AuthManager.I != null);

        var repo = new PlayerStatsRepo(AuthManager.I.UserId, new PlayerStatsDAO(AuthManager.I.Url, AuthManager.I.AnonKey));
        yield return repo.Sync(
            dto => Debug.Log("✅ Sync OK con best_distance: " + dto.best_distance),
            err => Debug.LogWarning("❌ Sync FAIL: " + err));
    }

    // === APPLICATION METHODS ===
    // Exits play mode (Editor) or quits the application
    public void OnExit()
    {
        Debug.Log("[NebulaMenuUI] Saliendo...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // === UTILITY METHODS ===
    // Parses an integer from a button label
    int ParsePrecio(string texto)
    {
        string numPart = texto.Split(' ')[0];
        int.TryParse(numPart, out int resultado);
        return resultado;
    }
}