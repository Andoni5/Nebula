using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Collections.Generic;

public class PlayerStatsDAO
{
    readonly string supabaseUrl;
    readonly string anonKey;

    // === CONSTRUCTOR ===

    // Creates a DAO for reading and writing player_stats.
    public PlayerStatsDAO(string url, string anonKey)
    {
        supabaseUrl = url.TrimEnd('/');
        this.anonKey = anonKey;
        Debug.Log($"[DAO] ▶ PlayerStatsDAO init · url={supabaseUrl}");
    }

    // === STATS RETRIEVAL METHODS ===

    // Downloads the stats row for the authenticated user.
    public IEnumerator GetStats(string accessToken, Action<PlayerStatsDTO> onOk, Action<string> onError)
    {
        Debug.Log("[DAO] ----- GetStats START -----");

        if (string.IsNullOrEmpty(accessToken))
        {
            onError?.Invoke("Token vacío");
            yield break;
        }

        string endpoint = $"{supabaseUrl}/rest/v1/player_stats?select=*&limit=1";
        Debug.Log($"[DAO] Endpoint={endpoint}");

        var req = UnityWebRequest.Get(endpoint);
        req.SetRequestHeader("apikey", anonKey);
        req.SetRequestHeader("Authorization", "Bearer " + accessToken.Trim());
        req.timeout = 10;

        Debug.Log("[DAO] Enviando request...");
        yield return req.SendWebRequest();
        Debug.Log($"[DAO] ➡ HTTP {req.responseCode}");

        // 401 → token expired or lacks policy
        if (req.responseCode == 401)
        {
            onError?.Invoke("Token expirado o sin permisos (401)");
            yield break;
        }

        if (req.result == UnityWebRequest.Result.Success)
        {
            string json = req.downloadHandler.text;
            Debug.Log($"[DAO] JSON: {json}");

            var arr = JsonConvert.DeserializeObject<PlayerStatsDTO[]>(json);
            if (arr.Length > 0) onOk?.Invoke(arr[0]);
            else                onOk?.Invoke(null);
        }
        else
        {
            onError?.Invoke($"Error HTTP {req.responseCode}: {req.error}");
        }

        Debug.Log("[DAO] ----- GetStats END -----");
    }

    // === STATS SAVE METHODS ===

    // Sends a PATCH request updating all mutable statistic fields.
    public IEnumerator SaveStats(PlayerStatsDTO dto, Action onOk, Action<string> onErr)
    {
        // PATCH all counters and records (excluding immutable fields)
        string endpoint = $"{supabaseUrl}/rest/v1/player_stats?user_id=eq.{dto.user_id}";

        var body = new Dictionary<string, object>
        {
            ["best_distance"]         = dto.best_distance,
            ["best_coins_earned"]     = dto.best_coins_earned,
            ["total_sessions"]        = dto.total_sessions,
            ["total_distance"]        = dto.total_distance,
            ["total_coins_collected"] = dto.total_coins_collected,
            ["total_coins_spent"]     = dto.total_coins_spent,
            ["challenges_completed"]  = dto.challenges_completed,
            ["actual_skin"]           = dto.actual_skin
        };

        string json = JsonConvert.SerializeObject(body);

        var req = new UnityWebRequest(endpoint, "PATCH")
        {
            uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout         = 10
        };

        req.SetRequestHeader("apikey", anonKey);
        req.SetRequestHeader("Authorization", "Bearer " + AuthManager.I.AccessToken);
        req.SetRequestHeader("Content-Type", "application/json");
        // With "return=minimal" Supabase returns 204 with no body so we don't need to parse.
        req.SetRequestHeader("Prefer", "return=minimal");

        Debug.Log($"[SaveStats] PATCH to {endpoint} · body={json}");
        yield return req.SendWebRequest();
        Debug.Log($"[SaveStats] Response status={req.responseCode}");

        if (req.result != UnityWebRequest.Result.Success)
        {
            onErr?.Invoke($"Error HTTP {req.responseCode}: {req.error}");
            yield break;
        }

        onOk?.Invoke();
    }

    // Inserts or updates the stats row (upsert) depending on existence.
    public IEnumerator UpsertStats(PlayerStatsDTO dto,
                                   Action onOk,
                                   Action<string> onErr)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/player_stats";

        var req = new UnityWebRequest(endpoint, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(
                                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(dto))),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 10
        };
        req.SetRequestHeader("apikey",     anonKey);
        req.SetRequestHeader("Authorization", "Bearer " + AuthManager.I.AccessToken);
        req.SetRequestHeader("Content-Type", "application/json");
        // ► Creates or updates the row (upsert)
        req.SetRequestHeader("Prefer", "resolution=merge-duplicates,return=minimal");

        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
            onErr?.Invoke($"HTTP {req.responseCode}: {req.error}");
        else
            onOk?.Invoke();
    }

    // === UTILITY METHODS ===

    // Retrieves the server-side updated_at timestamp for the authenticated user.
    public IEnumerator GetServerTimestamp(string token, Action<DateTime> ok, Action<string> err)
    {
        string url = $"{supabaseUrl}/rest/v1/player_stats?select=updated_at&user_id=eq.{AuthManager.I.UserId}&limit=1";
        UnityWebRequest req = UnityWebRequest.Get(url);
        req.SetRequestHeader("apikey", anonKey);
        req.SetRequestHeader("Authorization", "Bearer " + token);
        req.SetRequestHeader("Accept", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            err?.Invoke($"Error HTTP {req.responseCode}: {req.error}");
            yield break;
        }

        try
        {
            string raw = req.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith("["))
            {
                err?.Invoke("Formato de respuesta inesperado");
                yield break;
            }

            string isoTime = JsonUtility.FromJson<UpdatedAtDTO>(WrapSingleObject(raw)).updated_at;
            ok?.Invoke(DateTime.Parse(isoTime));
        }
        catch (Exception e)
        {
            err?.Invoke($"Excepción al parsear fecha: {e.Message}");
        }
    }

    // Converts a single-element JSON array ( [{...}] ) into a JSON object ( {...} ).
    private string WrapSingleObject(string json)
    {
        return json.Trim('[', ']');
    }

    // === HELPER DTO ===

    // DTO used only to deserialize the updated_at field.
    [Serializable]
    private struct UpdatedAtDTO { public string updated_at; }
}