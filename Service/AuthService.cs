using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class AuthService
{
    private readonly IConfiguration _cfg;
    private readonly IHttpContextAccessor _httpContext;

    public AuthService(IConfiguration cfg, IHttpContextAccessor httpContextAccessor)
    {
        _cfg = cfg;
        _httpContext = httpContextAccessor;
    }

    private string? SessionGet(string key) => _httpContext.HttpContext?.Session.GetString(key);
    private void SessionSet(string key, string val) => _httpContext.HttpContext?.Session.SetString(key, val);

    public string BuildAuthorizeUrl(string[] scopes)
    {
        var clientId = _cfg["Spotify:ClientId"]!;
        var redirect = Uri.EscapeDataString(_cfg["Spotify:RedirectUri"]!);
        var scope = Uri.EscapeDataString(string.Join(" ", scopes));
        var state = Guid.NewGuid().ToString("N");
        SessionSet("oauth_state", state);

        return $"https://accounts.spotify.com/authorize?client_id={clientId}&response_type=code&redirect_uri={redirect}&scope={scope}&state={state}";
    }

    public async Task ExchangeCodeAsync(string code, string state)
    {
        // valida state (opcional, mas recomendado)
        var expected = SessionGet("oauth_state");
        if (!string.IsNullOrWhiteSpace(expected) && expected != state)
            throw new InvalidOperationException("Invalid state.");

        var clientId = _cfg["Spotify:ClientId"]!;
        var clientSecret = _cfg["Spotify:ClientSecret"]!;
        var redirect = _cfg["Spotify:RedirectUri"]!;

        using var http = new HttpClient();
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string,string>("grant_type","authorization_code"),
            new KeyValuePair<string,string>("code", code),
            new KeyValuePair<string,string>("redirect_uri", redirect),
        });

        var res = await http.PostAsync("https://accounts.spotify.com/api/token", form);
        res.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

        var access = json.GetProperty("access_token").GetString()!;
        var refresh = json.GetProperty("refresh_token").GetString()!;
        var expiresIn = json.GetProperty("expires_in").GetInt32();

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60); // margem

        SessionSet("access_token", access);
        SessionSet("refresh_token", refresh);
        SessionSet("expires_at", expiresAt.ToUnixTimeSeconds().ToString());
    }

    public async Task<string> GetValidAccessTokenAsync()
    {
        var access = SessionGet("access_token");
        var refresh = SessionGet("refresh_token");
        var expiresAtStr = SessionGet("expires_at");

        if (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh) || string.IsNullOrWhiteSpace(expiresAtStr))
            throw new InvalidOperationException("Usuário não autenticado.");

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expiresAtStr));
        if (DateTimeOffset.UtcNow < expiresAt)
            return access!;

        // Refresh
        var clientId = _cfg["Spotify:ClientId"]!;
        var clientSecret = _cfg["Spotify:ClientSecret"]!;

        using var http = new HttpClient();
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string,string>("grant_type","refresh_token"),
            new KeyValuePair<string,string>("refresh_token", refresh!)
        });

        var res = await http.PostAsync("https://accounts.spotify.com/api/token", form);
        res.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;

        var newAccess = json.GetProperty("access_token").GetString()!;
        var expiresIn = json.GetProperty("expires_in").GetInt32();
        var newExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60);

        SessionSet("access_token", newAccess);
        SessionSet("expires_at", newExpiresAt.ToUnixTimeSeconds().ToString());

        return newAccess;
    }
}
