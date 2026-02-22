using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public static class OpenAIImageClient
{
    private const string Endpoint = "https://api.openai.com/v1/images/generations";
    private const string EditorPrefsApiKey = "GSP.OpenAI.ApiKey";
    private static DateTime nextAllowedRequestUtc = DateTime.MinValue;

    [Serializable]
    private sealed class RequestPayload
    {
        public string model;
        public string prompt;
        public string size;
        public string background;
        public string output_format;
    }

    [Serializable]
    private sealed class ResponsePayload
    {
        public ImageData[] data;
    }

    [Serializable]
    private sealed class ImageData
    {
        public string b64_json;
    }

    public static string GetApiKey()
    {
        var env = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env.Trim();
        }

        return EditorPrefs.GetString(EditorPrefsApiKey, string.Empty).Trim();
    }

    public static void SetEditorPrefsApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            EditorPrefs.DeleteKey(EditorPrefsApiKey);
            return;
        }

        EditorPrefs.SetString(EditorPrefsApiKey, apiKey.Trim());
    }

    public static async Task<byte[]> GeneratePngBase64(string prompt, string model, string size, string background, string outputFormat)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Missing API key. Set OPENAI_API_KEY or save one in EditorPrefs.");
        }

        var payload = new RequestPayload
        {
            model = string.IsNullOrWhiteSpace(model) ? "gpt-image-1" : model,
            prompt = prompt,
            size = string.IsNullOrWhiteSpace(size) ? "1024x1024" : size,
            background = string.IsNullOrWhiteSpace(background) ? "transparent" : background,
            output_format = string.IsNullOrWhiteSpace(outputFormat) ? "png" : outputFormat
        };

        var bodyJson = JsonUtility.ToJson(payload);
        var bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

        const int maxAttempts = 5;
        Exception lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var now = DateTime.UtcNow;
            if (now < nextAllowedRequestUtc)
            {
                var waitMs = Mathf.Max(0, (int)(nextAllowedRequestUtc - now).TotalMilliseconds);
                if (waitMs > 0)
                {
                    await Task.Delay(waitMs);
                }
            }

            using var request = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(bodyBytes),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 120
            };

            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            request.SetRequestHeader("Content-Type", "application/json");

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            var status = (int)request.responseCode;
            var text = request.downloadHandler?.text ?? string.Empty;
            var shouldRetry = status == 429 || status >= 500 || request.result == UnityWebRequest.Result.ConnectionError;

            if (request.result == UnityWebRequest.Result.Success && status >= 200 && status < 300)
            {
                var parsed = JsonUtility.FromJson<ResponsePayload>(text);
                if (parsed?.data == null || parsed.data.Length == 0 || string.IsNullOrWhiteSpace(parsed.data[0].b64_json))
                {
                    throw new InvalidOperationException("OpenAI image response was missing data[0].b64_json.");
                }

                nextAllowedRequestUtc = DateTime.UtcNow.AddSeconds(1.2);
                return Convert.FromBase64String(parsed.data[0].b64_json);
            }

            lastError = new InvalidOperationException($"OpenAI request failed ({status}): {text}");
            if (!shouldRetry || attempt == maxAttempts)
            {
                break;
            }

            var backoffSeconds = Mathf.Min(10f, Mathf.Pow(2f, attempt - 1));
            await Task.Delay(Mathf.RoundToInt(backoffSeconds * 1000f));
        }

        throw lastError ?? new InvalidOperationException("OpenAI request failed unexpectedly.");
    }
}
