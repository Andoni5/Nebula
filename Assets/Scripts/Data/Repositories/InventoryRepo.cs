using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;

public class InventoryRepo
{
    // === FIELDS ===
    readonly InventoryDAO remote;
    readonly JsonTable<InventoryDTO> local;

    // === CONSTRUCTOR ===
    // Initializes the repository with local storage and the remote DAO reference.
    public InventoryRepo(string userId, InventoryDAO dao)
    {
        remote = dao;

        // Path where the offline inventory will be saved
        string dbPath = Path.Combine(Application.persistentDataPath, "offline_db", $"{userId}-inventory.json");
        local = new JsonTable<InventoryDTO>(dbPath);

        Debug.Log($"[InventoryRepo] Instanciado | Archivo local = {dbPath}");
    }

    // === SYNCHRONIZATION METHODS ===
    // Synchronizes local inventory data with the remote server.
    public IEnumerator Sync(string token, string userId,
                            Action<List<InventoryDTO>> onOk,
                            Action<string> onErr)
    {
        Debug.Log("[InventoryRepo.Sync] ---- INICIO ----");

        // Load current local data from disk
        yield return local.Load(
            () => Debug.Log("[InventoryRepo.Sync] ✔️ Local cargado"),
            err => Debug.LogWarning("[InventoryRepo.Sync] ❌ Error cargando local: " + err)
        );

        DateTime localTime = local.All.Count > 0
                           ? local.All.Max(i => i.acquired_at)
                           : DateTime.MinValue;

        // Request the last server timestamp
        DateTime remoteTime = DateTime.MinValue;
        string daoErr = null;

        yield return remote.GetLastInventoryTimestamp(userId, token,
            ts =>
            {
                remoteTime = ts;
                Debug.Log($"[InventoryRepo.Sync] 🕒 Timestamp remoto = {remoteTime:u}");
            },
            err =>
            {
                daoErr = err;
                Debug.LogWarning($"[InventoryRepo.Sync] ❌ Error timestamp remoto: {err}");
            });

        if (!string.IsNullOrEmpty(daoErr))
        {
            onErr?.Invoke(daoErr);
            yield break;
        }

        Debug.Log($"[InventoryRepo.Sync] Comparación de timestamps | Local = {localTime:u} | Remoto = {remoteTime:u}");

        // Decide the sync direction
        if (localTime > remoteTime)
        {
            // Upload newer local items
            List<InventoryDTO> toUpload = local.All
                                              .Where(i => i.acquired_at > remoteTime)
                                              .ToList();

            Debug.Log($"[InventoryRepo.Sync] Ítems a sincronizar con el servidor: {toUpload.Count}");

            foreach (var item in toUpload)
            {
                yield return remote.UploadItem(item, token,
                    () => Debug.Log($"[InventoryRepo.Sync] ✔️ Enviado {item.item_name}"),
                    err => Debug.LogWarning($"[InventoryRepo.Sync] ❌ Error al enviar {item.item_name}: {err}")
                );
            }

            Debug.Log("[InventoryRepo.Sync] Subida local → remoto completada ✅");
            onOk?.Invoke(local.All.ToList());
        }
        else if (remoteTime > localTime)
        {
            // Download newer remote data
            Debug.Log("[InventoryRepo.Sync] Detected newer remote data → Descargando del servidor");

            bool enableLocalWrite = false; // Toggle for local persistence

            yield return remote.GetInventory(userId, token,
                list =>
                {
                    Debug.Log($"[InventoryRepo.Sync] 🛬 Datos remotos recibidos ({list.Count} ítems)");

                    if (!enableLocalWrite)
                    {
                        Debug.Log("[InventoryRepo.Sync] Escritura local no requerida, operando solo en memoria");
                        onOk?.Invoke(list);
                        return;
                    }

                    // Persist data locally
                    local.ReplaceAll(list);
                    local.Save(
                        () =>
                        {
                            Debug.Log("[InventoryRepo.Sync] Datos escritos en almacenamiento local ✔️");
                            onOk?.Invoke(list);
                        },
                        err =>
                        {
                            Debug.LogWarning("[InventoryRepo.Sync] ❌ Fallo al guardar datos localmente: " + err);
                            onErr?.Invoke(err);
                        });
                },
                err =>
                {
                    Debug.LogWarning("[InventoryRepo.Sync] ❌ Error al obtener inventario remoto: " + err);
                    onErr?.Invoke(err);
                });
        }
        else
        {
            Debug.Log("[InventoryRepo.Sync] Sincronización no requerida. Datos locales y remotos están alineados.");
            onOk?.Invoke(local.All.ToList());
        }
    }

