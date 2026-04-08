# Dockerfile
# 放在 testEcpay/ 資料夾的根目錄（跟 testEcpay.sln 同層）

# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 複製 csproj 並還原套件（利用 Docker 快取層）
COPY testEcpay/testEcpay.csproj ./testEcpay/
RUN dotnet restore ./testEcpay/testEcpay.csproj

# 複製所有程式碼並發布
COPY testEcpay/ ./testEcpay/
RUN dotnet publish ./testEcpay/testEcpay.csproj -c Release -o /app/publish

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Railway 會自動注入 PORT 環境變數
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}

ENTRYPOINT ["dotnet", "testEcpay.dll"]
