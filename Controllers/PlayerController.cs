using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text.Json;

[Route("player")]
public class PlayerController : Controller
{
    private readonly AuthService _auth;
    private HttpClient Create(string token)
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    public PlayerController(AuthService auth) => _auth = auth;

    [HttpPost("play")]
    public async Task<IActionResult> Play([FromForm] string deviceId, [FromForm] string? uri, [FromForm] string? contextUri)
    {
        var token = await _auth.GetValidAccessTokenAsync();

        using var http = Create(token);
        var body = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(uri)) body["uris"] = new[] { uri };               // ex: spotify:track:...
        if (!string.IsNullOrWhiteSpace(contextUri)) body["context_uri"] = contextUri;    // ex: spotify:playlist:...

        var req = new HttpRequestMessage(HttpMethod.Put,
            $"https://api.spotify.com/v1/me/player/play?device_id={Uri.EscapeDataString(deviceId)}")
        { Content = JsonContent.Create(body) };

        var res = await http.SendAsync(req);
        return StatusCode((int)res.StatusCode, await res.Content.ReadAsStringAsync());
    }

    [HttpPost("pause")]
    public async Task<IActionResult> Pause([FromForm] string deviceId)
    {
        var token = await _auth.GetValidAccessTokenAsync();
        using var http = Create(token);

        var res = await http.PutAsync("https://api.spotify.com/v1/me/player/pause", null);
        return StatusCode((int)res.StatusCode);
    }
}
