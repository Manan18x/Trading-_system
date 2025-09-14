using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[Route("api")]
public class PostingController : ControllerBase
{
    private readonly IConfiguration _config;
    public PostingController(IConfiguration config) => _config = config;

    [Authorize(Roles = "Admin,Ops")]
    [HttpPost("receipts/{receiptId}/post")]
    public async Task<IActionResult> PostReceipt(int receiptId)
    {
        using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        var result = await conn.ExecuteScalarAsync<int>(
            "EXEC usp_PostReceipt @ReceiptId", new { ReceiptId = receiptId });
        return Ok(new { message = "Receipt posted", code = result });
    }

    [Authorize(Roles = "Admin,Ops")]
    [HttpPost("shipments/{shipId}/post")]
    public async Task<IActionResult> PostShipment(int shipId)
    {
        using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        try
        {
            var result = await conn.ExecuteScalarAsync<int>(
                "EXEC usp_PostShipment @ShipId", new { ShipId = shipId });
            return Ok(new { message = "Shipment posted", code = result });
        }
        catch (SqlException ex)
        {
            return BadRequest(new { code = ex.Number, message = ex.Message });
        }
    }
}
