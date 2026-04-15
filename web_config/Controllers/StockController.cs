using FoodBankApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace FoodBankApp.Controllers
{
    [Authorize]
    public class StockController : Controller
    {
        private readonly IConfiguration _config;
        private readonly ILogger<StockController> _logger;
        private readonly IWebHostEnvironment _env;

        public StockController(IConfiguration config, ILogger<StockController> logger, IWebHostEnvironment env)
        {
            _config = config;
            _logger = logger;
            _env = env;
        }

       
        // THis shows the stock list 
      
        [HttpGet]
        public async Task<IActionResult> Index(string? q = null, string? category = null, bool showInactive = false)
        {
            var vm = new StockIndexViewModel
            {
                Search = q,
                Category = category,
                ShowInactive = showInactive
            };

            string connectionString = GetConnectionString();

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            
            const string categorySql = @"
SELECT DISTINCT Category
FROM dbo.Items
WHERE Category IS NOT NULL AND LTRIM(RTRIM(Category)) <> ''
ORDER BY Category;";

            await using (var catCmd = new SqlCommand(categorySql, conn))
            await using (SqlDataReader catRdr = await catCmd.ExecuteReaderAsync())
            {
                while (await catRdr.ReadAsync())
                {
                    if (!catRdr.IsDBNull(0))
                    {
                        vm.Categories.Add(catRdr.GetString(0));
                    }
                }
            }

            
            const string sql = @"
SELECT ItemID, ItemName, StockQuantity, Category, IsActive, ImagePath
FROM dbo.Items
WHERE (@showInactive = 1 OR IsActive = 1)
  AND (@category IS NULL OR Category = @category)
  AND (@q IS NULL OR ItemName LIKE '%' + @q + '%')
ORDER BY IsActive DESC, Category, ItemName;";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add(new SqlParameter("@showInactive", SqlDbType.Bit) { Value = showInactive });
            cmd.Parameters.Add(new SqlParameter("@category", SqlDbType.NVarChar, 60) { Value = (object?)category ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@q", SqlDbType.NVarChar, 120) { Value = (object?)q ?? DBNull.Value });

            await using SqlDataReader rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                vm.Items.Add(new InventoryItem
                {
                    ItemId = rdr.GetGuid(0),
                    ItemName = rdr.GetString(1),
                    StockQuantity = rdr.GetInt32(2),
                    Category = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                    IsActive = !rdr.IsDBNull(4) && rdr.GetBoolean(4),
                    ImagePath = rdr.IsDBNull(5) ? null : rdr.GetString(5)
                });
            }

            return View(vm);
        }

        
        [HttpGet]
        public IActionResult Create()
        {
            
            return View(new ItemUpsertViewModel
            {
                IsActive = true,
                StockQuantity = 0,
                ImagePath = "/images/items/default.png"
            });
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ItemUpsertViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            Guid id = Guid.NewGuid();

            
            string? imagePath = null;
            try
            {
                imagePath = await SaveItemImageAsync(model.ImageFile, id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Image upload failed for item {ItemId}", id);
                ModelState.AddModelError(string.Empty, "Image upload failed. Please use a JPG/PNG/WebP under 5MB.");
                return View(model);
            }

            try
            {
                string connectionString = GetConnectionString();

                await using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                const string sql = @"
INSERT INTO dbo.Items (ItemID, ItemName, StockQuantity, Category, IsActive, ImagePath)
VALUES (@id, @name, @qty, @cat, @active, @img);";

                await using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id });
                cmd.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 120) { Value = model.ItemName.Trim() });
                cmd.Parameters.Add(new SqlParameter("@qty", SqlDbType.Int) { Value = model.StockQuantity });
                cmd.Parameters.Add(new SqlParameter("@cat", SqlDbType.NVarChar, 60) { Value = (object?)model.Category?.Trim() ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@active", SqlDbType.Bit) { Value = model.IsActive });
                cmd.Parameters.Add(new SqlParameter("@img", SqlDbType.NVarChar, 260) { Value = (object?)imagePath ?? DBNull.Value });

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating item");
                ModelState.AddModelError(string.Empty, "Could not create item. Please check your database schema and try again.");
                return View(model);
            }

            TempData["Flash"] = "Item created.";
            return RedirectToAction(nameof(Index));
        }

        
        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            string connectionString = GetConnectionString();

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"
SELECT ItemID, ItemName, StockQuantity, Category, IsActive, ImagePath
FROM dbo.Items
WHERE ItemID = @id;";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id });

            await using SqlDataReader rdr = await cmd.ExecuteReaderAsync();

            if (!await rdr.ReadAsync())
                return NotFound();

            var vm = new ItemUpsertViewModel
            {
                ItemId = rdr.GetGuid(0),
                ItemName = rdr.GetString(1),
                StockQuantity = rdr.GetInt32(2),
                Category = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                IsActive = !rdr.IsDBNull(4) && rdr.GetBoolean(4),
                ImagePath = rdr.IsDBNull(5) ? null : rdr.GetString(5)
            };

            return View(vm);
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ItemUpsertViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (model.ItemId is null || model.ItemId == Guid.Empty)
                return BadRequest();

            try
            {
                string connectionString = GetConnectionString();

                await using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                
                string? existingImagePath = null;
                const string readImgSql = "SELECT ImagePath FROM dbo.Items WHERE ItemID = @id;";

                await using (var readCmd = new SqlCommand(readImgSql, conn))
                {
                    readCmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = model.ItemId.Value });
                    object? val = await readCmd.ExecuteScalarAsync();
                    existingImagePath = val == DBNull.Value ? null : val as string;
                }

                
                string? imagePath = existingImagePath;

                if (model.ImageFile is not null && model.ImageFile.Length > 0)
                {
                    imagePath = await SaveItemImageAsync(model.ImageFile, model.ItemId.Value);
                }

                const string sql = @"
UPDATE dbo.Items
SET ItemName = @name,
    StockQuantity = @qty,
    Category = @cat,
    IsActive = @active,
    ImagePath = @img
WHERE ItemID = @id;";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = model.ItemId.Value });
                cmd.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 120) { Value = model.ItemName.Trim() });
                cmd.Parameters.Add(new SqlParameter("@qty", SqlDbType.Int) { Value = model.StockQuantity });
                cmd.Parameters.Add(new SqlParameter("@cat", SqlDbType.NVarChar, 60) { Value = (object?)model.Category?.Trim() ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@active", SqlDbType.Bit) { Value = model.IsActive });
                cmd.Parameters.Add(new SqlParameter("@img", SqlDbType.NVarChar, 260) { Value = (object?)imagePath ?? DBNull.Value });

                int rows = await cmd.ExecuteNonQueryAsync();

                if (rows == 0)
                    return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating item");
                ModelState.AddModelError(string.Empty, "Could not update item. Please try again.");
                return View(model);
            }

            TempData["Flash"] = "Item updated.";
            return RedirectToAction(nameof(Index));
        }

        
        private async Task<string?> SaveItemImageAsync(IFormFile? file, Guid itemId)
        {
            if (file is null || file.Length == 0)
                return null;

            
            if (file.Length > 5 * 1024 * 1024)
                throw new InvalidOperationException("Image is too large.");

            
            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".webp"
            };

            if (string.IsNullOrWhiteSpace(ext) || !allowed.Contains(ext))
                throw new InvalidOperationException("Unsupported image type.");

            
            string folder = Path.Combine(_env.WebRootPath, "images", "items");
            Directory.CreateDirectory(folder);

            string fileName = $"item-{itemId}{ext}";
            string absolutePath = Path.Combine(folder, fileName);

            await using FileStream stream = System.IO.File.Create(absolutePath);
            await file.CopyToAsync(stream);

            
            return $"/images/items/{fileName}";
        }

        
        [HttpGet]
        public async Task<IActionResult> AddStock(Guid id)
        {
            string connectionString = GetConnectionString();

            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            const string sql = @"
SELECT ItemID, ItemName, StockQuantity
FROM dbo.Items
WHERE ItemID = @id;";

            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id });

            await using SqlDataReader rdr = await cmd.ExecuteReaderAsync();

            if (!await rdr.ReadAsync())
                return NotFound();

            var vm = new AddStockViewModel
            {
                ItemId = rdr.GetGuid(0),
                ItemName = rdr.GetString(1),
                CurrentStock = rdr.GetInt32(2),
                AddAmount = 1
            };

            return View(vm);
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStock(AddStockViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                string connectionString = GetConnectionString();

                await using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                const string sql = @"
UPDATE dbo.Items
SET StockQuantity = StockQuantity + @add
WHERE ItemID = @id;";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = model.ItemId });
                cmd.Parameters.Add(new SqlParameter("@add", SqlDbType.Int) { Value = model.AddAmount });

                int rows = await cmd.ExecuteNonQueryAsync();

                if (rows == 0)
                    return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding stock");
                ModelState.AddModelError(string.Empty, "Could not add stock. Please try again.");
                return View(model);
            }

            TempData["Flash"] = "Stock updated.";
            return RedirectToAction(nameof(Index));
        }

        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SeedBasics()
        {
            var starterItems = new (string Name, string Category)[]
            {
                ("Sugar", "Pantry"),
                ("Yoghurt", "Dairy"),
                ("Cereal", "Pantry"),
                ("Butter", "Dairy"),
                ("Mixed Vegetables", "Vegetables"),
                ("Juice Box", "Drinks")
            };

            try
            {
                string connectionString = GetConnectionString();

                await using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                await using var tx = await conn.BeginTransactionAsync();

                const string existsSql = "SELECT COUNT(1) FROM dbo.Items WHERE ItemName = @name;";

                const string insertSql = @"
INSERT INTO dbo.Items (ItemID, ItemName, StockQuantity, Category, IsActive)
VALUES (@id, @name, 0, @cat, 1);";

                int inserted = 0;

                foreach (var it in starterItems)
                {
                    
                    await using (var existsCmd = new SqlCommand(existsSql, conn, (SqlTransaction)tx))
                    {
                        existsCmd.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 120) { Value = it.Name });

                        int count = (int)await existsCmd.ExecuteScalarAsync();

                        if (count > 0)
                            continue;
                    }

                    
                    await using (var insCmd = new SqlCommand(insertSql, conn, (SqlTransaction)tx))
                    {
                        insCmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = Guid.NewGuid() });
                        insCmd.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 120) { Value = it.Name });
                        insCmd.Parameters.Add(new SqlParameter("@cat", SqlDbType.NVarChar, 60) { Value = it.Category });

                        await insCmd.ExecuteNonQueryAsync();
                        inserted++;
                    }
                }

                await tx.CommitAsync();

                TempData["Flash"] = inserted == 0
                    ? "Starter items already exist."
                    : $"Added {inserted} starter items.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding starter items");
                TempData["Flash"] = "Could not seed items. Please check your dbo.Items table schema.";
            }

            return RedirectToAction(nameof(Index));
        }

        private string GetConnectionString()
        {
            return _config.GetConnectionString("DefaultConnection");
        }
    }
}
