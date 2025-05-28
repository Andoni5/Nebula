using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class OfflineLoginHandler : MonoBehaviour
{
    // === SERIALIZED FIELDS ===
    public Text feedbackLabel;
    [SerializeField] private string gameSceneName = "menutest1";

    // === SINGLETON INSTANCE ===
    public static OfflineLoginHandler Instance { get; private set; }

    // === UNITY LIFECYCLE METHODS ===
    // Ensures a single persistent instance across scenes
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        PersistentObjectsManager.Register(gameObject);
    }

    // === OFFLINE MODE METHODS ===
    // Attempts to restore a saved session and load the game scene in offline mode
    public void EnterOfflineMode()
    {
        if (feedbackLabel != null)
            feedbackLabel.text = "📴 OFFLINE: attempting to restore local session…";

        AuthManager.I.EnsureToken(
            token =>
            {
                var dao  = new PlayerStatsDAO(AuthManager.I.Url, AuthManager.I.AnonKey);
                var repo = new PlayerStatsRepo(AuthManager.I.UserId, dao);

                StartCoroutine(repo.Get(token,
                    dto =>
                    {
                        Debug.Log("✅ Offline login successful");

                        // Save minimal data for future automatic login
                        PlayerPrefs.SetString("actual_skin", dto.actual_skin);
                        PlayerPrefs.SetInt("total_coins",
                            (int)(dto.total_coins_collected - dto.total_coins_spent));
                        PlayerPrefs.Save();

                        SceneManager.LoadScene(gameSceneName);
                    },
                    err =>
                    {
                        if (feedbackLabel != null)
                            feedbackLabel.text = "❌ Offline failure: " + err;
                    }));
            },
            err =>
            {
                if (feedbackLabel != null)
                    feedbackLabel.text = "❌ Error retrieving offline token";
            });
    }
}