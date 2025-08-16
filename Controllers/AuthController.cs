using Microsoft.AspNetCore.Mvc;

[Route("auth")]
public class AuthController : Controller
{
    private readonly AuthService _auth;
    public AuthController(AuthService auth) => _auth = auth;

    [HttpGet("login")]
    public IActionResult Login()
    {
        var url = _auth.BuildAuthorizeUrl(new[]{
            "streaming","user-modify-playback-state",
            "user-read-playback-state","user-read-currently-playing"
        });
        return Redirect(url);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        await _auth.ExchangeCodeAsync(code, state);
        return RedirectToAction("Index", "Home");
    }

  
    [HttpGet("token")]
    public async Task<IActionResult> Token()
    {
        var token = await _auth.GetValidAccessTokenAsync();
        return Json(new { access_token = token });
    }
}
