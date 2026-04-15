//controller for the ordering page

using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using FoodBankApp.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FoodBankApp.Controllers
{
    public class PublicOrdersController : Controller
    {
        private readonly IConfiguration _config;
        private readonly ILogger<PublicOrdersController> _logger;

        public PublicOrdersController(IConfiguration config, ILogger<PublicOrdersController> logger)
        {
            _config = config;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new PublicOrderViewModel
            {
                // Start with an empty list, then fill it from the database.
                Lines = await GetActiveItemsForOrderFormAsync()
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PublicOrderViewModel model)
        {

            model.Lines ??= new List<OrderLine>();

            List<OrderLine> requestedLines = model.Lines
                .Where(l => l.Quantity > 0)
                .ToList();

            if (string.IsNullOrWhiteSpace(model.ContactName))
            {
                ModelState.AddModelError(string.Empty, "Please enter your name.");
            }

            if (!requestedLines.Any())
            {
                ModelState.AddModelError(string.Empty, "Please choose at least one item.");
            }

            if (!ModelState.IsValid)
            {
                return await ReloadItems(model);
            }

            DataTable itemsTable = BuildItemsTable(requestedLines);

            string connectionString = GetConnectionString();
            string? orderCode = null;

            try
            {
                await using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                await using var cmd = new SqlCommand("dbo.PlaceOrder_NoAuth", conn)
                {
                    CommandType = CommandType.StoredProcedure
                };

                cmd.Parameters.AddWithValue("@ContactName", model.ContactName);

                SqlParameter itemsParam = cmd.Parameters.Add("@Items", SqlDbType.Structured);
                itemsParam.TypeName = "dbo.OrderItemType";
                itemsParam.Value = itemsTable;

                var orderCodeParam = new SqlParameter("@OrderCode", SqlDbType.NVarChar, 32)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(orderCodeParam);

                await using (SqlDataReader rdr = await cmd.ExecuteReaderAsync())
                {
                    if (rdr.FieldCount > 0 &&
                        rdr.GetName(0).Equals("ItemName", StringComparison.OrdinalIgnoreCase))
                    {
                        var errors = new List<string>();

                        while (await rdr.ReadAsync())
                        {
                            errors.Add($"{rdr["ItemName"]}: requested {rdr["QtyRequested"]}, available {rdr["StockQuantity"]}");
                        }

                        ModelState.AddModelError(string.Empty, "Not enough stock: " + string.Join("; ", errors));
                        return await ReloadItems(model);
                    }
                }

                orderCode = orderCodeParam.Value as string;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error placing order");
                ModelState.AddModelError(string.Empty, "An error occurred while placing your order. Please try again.");
                return await ReloadItems(model);
            }

            TempData["OrderCode"] = orderCode;
            TempData["ContactName"] = model.ContactName;

            return RedirectToAction(nameof(Confirmation));
        }

        [HttpGet]
        public IActionResult Confirmation()
        {
            ViewBag.OrderCode = TempData["OrderCode"]?.ToString();
            ViewBag.ContactName = TempData["ContactName"]?.ToString();
            return View();
        }

        private async Task<IActionResult> ReloadItems(PublicOrderViewModel model)
        {
            model.Lines ??= new List<OrderLine>();

            Dictionary<Guid, ItemInfo> itemsNow = await GetActiveItemsDictionaryAsync();

            foreach (OrderLine line in model.Lines)
            {
                if (itemsNow.TryGetValue(line.ItemId, out ItemInfo info))
                {
                    line.ItemName = info.ItemName;
                    line.Available = info.StockQuantity;
                    line.Category = info.Category;

                    line.ImagePath = string.IsNullOrWhiteSpace(info.ImagePath) ? null : info.ImagePath;
                }
            }

            model.Lines = model.Lines
                .OrderBy(l => l.Category ?? "Uncategorized")
                .ThenBy(l => l.ItemName)
                .ToList();

            return View("Create", model);
        }


        private string GetConnectionString()
        {
            return _config.GetConnectionString("DefaultConnection");
        }

        private async Task<List<OrderLine>> GetActiveItemsForOrderFormAsync()
        {
            var lines = new List<OrderLine>();
            string connectionString = GetConnectionString();

            const string sql = @"
                SELECT ItemID, ItemName, StockQuantity, Category, ImagePath
                FROM dbo.Items
                WHERE IsActive = 1
                ORDER BY Category, ItemName;";

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(sql, conn);
            await using SqlDataReader rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                Guid itemId = rdr.GetGuid(0);
                string itemName = rdr.GetString(1);
                int stockQty = rdr.GetInt32(2);

                string? category = rdr.IsDBNull(3) ? null : rdr.GetString(3);
                string? imagePath = rdr.IsDBNull(4) ? null : rdr.GetString(4);

                lines.Add(new OrderLine
                {
                    ItemId = itemId,
                    ItemName = itemName,
                    Available = stockQty,
                    Category = category,
                    Quantity = 0,
                    ImagePath = string.IsNullOrWhiteSpace(imagePath) ? null : imagePath
                });
            }

            return lines
                .OrderBy(l => l.Category ?? "Uncategorized")
                .ThenBy(l => l.ItemName)
                .ToList();
        }

        private async Task<Dictionary<Guid, ItemInfo>> GetActiveItemsDictionaryAsync()
        {
            var result = new Dictionary<Guid, ItemInfo>();
            string connectionString = GetConnectionString();

            const string sql = @"
                SELECT ItemID, ItemName, StockQuantity, Category, ImagePath
                FROM dbo.Items
                WHERE IsActive = 1;";

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand(sql, conn);
            await using SqlDataReader rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                var info = new ItemInfo
                {
                    ItemId = rdr.GetGuid(0),
                    ItemName = rdr.GetString(1),
                    StockQuantity = rdr.GetInt32(2),
                    Category = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                    ImagePath = rdr.IsDBNull(4) ? null : rdr.GetString(4)
                };

                result[info.ItemId] = info;
            }

            return result;
        }

        private static DataTable BuildItemsTable(IEnumerable<OrderLine> requestedLines)
        {
            DataTable table = new DataTable();
            table.Columns.Add("ItemID", typeof(Guid));
            table.Columns.Add("Quantity", typeof(int));

            foreach (OrderLine line in requestedLines)
            {
                table.Rows.Add(line.ItemId, line.Quantity);
            }

            return table;
        }

        private class ItemInfo
        {
            public Guid ItemId { get; set; }
            public string ItemName { get; set; } = string.Empty;
            public int StockQuantity { get; set; }
            public string? Category { get; set; }
            public string? ImagePath { get; set; }
        }
    }
}
