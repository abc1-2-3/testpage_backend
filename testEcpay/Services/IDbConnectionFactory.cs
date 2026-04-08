// Services/IDbConnectionFactory.cs
using System.Data;

namespace testEcpay.Services;

/// <summary>資料庫連線工廠介面，方便之後替換或測試 mock</summary>
public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync();
}
