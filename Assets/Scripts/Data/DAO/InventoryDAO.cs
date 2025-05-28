using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System;

public class InventoryDAO
{
    readonly string supabaseUrl;
    readonly string anonKey;

    // === CONSTRUCTOR ===

    // Creates a new DAO instance for inventory operations.
    public InventoryDAO(string url, string key)
    {
        supabaseUrl = url;
        anonKey = key;
        Debug.Log($"[InventoryDAO] Creado. URL = {supabaseUrl}");
    }

    // === INVENTORY QUERY METHODS ===

    // Retrieves the full inventory list for the specified user.
    public IEnumerator GetInventory(string userId, string accessToken,
                                    Action<List<InventoryDTO>> onOk,
                                    Action<string> onErr)
    {
        Debug.Log($"[InventoryDAO.GetInventory] Inicio | userId = {userId} | token = {accessToken.Substring(0, 6)}***");

        string endpoint = $"{supabaseUrl}/rest/v1/inventory?user_id=eq.{userId}&select=*";
        Debug.Log($"[InventoryDAO.GetInventory] Endpoint construido = {endpoint}");

        var req = UnityWebRequest.Get(endpoint);
        req.SetRequestHeader("apikey", anonKey);
        req.SetRequestHeader("Authorization", "Bearer " + accessToken);
        req.timeout = 10;

        yield return req.SendWebRequest();
        Debug.Log($"[InventoryDAO.GetInventory] HTTP {req.responseCode} | result = {req.result}");

        if (req.result == UnityWebRequest.Result.Success)
        {
            var json = req.downloadHandler.text;
            Debug.Log($"[InventoryDAO.GetInventory] JSON recibido ({json.Length} bytes)");
            var list = JsonConvert.DeserializeObject<List<InventoryDTO>>(json);
            Debug.Log($"[InventoryDAO.GetInventory] Objetos deserializados = {list?.Count ?? 0}");
            onOk?.Invoke(list);
        }
        else
        {
            string msg = $"Inventario error HTTP {req.responseCode}: {req.error}";
            Debug.LogWarning($"[InventoryDAO.GetInventory] {msg}");
            onErr?.Invoke(msg);
        }
    }

    // Retrieves the timestamp of the last acquired item in the inventory.
    public IEnumerator GetLastInventoryTimestamp(string userId, string token,
                                                 Action<DateTime> onOk,
                                                 Action<string> onErr)
    {
        Debug.Log($"[InventoryDAO.GetLastInventoryTimestamp] Inicio | userId = {userId} | token = {token.Substring(0, 6)}***");

        string url = $"{supabaseUrl}/rest/v1/inventory?select=acquired_at&user_id=eq.{userId}&order=acquired_at.desc&limit=1";
        Debug.Log($"[InventoryDAO.GetLastInventoryTimestamp] URL = {url}");

        var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("apikey", anonKey);
        req.SetRequestHeader("Authorization", "Bearer " + token);
        req.SetRequestHeader("Accept", "application/json");

        yield return req.SendWebRequest();
        Debug.Log($"[InventoryDAO.GetLastInventoryTimestamp] HTTP {req.responseCode} | result = {req.result}");

        if (req.result != UnityWebRequest.Result.Success)
        {
            string msg = $"Error HTTP {req.responseCode}: {req.error}";
            Debug.LogWarning($"[InventoryDAO.GetLastInventoryTimestamp] {msg}");
            onErr?.Invoke(msg);
            yield break;
        }

        try
        {
            var raw = req.downloadHandler.text;
            Debug.Log($"[InventoryDAO.GetLastInventoryTimestamp] JSON crudo = {raw}");

            if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith("["))
            {
                onErr?.Invoke("Respuesta inesperada del servidor");
                yield break;
            }

            var arr = JsonConvert.DeserializeObject<InventoryDTO[]>(raw);
            Debug.Log($"[InventoryDAO.GetLastInventoryTimestamp] Elementos en array = {arr.Length}");

            if (arr.Length > 0)
                onOk(arr[0].acquired_at);
            else
                onOk(DateTime.MinValue);
        }
        catch (Exception e)
        {
            string msg = "Error parseando JSON de timestamp: " + e.Message;
            Debug.LogError($"[InventoryDAO.GetLastInventoryTimestamp] {msg}");
            onErr?.Invoke(msg);
        }
    }

    // === INVENTORY UPLOAD METHODS ===

    // Uploads a new inventory item to the server for the given user.
    public IEnumerator UploadItem(InventoryDTO item, string token,
                                  Action onOk,
                                  Action<string> onErr)
    {
        Debug.Log($"[InventoryDAO.UploadItem] Inicio | userId = {item.user_id} | item = {item.item_name}");

        string endpoint = $"{supabaseUrl}/rest/v1/inventory";
        string json = JsonConvert.SerializeObject(new
        {
            user_id     = item.user_id,
            item_name   = item.item_name,
            acquired_at = item.acquired_at.ToUniversalTime().ToString("o")
        });

        Debug.Log($"[InventoryDAO.UploadItem] Payload = {json}");

        var req = new UnityWebRequest(endpoint, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json)),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout         = 10
        };

        req.SetRequestHeader("apikey", anonKey);
        req.SetRequestHeader("Authorization", "Bearer " + token);
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();
        Debug.Log($"[InventoryDAO.UploadItem] HTTP {req.responseCode} | result = {req.result}");

        if (req.result == UnityWebRequest.Result.Success || req.responseCode == 201)
        {
            Debug.Log($"[InventoryDAO.UploadItem] ✔️ Subida correcta");
            onOk?.Invoke();
        }
        else
        {
            string msg = $"Error HTTP {req.responseCode}: {req.error}";
            Debug.LogWarning($"[InventoryDAO.UploadItem] {msg}");
            onErr?.Invoke(msg);
        }
    }

    // === HELPER DTO ===

    // Lightweight DTO used to deserialize the acquired_at field only.
    [Serializable]
    private class AcquiredAtDTO
    {
        public string acquired_at;
    }
}