    // === INVENTORY RETRIEVAL METHODS ===
    // Retrieves local inventory or syncs first based on connectivity.
    public IEnumerator GetLocalOrSync(string token, string userId,
                                      Action<List<InventoryDTO>> onOk,
                                      Action<string> onErr)
    {
        Debug.Log($"[InventoryRepo.GetLocalOrSync] Inicio | Conectividad = {Application.internetReachability}");

        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            Debug.Log("[InventoryRepo.GetLocalOrSync] 🌐 Online: intentando sincronizar");
            yield return Sync(token, userId, onOk, onErr);
        }
        else
        {
            Debug.Log("[InventoryRepo.GetLocalOrSync] 📴 Offline: cargando desde disco");
            string loadErr = null;

            yield return local.Load(
                () => Debug.Log("[InventoryRepo.GetLocalOrSync] ✔️ Local cargado"),
                err => loadErr = err
            );

            if (!string.IsNullOrEmpty(loadErr))
            {
                Debug.LogWarning("[InventoryRepo.GetLocalOrSync] ❌ Error leyendo local: " + loadErr);
                onErr?.Invoke(loadErr);
                yield break;
            }

            Debug.Log($"[InventoryRepo.GetLocalOrSync] Devolviendo {local.All.Count} ítems locales");
            onOk?.Invoke(local.All.ToList());
        }
    }

    // === MERGE METHODS ===
    // Merges local and remote inventories to ensure both contain all items.
    public IEnumerator MergeInventoryIfNeeded(string token, string userId,
                                              Action<List<InventoryDTO>> onSuccess,
                                              Action<string> onError)
    {
        Debug.Log("[InventoryRepo.MergeInventoryIfNeeded] Iniciando comparación remota y local...");

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogWarning("[InventoryRepo.MergeInventoryIfNeeded] 🚫 Sin internet, se omite comparación.");
            yield break;
        }

        // Load local data
        yield return local.Load(
            () => Debug.Log("[InventoryRepo.MergeInventoryIfNeeded] ✔️ Local cargado"),
            err => Debug.LogWarning("[InventoryRepo.MergeInventoryIfNeeded] ❌ Error cargando local: " + err)
        );

        List<InventoryDTO> localItems = local.All.ToList();
        HashSet<string> localNames = new HashSet<string>(localItems.Select(i => i.item_name));

        // Get remote inventory
        List<InventoryDTO> remoteItems = null;
        string remoteErr = null;

        yield return remote.GetInventory(userId, token,
            list =>
            {
                remoteItems = list;
                Debug.Log($"[InventoryRepo.MergeInventoryIfNeeded] 🛬 Remoto cargado ({remoteItems.Count} ítems)");
            },
            err =>
            {
                Debug.LogWarning($"[InventoryRepo.MergeInventoryIfNeeded] ❌ Error cargando remoto: {err}");
                remoteErr = err;
            });

        if (!string.IsNullOrEmpty(remoteErr))
        {
            onError?.Invoke(remoteErr);
            yield break;
        }

        HashSet<string> remoteNames = new HashSet<string>(remoteItems.Select(i => i.item_name));

        // Detect differences
        var onlyInLocal = localItems.Where(i => !remoteNames.Contains(i.item_name)).ToList();
        var onlyInRemote = remoteItems.Where(i => !localNames.Contains(i.item_name)).ToList();

        Debug.Log($"[InventoryRepo.MergeInventoryIfNeeded] 🧩 Sólo en local: {onlyInLocal.Count} | Sólo en remoto: {onlyInRemote.Count}");

        // Upload items only in local
        foreach (var item in onlyInLocal)
        {
            yield return remote.UploadItem(item, token,
                () => Debug.Log($"[InventoryRepo.MergeInventoryIfNeeded] ⬆ Subido al servidor: {item.item_name}"),
                err => Debug.LogWarning($"[InventoryRepo.MergeInventoryIfNeeded] ❌ Error subiendo {item.item_name}: {err}")
            );
        }

        // Add missing remote items to local and save
        if (onlyInRemote.Count > 0)
        {
            foreach (var item in onlyInRemote)
            {
                local.Add(item);
            }

            yield return local.Save(
                () => Debug.Log("[InventoryRepo.MergeInventoryIfNeeded] ✔️ Inventario local actualizado con datos remotos"),
                err => Debug.LogWarning("[InventoryRepo.MergeInventoryIfNeeded] ❌ Error guardando inventario local: " + err)
            );
        }

        // Return unified inventory
        List<InventoryDTO> merged = local.All.Union(onlyInRemote).ToList();
        onSuccess?.Invoke(merged);
    }
}