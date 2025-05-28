using UnityEngine;
using UnityEngine.U2D.Animation;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class SkinManager : MonoBehaviour
{
    // === SERIALIZED FIELDS ===
    [SerializeField] private SpriteLibraryAsset mouseLibrary;

    // === PUBLIC PROPERTIES ===
    public static SkinManager I { get; private set; }
    public SpriteLibraryAsset MouseLibrary => mouseLibrary;
    public string SelectedSkin { get; private set; } = "default";
    public bool SkinReady     { get; private set; } = false;

    // === PRIVATE FIELDS ===
    private PlayerStatsRepo repo;
    private PlayerStatsDTO stats;
    private List<string> ownedSkins = new List<string>();

    // === UNITY CALLBACKS ===
    private void Awake()
    {
        // Ensures a single persistent instance.
        Debug.Log("[SkinManager] Awake");
        if (I && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        PersistentObjectsManager.Register(gameObject);
    }

    private void Start()
    {
        // Begins loading skin data.
        Debug.Log("[SkinManager] Start");
        StartCoroutine(LoadSkinFromRepo());
    }

    // === DATA LOADING METHODS ===
    private IEnumerator LoadSkinFromRepo()
    {
        // Retrieves the player's current skin from the repository.
        yield return new WaitUntil(() => AuthManager.I != null);

        string uid   = AuthManager.I.UserId;
        string token = AuthManager.I.AccessToken;
        repo         = new PlayerStatsRepo(uid, new PlayerStatsDAO(AuthManager.I.Url, AuthManager.I.AnonKey));

        Debug.Log("[SkinManager] AuthManager ready");
        Debug.Log("[SkinManager] UID: " + uid);
        Debug.Log("[SkinManager] Token (short): " + token.Substring(0, 10));

        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            yield return repo.Sync(
                dto =>
                {
                    stats        = dto;
                    SelectedSkin = string.IsNullOrEmpty(stats.actual_skin) ? "default" : stats.actual_skin;
                    SkinReady    = true;
                    Debug.Log("[SkinManager] Skin loaded after sync: " + SelectedSkin);
                },
                err =>
                {
                    Debug.LogWarning("[SkinManager] Error syncing skin: " + err);
                    SkinReady = true; // Avoid blocking the wait
                });
        }
        else
        {
            yield return repo.Get(AuthManager.I.AccessToken,
                dto =>
                {
                    stats        = dto;
                    SelectedSkin = string.IsNullOrEmpty(stats.actual_skin) ? "default" : stats.actual_skin;
                    SkinReady    = true;
                    Debug.Log("[SkinManager] Offline skin loaded: " + SelectedSkin);
                },
                err =>
                {
                    Debug.LogWarning("[SkinManager] Error loading skin offline: " + err);
                    SkinReady = true;
                });
        }
    }

    // === SKIN MANAGEMENT METHODS ===
    public string[] GetAllSkins() =>
        mouseLibrary ? mouseLibrary.GetCategoryNames().ToArray() : new[] { "default" };

    public void SetSkin(string skin, bool saveToRepo = true)
    {
        // Sets the current skin and optionally saves it to the repo.
        if (string.IsNullOrEmpty(skin)) return;

        SelectedSkin = skin;
        Debug.Log("[SkinManager] Skin set: " + skin);

        if (stats == null || repo == null)
        {
            Debug.LogWarning("[SkinManager] No DTO/repo available to save skin");
            return;
        }

        if (saveToRepo)
        {
            stats.actual_skin = skin;
            stats.updated_at  = DateTime.UtcNow;

            StartCoroutine(repo.Save(stats,
                () => Debug.Log("[SkinManager] Skin saved to repo"),
                err => Debug.LogWarning("[SkinManager] Error saving skin: " + err)));
        }
    }

    public Sprite GetPreviewSprite(string category) =>
        mouseLibrary ? mouseLibrary.GetSprite(category, "fly") : null;

    public Sprite GetPreviewSpriteStore(string category) =>
        mouseLibrary ? mouseLibrary.GetSprite(category, "lock") : null;

    // === OWNED SKINS UTILITIES ===
    public void SetOwnedSkins(List<string> skins)
    {
        // Updates the list of unlocked skins.
        ownedSkins = new List<string>(skins);
        Debug.Log("[SkinManager] Unlocked skins loaded: " + string.Join(", ", ownedSkins));
    }

    public bool IsSkinUnlocked(string skinName) =>
        ownedSkins.Contains(skinName);

    public List<string> GetOwnedSkins() =>
        new List<string>(ownedSkins);
}