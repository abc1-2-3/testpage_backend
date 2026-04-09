// Program.cs

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using testEcpay.Services;

var builder = WebApplication.CreateBuilder(args);

// ── 設定讀取 ────────────────────────────────────────────────────────────────
var jwtConfig = builder.Configuration.GetSection("Jwt");
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000"];

// ── Controller ──────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<PendingOrderCleanupService>();

// ── CORS（允許 Next.js 前端呼叫）────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ── JWT 驗證 ─────────────────────────────────────────────────────────────────
var jwtSecret = jwtConfig["Secret"]
    ?? throw new InvalidOperationException("JWT Secret 未設定，請檢查 appsettings.json");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtConfig["Issuer"],
            ValidAudience = jwtConfig["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret)
            ),
            ClockSkew = TimeSpan.Zero // 不允許時間誤差
        };
    });

builder.Services.AddAuthorization();

// ── Dapper + PostgreSQL ──────────────────────────────────────────────────────
// 注入 DB 連線工廠，所有 Service 都透過它取得連線
builder.Services.AddSingleton<IDbConnectionFactory>(provider =>
{
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("資料庫連線字串未設定");
    return new NpgsqlConnectionFactory(connStr);
});

// ── 注入服務 ─────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDonationService, DonationService>();
builder.Services.AddSingleton<JwtService>();   // JWT 簽發/驗證工具

// ── Build ────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("FrontendPolicy");      // ⚠️ UseCors 必須在 UseAuthentication 之前
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── 啟動時自動建立資料表 ──────────────────────────────────────────────────────
await DatabaseInitializer.InitializeAsync(
    builder.Configuration.GetConnectionString("DefaultConnection")!
);

app.Run();
