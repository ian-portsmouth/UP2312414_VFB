using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using System.Data;
using FoodBankApp.Models;

namespace FoodBankApp.Controllers
{
    [Authorize]
    public class StaffOrdersController : Controller
    {
        private readonly IConfiguration _config;

        public StaffOrdersController(IConfiguration config)
        {
            _config = config;
        }

      
        [HttpGet]
        public async Task<IActionResult> Index(string? code = null)
        {
            var orders = new List<StaffOrderListItem>();

            string connectionString = GetConnectionString();

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"
SELECT TOP 200 OrderID, OrderCode, ContactName, DateCreated, Status
FROM dbo.Orders
WHERE (@code IS NULL OR OrderCode = @code)
ORDER BY DateCreated DESC;";

            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.Add(new SqlParameter("@code", SqlDbType.NVarChar, 32)
            {
                Value = (object?)code ?? DBNull.Value
            });

            await using SqlDataReader rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                orders.Add(new StaffOrderListItem
                {
                    OrderId = rdr.GetGuid(0),
                    OrderCode = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                    ContactName = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                    DateCreated = rdr.GetDateTime(3),
                    Status = rdr.IsDBNull(4) ? "Pending" : rdr.GetString(4)
                });
            }

            ViewBag.FilterCode = code;
            return View(orders);
        }

        
        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var vm = new StaffOrderDetailsViewModel();

            string connectionString = GetConnectionString();

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

           
            const string headerSql = @"
SELECT OrderID, OrderCode, ContactName, DateCreated, Status
FROM dbo.Orders
WHERE OrderID = @id;";

            await using (var cmd = new SqlCommand(headerSql, conn))
            {
                cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id });

                await using SqlDataReader rdr = await cmd.ExecuteReaderAsync();

                if (!await rdr.ReadAsync())
                    return NotFound();

                vm.OrderId = rdr.GetGuid(0);
                vm.OrderCode = rdr.IsDBNull(1) ? "" : rdr.GetString(1);
                vm.ContactName = rdr.IsDBNull(2) ? "" : rdr.GetString(2);
                vm.DateCreated = rdr.GetDateTime(3);
                vm.Status = rdr.IsDBNull(4) ? "Pending" : rdr.GetString(4);
            }

            
            const string linesSql = @"
SELECT i.ItemName, i.ImagePath, oi.Quantity
FROM dbo.OrderItems oi
JOIN dbo.Items i ON i.ItemID = oi.ItemID
WHERE oi.OrderID = @id
ORDER BY i.ItemName;";

            await using (var cmd = new SqlCommand(linesSql, conn))
            {
                cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id });

                await using SqlDataReader rdr = await cmd.ExecuteReaderAsync();

                while (await rdr.ReadAsync())
                {
                    vm.Lines.Add(new OrderLineDetail
                    {
                        ItemName = rdr.GetString(0),
                        ImagePath = rdr.IsDBNull(1) ? null : rdr.GetString(1),
                        Quantity = rdr.GetInt32(2)
                    });
                }
            }

            return View(vm);
        }

       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(Guid id)
        {
            string connectionString = GetConnectionString();

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = "UPDATE dbo.Orders SET Status = 'Completed' WHERE OrderID = @id;";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id });

            int rows = await cmd.ExecuteNonQueryAsync();

            if (rows == 0)
                return NotFound();

            TempData["Flash"] = "Order marked as Completed.";

            return RedirectToAction(nameof(Details), new { id });
        }

        private string GetConnectionString()
        {
            return _config.GetConnectionString("DefaultConnection");
        }
    }
}
