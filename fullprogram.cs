using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Data.SqlClient;
using System.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// config
var connStr = builder.Configuration.GetConnectionString("TradingDb") ?? "Server=.;Database=Trading;Trusted_Connection=True;";
var jwtKey = builder.Configuration["Jwt:Key"] ?? "SuperSecretDevKeyReplaceThis"; // store securely in prod
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "trading.local";

// auth
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OpsOrAdmin", policy => policy.RequireRole("Admin","Ops"));
});

var app = builder.Build();

app.UseSwagger(); app.UseSwaggerUI();
app.UseAuthentication(); app.UseAuthorization();

// central error middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (SqlException sex)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { code = "SQL_ERROR", message = sex.Message, details = sex.Number });
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { code = "ERR", message = ex.Message });
    }
});

// helper: create DB connection
IDbConnection CreateConn() => new SqlConnection(connStr);

// ----- Auth/Login -----
app.MapPost("/auth/login", async (LoginRequest req) =>
{
    using var conn = CreateConn();
    var user = await conn.QuerySingleOrDefaultAsync<User>("SELECT UserId, Username, PasswordHash, [Role] FROM [Users] WHERE Username = @u", new { u = req.Username });
    if (user == null) return Results.BadRequest(new { code = "AUTH_FAILED", message = "Invalid username or password" });

    // compute SHA256 and compare
    using var sha = SHA256.Create();
    var passBytes = Encoding.UTF8.GetBytes(req.Password);
    var hash = sha.ComputeHash(passBytes);

    if (!hash.SequenceEqual(user.PasswordHash)) return Results.BadRequest(new { code = "AUTH_FAILED", message = "Invalid username or password" });

    // build JWT
    var claims = new[] { new Claim(ClaimTypes.Name, user.Username), new Claim(ClaimTypes.Role, user.Role) };
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
        issuer: jwtIssuer,
        claims: claims,
        expires: DateTime.UtcNow.AddHours(8),
        signingCredentials: creds
    );
    var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    return Results.Ok(new { token = jwt });
});

// ----- GET /api/items?search=&page=&pageSize= -----
app.MapGet("/api/items", async (string? search, int page = 1, int pageSize = 20) =>
{
    using var conn = CreateConn();
    var where = string.IsNullOrWhiteSpace(search) ? "" : "WHERE Sku LIKE @s OR [Name] LIKE @s OR [Category] LIKE @s";
    var sql = $@"
        SELECT ItemId, Sku, [Name], [Category], UnitPrice, Active
        FROM Items
        {where}
        ORDER BY ItemId
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        SELECT COUNT(1) FROM Items {where};";
    using var multi = await conn.QueryMultipleAsync(sql, new { s = $"%{search}%", offset = (page - 1) * pageSize, pageSize });
    var items = (await multi.ReadAsync()).ToList();
    var total = await multi.ReadFirstAsync<int>();
    return Results.Ok(new { page, pageSize, total, items });
});

// ----- GET /api/items/{id}/stock -----
app.MapGet("/api/items/{id:int}/stock", async (int id) =>
{
    using var conn = CreateConn();
    var onHand = await conn.QuerySingleOrDefaultAsync<int?>("SELECT OnHand FROM vw_StockOnHand WHERE ItemId = @id", new { id });
    return Results.Ok(new { itemId = id, onHand = onHand ?? 0 });
});

// ----- POST /api/receipts/{receiptId}/post -----
app.MapPost("/api/receipts/{receiptId:int}/post", [Microsoft.AspNetCore.Authorization.Authorize(Policy = "OpsOrAdmin")] async (int receiptId) =>
{
    using var conn = (SqlConnection)CreateConn();
    var dp = new Dapper.DynamicParameters();
    dp.Add("@ReceiptId", receiptId, DbType.Int32);
    dp.Add("ReturnValue", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);
    await conn.ExecuteAsync("dbo.usp_PostReceipt", dp, commandType: CommandType.StoredProcedure);
    var rv = dp.Get<int>("ReturnValue");
    if (rv != 0) return Results.BadRequest(new { code = "POST_RECEIPT_FAILED", returnCode = rv });
    return Results.Ok(new { code = "OK", returnCode = rv });
});

// ----- POST /api/shipments/{shipId}/post -----
app.MapPost("/api/shipments/{shipId:int}/post", [Microsoft.AspNetCore.Authorization.Authorize(Policy = "OpsOrAdmin")] async (int shipId) =>
{
    using var conn = (SqlConnection)CreateConn();
    var dp = new Dapper.DynamicParameters();
    dp.Add("@ShipId", shipId, DbType.Int32);
    dp.Add("ReturnValue", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);
    try
    {
        await conn.ExecuteAsync("dbo.usp_PostShipment", dp, commandType: CommandType.StoredProcedure);
        var rv = dp.Get<int>("ReturnValue");
        if (rv != 0) return Results.BadRequest(new { code = "POST_SHIPMENT_FAILED", returnCode = rv });
        return Results.Ok(new { code = "OK", returnCode = rv });
    }
    catch (SqlException sqlEx)
    {
        // RAISERROR will surface here
        return Results.BadRequest(new { code = "SQL_ERROR", message = sqlEx.Message });
    }
});

// ----- GET /api/kpi/sales?startDate=&endDate=  -----
app.MapGet("/api/kpi/sales", async (DateTime? startDate, DateTime? endDate) =>
{
    var s = startDate ?? DateTime.UtcNow.AddMonths(-1);
    var e = endDate ?? DateTime.UtcNow;
    using var conn = CreateConn();
    // total revenue & cost & margin
    var totalsSql = @"
    SELECT
      SUM(sl.QtyShipped * sol.UnitPrice) as TotalRevenue,
      SUM(sl.QtyShipped * ISNULL(lastp.UnitCost,0)) as TotalCost
    FROM ShipmentLines sl
    INNER JOIN Shipments sh ON sl.ShipId = sh.ShipId
    INNER JOIN SalesOrderLines sol ON sl.SoLineId = sol.SoLineId
    OUTER APPLY (
      SELECT TOP 1 pol.UnitCost
      FROM PurchaseOrderLines pol
      INNER JOIN PurchaseOrders p ON pol.PoId = p.PoId
      WHERE pol.ItemId = sol.ItemId AND p.PoDate <= sh.ShipDate
      ORDER BY p.PoDate DESC
    ) lastp(UnitCost)
    WHERE sh.ShipDate BETWEEN @s AND @e;
    ";
    var totals = await conn.QuerySingleAsync(totalsSql, new { s = s.Date, e = e.Date });
    // top 5 items by margin using the stored proc
    var topItems = await conn.QueryAsync("dbo.usp_TopItemsByMargin", new { StartDate = s.Date, EndDate = e.Date, TopN = 5 }, commandType: CommandType.StoredProcedure);
    decimal totalRevenue = totals.TotalRevenue ?? 0;
    decimal totalCost = totals.TotalCost ?? 0;
    decimal margin = totalRevenue - totalCost;
    return Results.Ok(new {
        startDate = s.Date,
        endDate = e.Date,
        revenue = totalRevenue,
        cost = totalCost,
        margin = margin,
        topItems = topItems
    });
});

app.Run();

record LoginRequest(string Username, string Password);
record User(int UserId, string Username, byte[] PasswordHash, string Role);
