using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public sealed class EscapeBlock9MultiplayerApiClient
{
    private readonly Func<string> tokenProvider;
    private string baseHttpUrl;

    public EscapeBlock9MultiplayerApiClient(string baseHttpUrl, Func<string> tokenProvider)
    {
        this.tokenProvider = tokenProvider;
        BaseHttpUrl = baseHttpUrl;
    }

    public string BaseHttpUrl
    {
        get => baseHttpUrl;
        set => baseHttpUrl = NormalizeBaseUrl(value);
    }

    public string BuildWebSocketUrl(string path)
    {
        string wsBase = baseHttpUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? "wss://" + baseHttpUrl.Substring("https://".Length)
            : "ws://" + baseHttpUrl.Substring("http://".Length);

        string token = tokenProvider?.Invoke();
        string separator = path.Contains("?") ? "&" : "?";
        return $"{wsBase}{path}{separator}token={UnityWebRequest.EscapeURL(token ?? string.Empty)}";
    }

    public IEnumerator CreateGuest(Action<MultiplayerApiResult<MultiplayerTokenResponseDto>> onComplete)
    {
        yield return Post("/api/accounts/guest/", "{}", false, onComplete);
    }

    public IEnumerator CreateLobby(int maxPlayers, Action<MultiplayerApiResult<MultiplayerLobbyDto>> onComplete)
    {
        string body = JsonUtility.ToJson(new MultiplayerCreateLobbyRequestDto { maxPlayers = maxPlayers });
        yield return Post("/api/lobbies/create/", body, true, onComplete);
    }

    public IEnumerator JoinLobby(string code, Action<MultiplayerApiResult<MultiplayerJoinLobbyResponseDto>> onComplete)
    {
        string safeCode = UnityWebRequest.EscapeURL((code ?? string.Empty).Trim().ToUpperInvariant());
        yield return Post($"/api/lobbies/{safeCode}/join/", "{}", true, onComplete);
    }

    public IEnumerator SetReady(int lobbyId, bool isReady, Action<MultiplayerApiResult<MultiplayerLobbyEventDto>> onComplete)
    {
        string body = JsonUtility.ToJson(new MultiplayerReadyRequestDto { isReady = isReady });
        yield return Post($"/api/lobbies/{lobbyId}/ready/", body, true, onComplete);
    }

    public IEnumerator StartLobby(int lobbyId, string mapId, Action<MultiplayerApiResult<MultiplayerGameStartedDto>> onComplete)
    {
        string body = JsonUtility.ToJson(new MultiplayerStartLobbyRequestDto { mapId = mapId });
        yield return Post($"/api/lobbies/{lobbyId}/start/", body, true, onComplete);
    }

    public IEnumerator GetLobby(int lobbyId, Action<MultiplayerApiResult<MultiplayerLobbyDto>> onComplete)
    {
        yield return Request("GET", $"/api/lobbies/{lobbyId}/", null, true, onComplete);
    }

    private IEnumerator Post<T>(string path, string body, bool requiresAuth, Action<MultiplayerApiResult<T>> onComplete)
    {
        yield return Request("POST", path, body, requiresAuth, onComplete);
    }

    private IEnumerator Request<T>(string method, string path, string body, bool requiresAuth, Action<MultiplayerApiResult<T>> onComplete)
    {
        using (UnityWebRequest request = new UnityWebRequest(baseHttpUrl + path, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();

            if (body != null)
            {
                byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            if (requiresAuth)
            {
                string token = tokenProvider?.Invoke();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.SetRequestHeader("Authorization", "Token " + token);
                }
            }

            yield return request.SendWebRequest();

            string responseText = request.downloadHandler?.text;
            if (request.result != UnityWebRequest.Result.Success)
            {
                string fallback = $"{method} {path} failed ({request.responseCode})";
                onComplete?.Invoke(MultiplayerApiResult<T>.Failure(MultiplayerJson.ExtractError(responseText, fallback)));
                yield break;
            }

            try
            {
                T response = JsonUtility.FromJson<T>(responseText);
                onComplete?.Invoke(MultiplayerApiResult<T>.Success(response));
            }
            catch (ArgumentException exception)
            {
                onComplete?.Invoke(MultiplayerApiResult<T>.Failure($"Could not parse server response: {exception.Message}"));
            }
        }
    }

    private static string NormalizeBaseUrl(string raw)
    {
        string url = string.IsNullOrWhiteSpace(raw) ? "https://cavegame-production.up.railway.app" : raw.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + url;
        }

        return url.EndsWith("/") ? url[..^1] : url;
    }
}
