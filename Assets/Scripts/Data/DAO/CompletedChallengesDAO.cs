using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

public class CompletedChallengesDAO
{
    // === FIELDS ===
    readonly string supabaseUrl;
    readonly string anonKey;
    readonly string jwt;

    // === CONSTRUCTORS ===
    // Creates a DAO configured to communicate with the Supabase backend
    public CompletedChallengesDAO(string url, string anonKey, string jwt)
    {
        supabaseUrl = url.TrimEnd('/');
        this.anonKey = anonKey;
        this.jwt = jwt;
        Debug.Log($"[CompletedChallengesDAO] ▶ init · url={supabaseUrl}");
    }

    // === SELECT METHODS ===
    // Retrieves a set containing the IDs of challenges already completed by the user
    public IEnumerator GetCompleted(Action<HashSet<int>> ok,
                                    Action<string>       err)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/completed_challenges?select=challenge_id";

        var req = UnityWebRequest.Get(endpoint);
        req.SetRequestHeader("Authorization", "Bearer " + jwt);
        req.SetRequestHeader("apikey",        anonKey);
        req.SetRequestHeader("Accept",        "application/json");

        Debug.Log("[CompletedDAO] GET → " + endpoint);
        yield return req.SendWebRequest();
        Debug.Log($"[CompletedDAO] HTTP {req.responseCode}");

        if (req.result != UnityWebRequest.Result.Success)
        {
            err?.Invoke(req.error);
            yield break;
        }

        try
        {
            var raw  = req.downloadHandler.text;
            var list = JsonConvert.DeserializeObject<List<CompletedChallengeDTO>>(raw);
            var set  = new HashSet<int>();
            foreach (var c in list) set.Add(c.challenge_id);
            ok?.Invoke(set);
        }
        catch (Exception e)
        {
            err?.Invoke(e.Message);
        }
    }

    // === INSERT METHODS ===
    // Inserts a row marking the specified challenge as completed
    public IEnumerator InsertCompleted(int challengeId,
                                       string userId,
                                       Action ok,
                                       Action<string> err)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/completed_challenges";

        // JSON body to send (includes reward_claimed flag set to true)
        var body = new
        {
            user_id        = userId,
            challenge_id   = challengeId,
            reward_claimed = true
        };
        string json = JsonConvert.SerializeObject(body);

        // ---------- petición ----------
        var req = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();

        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Accept",       "application/json");
        req.SetRequestHeader("Authorization","Bearer " + jwt);
        req.SetRequestHeader("apikey",       anonKey);
        req.SetRequestHeader("Prefer",       "return=minimal");

        Debug.Log($"[CompletedDAO] INSERT challenge_id={challengeId}  body={json}");
        yield return req.SendWebRequest();
        Debug.Log($"[CompletedDAO] HTTP {req.responseCode}  msg={req.downloadHandler.text}");

        if (req.result == UnityWebRequest.Result.Success ||
            req.responseCode == 201 || req.responseCode == 204 || req.responseCode == 409)
        {
            ok?.Invoke(); // 409 means it was already inserted → idempotent success
        }
        else
        {
            err?.Invoke(req.downloadHandler.text);   // returns 4xx/5xx detail
        }
    }

    // === CACHE METHODS ===
    // Updates local JSON caches to reflect the newly completed challenge
    public static void AppendToLocalCache(string userId, int challengeId)
	{
		var dir = Path.Combine(Application.persistentDataPath, "offline_db");
		Directory.CreateDirectory(dir);

		// 1) Add to completed cache
		var compFile  = Path.Combine(dir, $"{userId}-completed_challenges.json");
		var compTable = new JsonTable<int>(compFile);
		compTable.Add(challengeId);
		compTable.Save(() => { }, _ => { });

		// 2) Remove from pending daily challenges cache
		var dailyFile = Path.Combine(dir, "daily_challenges.json");
		if (!File.Exists(dailyFile)) return;
		try
		{
			var list = JsonConvert.DeserializeObject<List<DailyChallengeDTO>>(File.ReadAllText(dailyFile));
			if (list.RemoveAll(d => d.id == challengeId) > 0)
				File.WriteAllText(dailyFile,
					JsonConvert.SerializeObject(list, Formatting.Indented));
			Debug.Log($"[CompletedDAO] 🚮 Eliminada misión {challengeId} de daily_challenges.json");
		}
		catch (Exception e)
		{
			Debug.LogWarning("[CompletedDAO] No se pudo limpiar daily_challenges.json: " + e.Message);
		}
	}
}