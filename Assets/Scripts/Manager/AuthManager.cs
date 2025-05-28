using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

public class AuthManager : MonoBehaviour
{
    // === SERIALIZED FIELDS ===
    [Header("Supabase Config (set in Inspector)")]
    [SerializeField] string supabaseUrl;
    [SerializeField] string anonKey;

    // === PUBLIC PROPERTIES ===
    public static AuthManager I { get; private set; }
    public string Url     => supabaseUrl;
    public string AnonKey => anonKey;
    public string AccessToken => accessToken;

    // === PRIVATE FIELDS ===
    AuthDAO auth;
    string  accessToken;
    string  refreshToken;

    // === UNITY LIFECYCLE METHODS ===
    // Restores saved tokens and registers the singleton instance
    void Awake()
    {
        if (I && I != this) { Destroy(gameObject); return; }
        I = this;

        PersistentObjectsManager.Register(gameObject);
        PersistentObjectsManager.Register(gameObject); // Required for persistence

        auth = new AuthDAO(supabaseUrl, anonKey);

        accessToken  = PlayerPrefs.GetString("access_token", "");
        refreshToken = PlayerPrefs.GetString("refresh_token", "");
    }

    // === AUTHENTICATION METHODS ===
    // Performs email/password login and returns the new access token
    public void Login(string email, string pass,
                      System.Action<string> onOk, System.Action<string> onErr)
    {
        StartCoroutine(auth.Login(email, pass, dto =>
        {
            StoreTokens(dto.access_token, dto.refresh_token);
            onOk?.Invoke(dto.access_token);
        }, onErr));
    }

    // Ensures a valid access token, refreshing it if necessary
    public void EnsureToken(System.Action<string> withToken, System.Action<string> onErr)
    {
        if (!string.IsNullOrEmpty(accessToken))
        {
            withToken(accessToken);
            return;
        }

        if (string.IsNullOrEmpty(refreshToken))
        {
            onErr?.Invoke("No session");
            return;
        }

        StartCoroutine(auth.Refresh(refreshToken, dto =>
        {
            StoreTokens(dto.access_token, dto.refresh_token);
            withToken(accessToken);
        }, onErr));
    }

    // Registers a new user and returns the issued access token
    public void Register(string email, string pass,
                         System.Action<string> onOk, System.Action<string> onErr)
    {
        StartCoroutine(auth.Register(email, pass, dto =>
        {
            StoreTokens(dto.access_token, dto.refresh_token);
            onOk?.Invoke(dto.access_token);
        }, onErr));
    }

    // === TOKEN UTILITIES ===
    // Saves tokens both in memory and PlayerPrefs
    void StoreTokens(string acc, string refh)
    {
        accessToken  = acc;
        refreshToken = refh;
        PlayerPrefs.SetString("access_token", acc);
        PlayerPrefs.SetString("refresh_token", refh);
    }

    // Extracts the Supabase user ID from the JWT access token
    public string UserId
    {
        get
        {
            try
            {
                var payload = accessToken.Split('.')[1];
                var json = System.Text.Encoding.UTF8.GetString(
                    System.Convert.FromBase64String(PadBase64(payload)));
                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                return data["sub"].ToString();
            }
            catch { return ""; }
        }
    }

    // Adds missing Base64 padding to a JWT segment
    string PadBase64(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "=";  break;
        }
        return base64.Replace('-', '+').Replace('_', '/');
    }
}