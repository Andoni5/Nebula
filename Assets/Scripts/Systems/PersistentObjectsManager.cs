using System.Collections.Generic;
using UnityEngine;

public static class PersistentObjectsManager
{
    // === PRIVATE FIELDS ===
    private static List<GameObject> persistentObjects = new();

    // === REGISTRATION METHODS ===
    // Marks the object as persistent and tracks it for later cleanup
    public static void Register(GameObject obj)
    {
        if (!persistentObjects.Contains(obj))
        {
            persistentObjects.Add(obj);
            Object.DontDestroyOnLoad(obj);
        }
    }

    // === MANAGEMENT METHODS ===
    // Destroys all registered persistent objects
    public static void DestroyAll()
    {
        Debug.Log("[PersistentObjectsManager] Destroying persistent objects...");

        foreach (var obj in persistentObjects)
        {
            if (obj != null)
            {
                Object.Destroy(obj);
                Debug.Log($"Destroyed: {obj.name}");
            }
        }

        persistentObjects.Clear();
    }
}