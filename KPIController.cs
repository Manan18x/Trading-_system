using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;

[ApiController]
[Route("api/kpi")]
public class KPIController : ControllerBase
{
    private readonly IConfiguration _config;
    public KPIController(IConfiguration config) => _config = config;

    [HttpGet("sales")]
    public async Task<IActionResult> GetSalesKpi(DateTime startDate, DateTime endDate)
    {
        using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        string sql = "EXEC usp_TopItemsByMargin @StartDate, @EndDate, @TopN";
        var topItems = await conn.QueryAsync(sql, new { StartDate = startDate, EndDate = endDate, TopN = 5 });
        return Ok(new { StartDate = startDate, EndDate = endDate, TopItems = topItems });
    }
}
