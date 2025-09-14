using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;

[ApiController]
[Route("api/items")]
public class ItemsController : ControllerBase
{
    private readonly IConfiguration _config;

    public ItemsController(IConfiguration config)
    {
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> GetItems(string? search, int page = 1, int pageSize = 10)
    {
        using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));

        string sql = @"SELECT ItemId, Sku, Name, Category
                       FROM Items
                       WHERE (@search IS NULL OR Name LIKE '%' + @search + '%' OR Sku LIKE '%' + @search + '%')
                       ORDER BY Name
                       OFFSET (@page-1)*@pageSize ROWS
                       FETCH NEXT @pageSize ROWS ONLY;";

        var items = await conn.QueryAsync(sql, new { search, page, pageSize });
        return Ok(items);
    }

    [HttpGet("{id}/stock")]
    public async Task<IActionResult> GetStock(int id)
    {
        using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        string sql = "SELECT QtyOnHand FROM vw_StockOnHand WHERE ItemId=@id";
        var stock = await conn.QueryFirstOrDefaultAsync<int?>(sql, new { id });
        return Ok(new { ItemId = id, Stock = stock ?? 0 });
    }
}
