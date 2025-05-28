using System;
using System.Collections;
using UnityEngine;

public class PlayerStatsRepo
{
    // === PRIVATE FIELDS ===
    readonly PlayerStatsDAO remote;
    readonly JsonTable<PlayerStatsDTO> local;

    // === CONSTRUCTOR ===

    // Initializes the repository with a UID-scoped JSON cache and a remote DAO.
    public PlayerStatsRepo(string uid, PlayerStatsDAO dao)
    {
        remote = dao;
        local = new JsonTable<PlayerStatsDTO>($"{uid}-player_stats.json");
    }

    // === GET METHODS ===

    // Returns the cached player stats from the local JSON file.
    public IEnumerator Get(string token, Action<PlayerStatsDTO> ok, Action<string> err)
    {
        yield return local.Load(() =>
        {
            if (local.All.Count > 0)
                ok(local.All[0]);
            else
                err?.Invoke("No hay datos en caché");
        }, err);
    }

    // === SAVE METHODS ===

    // Saves stats locally and optionally pushes them to the remote server.
    public IEnumerator Save(PlayerStatsDTO dto,
                            Action ok,
                            Action<string> err,
                            bool pushRemote     = true,
                            bool touchTimestamp = true)
    {
        if (touchTimestamp)
            dto.updated_at = DateTime.UtcNow;

        local.ReplaceAll(new[] { dto });
        yield return local.Save(() => { }, err);

        bool online = Application.internetReachability != NetworkReachability.NotReachable;
        if (pushRemote && online)
            yield return remote.UpsertStats(dto, ok, err);
        else
            ok?.Invoke();
    }

    // === SYNC METHODS ===

    // Synchronizes local stats with the remote server, resolving any conflicts.
    public IEnumerator Sync(Action<PlayerStatsDTO> ok, Action<string> err)
    {
        Debug.Log("[PlayerStatsRepo] Sync iniciado");

        yield return local.Load(
            () => Debug.Log("[Sync] JSON local cargado"),
            e  => Debug.LogWarning("[Sync] Error al cargar JSON: " + e)
        );

        // Read from cache even if Save() has never been called.
        PlayerStatsDTO localDTO = local.FirstOrDefault;

        if (localDTO == null)
        {
            err?.Invoke("❌ No hay datos locales para sincronizar.");
            yield break;
        }

        PlayerStatsDTO remoteDTO = null;
        bool remoteFailed = false;

        yield return remote.GetStats(AuthManager.I.AccessToken,
            dto => remoteDTO = dto,
            e =>
            {
                Debug.LogWarning("[Sync] Error al obtener datos remotos: " + e);
                remoteFailed = true;
            });

        if (remoteFailed)
        {
            Debug.LogWarning("⚠️ No se pudieron obtener stats remotos. Subiendo los locales...");
            yield return remote.SaveStats(localDTO,
                () =>
                {
                    Debug.Log("[Sync] Subida completada ✅");
                    ok?.Invoke(localDTO);
                },
                err);
            yield break;
        }

        Debug.Log($"[Sync] Comparando datos...\n🗃️ Local: {JsonUtility.ToJson(localDTO)}\n☁️ Remote: {JsonUtility.ToJson(remoteDTO)}");

        bool valuesDiffer  = AreStatsDifferent(localDTO, remoteDTO);
        bool localIsNewer  = localDTO.updated_at  > remoteDTO.updated_at;
        bool remoteIsNewer = remoteDTO.updated_at > localDTO.updated_at;

        if (!valuesDiffer)
        {
            Debug.Log("✅ No hay diferencias en los valores. No se hace PATCH.");
            ok?.Invoke(localDTO);
            yield break;
        }

        if (localIsNewer)
        {
            Debug.Log("🡆 Datos locales más recientes → PATCH a Supabase");
            yield return remote.SaveStats(localDTO,
                () =>
                {
                    Debug.Log("[Sync] Subida completada ✅");
                    ok?.Invoke(localDTO);
                },
                err);
        }
        else if (remoteIsNewer)
        {
            Debug.Log("🡄 Datos remotos más recientes → actualizando JSON local");
            local.ReplaceAll(new[] { remoteDTO });
            yield return local.Save(() =>
            {
                Debug.Log("[Sync] Local actualizado ✅");
                ok?.Invoke(remoteDTO);
            }, err);
        }
        else
        {
            Debug.Log("🟠 Timestamps iguales pero valores distintos → asumo local como fuente de verdad");
            yield return remote.SaveStats(localDTO,
                () =>
                {
                    Debug.Log("[Sync] Subida completada ✅");
                    ok?.Invoke(localDTO);
                },
                err);
        }
    }

    // === HELPER METHODS ===

    // Checks whether two stats DTOs differ in any relevant field.
    private bool AreStatsDifferent(PlayerStatsDTO a, PlayerStatsDTO b)
    {
        return a.best_distance          != b.best_distance ||
               a.best_coins_earned      != b.best_coins_earned ||
               a.total_sessions         != b.total_sessions ||
               a.total_distance         != b.total_distance ||
               a.total_coins_collected  != b.total_coins_collected ||
               a.total_coins_spent      != b.total_coins_spent ||
               a.challenges_completed   != b.challenges_completed ||
               a.actual_skin            != b.actual_skin;
    }
}