using UnityEngine;

public class LoginCanvasHider : MonoBehaviour
{
    // === INITIALIZATION METHODS ===
    // Hides the login canvas after a successful login
    void Start()
    {
        GameObject loginCanvas = GameObject.Find("Canvas");

        if (loginCanvas != null)
        {
            // Hide only if the canvas still has the LoginUITest component
            if (loginCanvas.GetComponent<LoginUITest>())
            {
                loginCanvas.SetActive(false);
                Debug.Log("Canvas of login hidden after login.");
            }
        }
    }
}