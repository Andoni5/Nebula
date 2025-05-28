using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System;

public class CosmeticsDAO
{
    string supabaseUrl;
    string anonKey;

    // === CONSTRUCTOR ===

    // Creates a new DAO configured with the Supabase endpoint and anonymous key.
    public CosmeticsDAO(string url, string key)
    {
        supabaseUrl = url;
        anonKey = key;
    }

    // === COSMETIC QUERY METHODS ===

    // Retrieves a single cosmetic item by its name.
    public IEnumerator GetCosmetic(string itemName,
                                   System.Action<CosmeticItemDTO> onOk,
                                   System.Action<string> onErr)
    {
        string endpoint = $"{supabaseUrl}/rest/v1/cosmetic_items?name=eq.{itemName}&select=*";

        var req = UnityWebRequest.Get(endpoint);
        req.SetRequestHeader("apikey", anonKey);
        req.SetRequestHeader("Authorization", "Bearer " + AuthManager.I.AccessToken);
        req.timeout = 10;

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var json = req.downloadHandler.text;
            var list = JsonConvert.DeserializeObject<List<CosmeticItemDTO>>(json);
            if (list != null && list.Count > 0)
                onOk?.Invoke(list[0]);
            else
                onErr?.Invoke("No se encontró el item.");
        }
        else
        {
            onErr?.Invoke($"Error HTTP {req.responseCode}: {req.error}");
        }
    }

    // Retrieves every cosmetic item available in the table.
    public IEnumerator GetAllCosmetics(Action<List<CosmeticItemDTO>> onOk,
                                       Action<string> onErr)
    {
        string url = $"{supabaseUrl}/rest/v1/cosmetic_items?select=*";

        var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("apikey", anonKey);
        req.SetRequestHeader("Authorization", "Bearer " + AuthManager.I.AccessToken);
        req.timeout = 10;

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var json = req.downloadHandler.text;
            var list = JsonConvert.DeserializeObject<List<CosmeticItemDTO>>(json);
            onOk?.Invoke(list);
        }
        else
        {
            onErr?.Invoke($"Error HTTP {req.responseCode}: {req.error}");
        }
    }
}