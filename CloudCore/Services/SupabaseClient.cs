using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace 長期目標提醒器.Services;

public sealed class SupabaseException(string message, HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

/// <summary>
/// 共用 Supabase REST 呼叫。WASM 用 publishable key + 使用者 access token；Mailer 用 secret key。
/// </summary>
public sealed class SupabaseClient
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient httpClient;
    private readonly string apiKey;
    private readonly Func<string?>? accessTokenProvider;

    public SupabaseClient(HttpClient httpClient, string apiKey, Func<string?>? accessTokenProvider = null)
    {
        this.httpClient = httpClient;
        this.apiKey = apiKey;
        this.accessTokenProvider = accessTokenProvider;
    }

    public async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        AddHeaders(request);
        return await SupabaseSendAsync<T>(request, cancellationToken);
    }

    public async Task<T?> PostAsync<T>(
        string path,
        object body,
        string? prefer = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.TryAddWithoutValidation(
            "Prefer",
            string.IsNullOrWhiteSpace(prefer) ? "return=representation" : $"{prefer},return=representation");
        AddHeaders(request);
        return await SupabaseSendAsync<T>(request, cancellationToken);
    }

    public async Task<T?> PatchAsync<T>(string path, object body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        request.Headers.TryAddWithoutValidation("Prefer", "return=representation");
        AddHeaders(request);
        return await SupabaseSendAsync<T>(request, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        AddHeaders(request);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var detail = await ReadErrorAsync(response, cancellationToken);
        throw new SupabaseException(detail, response.StatusCode);
    }

    public async Task<HttpResponseMessage> PostFunctionAsync(
        string functionName,
        object body,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"functions/v1/{functionName}")
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        AddHeaders(request);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("apikey", apiKey);
        var token = accessTokenProvider?.Invoke();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task<T?> SupabaseSendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await ReadErrorAsync(response, cancellationToken);
            throw new SupabaseException(detail, response.StatusCode);
        }

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(body)
            ? $"Supabase 請求失敗（{(int)response.StatusCode}）。"
            : Truncate(body, 300);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
