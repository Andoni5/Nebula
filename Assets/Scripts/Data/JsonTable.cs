using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

public class JsonTable<T>
{
    // === PRIVATE FIELDS ===
    readonly string path;
    List<T> cache = new(); // Holds the in-memory data
    bool dirty = false;

    // === CONSTRUCTOR ===
    // Builds the table referencing a JSON file under the persistent data path
    public JsonTable(string fileName)
    {
        path = Path.Combine(Application.persistentDataPath, "offline_db", fileName);
    }

    // === LOAD / SAVE METHODS ===
    // Loads the table contents from disk asynchronously
    public IEnumerator Load(Action ok, Action<string> err)
    {
        if (!File.Exists(path)) { err?.Invoke("Missing file: " + path); yield break; }

        try
        {
            cache = JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(path));
            Debug.Log("[JsonTable] Loaded from disk: " + File.ReadAllText(path));
            ok?.Invoke();
        }
        catch (Exception e) { err?.Invoke(e.Message); }
    }

    // Returns all rows currently in memory
    public IReadOnlyList<T> All => cache;

    // === COLLECTION MUTATORS ===
    // Replaces all rows with the provided collection
    public void ReplaceAll(IEnumerable<T> rows) { cache = new List<T>(rows); dirty = true; }

    // Adds a single row to the collection
    public void Add(T row) { cache.Add(row); dirty = true; }

    // Saves the table to disk only if it has been modified
    public IEnumerator Save(Action ok, Action<string> err)
    {
        if (!dirty) { ok?.Invoke(); yield break; }

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string tmp = path + ".tmp";

        try
        {
            File.WriteAllText(tmp, JsonConvert.SerializeObject(cache, Formatting.Indented));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);     // Works on all Unity targets
            dirty = false;
            ok?.Invoke();
        }
        catch (Exception e) { err?.Invoke(e.Message); }
    }

    // Adds the row only if an identical one is not already present
    public void ReplaceAllIfNeeded(T row)
    {
        if (!cache.Exists(x => JsonConvert.SerializeObject(x) == JsonConvert.SerializeObject(row)))
        {
            cache.Add(row);
            dirty = true;
        }
    }

    // Returns the first row or default if empty
    public T FirstOrDefault => cache.Count > 0 ? cache[0] : default;

    // Clears the table contents in memory
    public void Clear()
    {
        cache.Clear();
        dirty = true;
    }
}