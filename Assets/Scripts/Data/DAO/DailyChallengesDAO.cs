using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class DailyChallengesDAO
{
    // === CONSTRUCTOR ===

    // Initializes the DAO with the base URL and anonymous key.
    public DailyChallengesDAO(string url, string anonKey)
    {
        baseUrl = url.TrimEnd('/');
        this.anonKey = anonKey;
        Debug.Log($"[DailyChallengesDAO] ▶ init · url={baseUrl}");
    }

    // === NETWORK METHODS ===

    // Retrieves all challenges dated today or earlier (UTC) from the server.
    public IEnumerator GetActiveChallenges(
        Action<List<DailyChallengeDTO>> onOk,
        Action<string>                  onErr)
    {
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        string endpoint =
            $"{baseUrl}/rest/v1/daily_challenges?select=*&challenge_date=lte.{today}";

        Debug.Log($"[DAO] ----- GetActiveChallenges START (≤ {today}) -----");
        Debug.Log($"[DAO] Endpoint={endpoint}");

        var req = UnityWebRequest.Get(endpoint);
        req.SetRequestHeader("apikey", anonKey);
        req.SetRequestHeader("Accept", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            string err = $"HTTP {req.responseCode} – {req.error}";
            Debug.LogWarning("[DAO] ❌ " + err);
            onErr?.Invoke(err);
            yield break;
        }

        string json = req.downloadHandler.text;
        Debug.Log("[DAO] JSON: " + json);

        try
        {
            var list = JsonUtilityWrapper.FromJsonArray<DailyChallengeDTO>(json);
            onOk?.Invoke(list ?? new List<DailyChallengeDTO>());
        }
        catch (Exception e)
        {
            onErr?.Invoke("Parse error: " + e.Message);
        }

        Debug.Log("[DAO] ----- GetActiveChallenges END -----");
    }

    // === UTILITY METHODS ===

    // Wrapper for JsonUtility to handle root-level JSON arrays.
    static class JsonUtilityWrapper
    {
        [Serializable]
        class Wrapper<T> { public T[] Items; }

        // Converts a JSON array (root level) into a list of objects.
        public static List<T> FromJsonArray<T>(string json)
        {
            string wrapped = "{\"Items\":" + json + "}";
            return new List<T>(JsonUtility.FromJson<Wrapper<T>>(wrapped).Items);
        }
    }

    // === PRIVATE FIELDS ===

    readonly string baseUrl;
    readonly string anonKey;
}