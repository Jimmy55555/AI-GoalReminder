using System.Net.Http.Json;

namespace 長期目標提醒器.Services;

public sealed record SupabaseSession(
    string AccessToken,
    string RefreshToken,
    string UserId,
    string Email,
    DateTimeOffset ExpiresAt);

public sealed class SupabaseAuthClient
{
    private readonly HttpClient httpClient;
    private readonly string anonKey;

    public SupabaseAuthClient(HttpClient httpClient, string anonKey)
    {
        this.httpClient = httpClient;
        this.anonKey = anonKey;
    }

    public async Task<SupabaseSession> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new SupabaseException("請輸入帳號與密碼。");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/v1/token?grant_type=password")
        {
            Content = JsonContent.Create(new { email = email.Trim(), password })
        };
        request.Headers.TryAddWithoutValidation("apikey", anonKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = body.Length > 240 ? body[..240] + "…" : body;
            throw new SupabaseException(
                string.IsNullOrWhiteSpace(detail) ? "登入失敗，請檢查帳號或密碼。" : detail,
                response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(SupabaseClient.JsonOptions, cancellationToken)
            ?? throw new SupabaseException("登入回應格式不正確。");

        if (string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new SupabaseException("登入回應缺少 access token。");
        }

        return new SupabaseSession(
            payload.AccessToken,
            payload.RefreshToken ?? "",
            payload.User?.Id ?? "",
            payload.User?.Email ?? email.Trim(),
            DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn <= 0 ? 3600 : payload.ExpiresIn));
    }

    public async Task<SupabaseSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new SupabaseException("登入工作階段已過期，請重新登入。");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/v1/token?grant_type=refresh_token")
        {
            Content = JsonContent.Create(new { refresh_token = refreshToken })
        };
        request.Headers.TryAddWithoutValidation("apikey", anonKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new SupabaseException("登入工作階段已過期，請重新登入。", response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(SupabaseClient.JsonOptions, cancellationToken)
            ?? throw new SupabaseException("更新登入工作階段失敗。");

        return new SupabaseSession(
            payload.AccessToken ?? "",
            payload.RefreshToken ?? refreshToken,
            payload.User?.Id ?? "",
            payload.User?.Email ?? "",
            DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn <= 0 ? 3600 : payload.ExpiresIn));
    }

    private sealed class AuthResponse
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public AuthUser? User { get; set; }
    }

    private sealed class AuthUser
    {
        public string? Id { get; set; }
        public string? Email { get; set; }
    }
}
