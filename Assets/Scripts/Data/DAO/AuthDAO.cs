using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Text;

public class AuthDAO
{
    readonly string url, anon;

    // === CONSTRUCTOR ===

    // Stores the Supabase URL and anonymous key used for all requests.
    public AuthDAO(string supabaseUrl, string anonKey)
    {
        url  = supabaseUrl;
        anon = anonKey;
    }

    // === AUTH METHODS ===

    // Performs a password-grant login returning a LoginDTO on success.
    public IEnumerator Login(
        string email, string pass,
        System.Action<LoginDTO> onOk,
        System.Action<string>   onErr)
    {
        string endpoint = $"{url}/auth/v1/token?grant_type=password";
        var body = new { email, password = pass };

        var req = new UnityWebRequest(endpoint, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body))),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 10
        };
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("apikey", anon);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var dto = JsonConvert.DeserializeObject<LoginDTO>(req.downloadHandler.text);
            onOk?.Invoke(dto);
        }
        else
            onErr?.Invoke(req.downloadHandler.text);
    }

    // Refreshes an access token using the provided refresh token.
    public IEnumerator Refresh(
        string refresh,
        System.Action<LoginDTO> onOk,
        System.Action<string>   onErr)
    {
        string endpoint = $"{url}/auth/v1/token?grant_type=refresh_token";
        var body = new { refresh_token = refresh };

        var req = new UnityWebRequest(endpoint, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body))),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 10
        };
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("apikey", anon);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            onOk?.Invoke(JsonConvert.DeserializeObject<LoginDTO>(req.downloadHandler.text));
        else
            onErr?.Invoke(req.downloadHandler.text);
    }

    // Registers a new user account and returns the resulting LoginDTO.
    public IEnumerator Register(
        string email, string password,
        System.Action<LoginDTO> onOk,
        System.Action<string>   onErr)
    {
        string endpoint = $"{url}/auth/v1/signup";
        var body = new { email, password };

        var req = new UnityWebRequest(endpoint, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body))),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 10
        };
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("apikey", anon);

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var dto = JsonConvert.DeserializeObject<LoginDTO>(req.downloadHandler.text);
            onOk?.Invoke(dto);
        }
        else
        {
            onErr?.Invoke(req.downloadHandler.text);
        }
    }

    // === DTO DEFINITIONS ===

    // DTO returned by Supabase authentication endpoints.
    [System.Serializable]
    public class LoginDTO
    {
        public string access_token;
        public string refresh_token;
        public int    expires_in;
    }
}