// Controllers/AuthController.cs
// 這支 API 是 Next.js Server 呼叫的（不是瀏覽器直接呼叫）
// 流程：Auth.js 登入成功 → Next.js Server Action 呼叫這裡 → 回傳 JWT

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using testEcpay.Models;
using testEcpay.Services;

namespace testEcpay.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly JwtService _jwtService;
    private readonly IConfiguration _config;

    public AuthController(IUserService userService, JwtService jwtService, IConfiguration config)
    {
        _userService = userService;
        _jwtService = jwtService;
        _config = config;
    }

    /// <summary>
    /// Next.js Server 在 Auth.js 登入後呼叫此端點
    /// 驗證 X-Internal-Secret → Upsert User → 回傳 JWT
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // 1. 驗證 Internal Secret（防止外部直接呼叫）
        var expectedSecret = _config["InternalSecret"];
        var receivedSecret = Request.Headers["X-Internal-Secret"].FirstOrDefault();

        if (string.IsNullOrEmpty(expectedSecret) || expectedSecret != receivedSecret)
        {
            return Unauthorized(new { error = "Invalid internal secret" });
        }

        // 2. 基本資料驗證
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { error = "Email 不可為空" });
        }

        // 3. Upsert User（找到就更新，找不到就建立）
        var user = await _userService.UpsertAsync(
            request.Email,
            request.Name,
            request.Image,
            request.GoogleId
        );

        // 4. 簽發 JWT
        var token = _jwtService.GenerateToken(user.Id, user.Email);

        return Ok(new LoginResponse(
            Token: token,
            UserId: user.Id,
            Email: user.Email,
            Name: user.Name,
            Image: user.Image
        ));
    }

    /// <summary>
    /// 前端可以用這個端點確認 JWT 是否仍有效，並取回用戶資訊
    /// GET /api/auth/me
    /// Header: Authorization: Bearer {token}
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                 ?? User.FindFirst("email")?.Value;

        if (string.IsNullOrEmpty(email))
            return Unauthorized();

        var user = await _userService.GetByEmailAsync(email);
        if (user == null)
            return NotFound(new { error = "找不到用戶" });

        return Ok(new
        {
            userId = user.Id,
            email = user.Email,
            name = user.Name,
            image = user.Image
        });
    }
}
