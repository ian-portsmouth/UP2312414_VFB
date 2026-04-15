using FoodBankApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace FoodBankApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly IConfiguration _config;

        public UsersController(IConfiguration config)
        {
            _config = config;
        }

        private string GetConnectionString() => _config.GetConnectionString("DefaultConnection");

        private static readonly string[] AllowedRoles = new[] { "Staff", "Admin" };

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var users = new List<UserListItem>();

            try
            {
                await using var conn = new SqlConnection(GetConnectionString());
                await conn.OpenAsync();

                const string sql = @"
SELECT UserID, Username, Role, Email, IsActive
FROM dbo.Users
ORDER BY Username;";

                await using var cmd = new SqlCommand(sql, conn);

                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    users.Add(new UserListItem
                    {
                        UserId = rdr.GetGuid(0),
                        Username = rdr.GetString(1),
                        Role = rdr.IsDBNull(2) ? "Staff" : rdr.GetString(2),
                        Email = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                        IsActive = !rdr.IsDBNull(4) && rdr.GetBoolean(4)
                    });
                }
            }
            catch
            {
                
                TempData["Flash"] = "Could not load users.";
            }

            return View(users);
        }
        
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Roles = AllowedRoles;
            return View(new UserCreateViewModel { Role = "Staff", IsActive = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserCreateViewModel model)
        {
            ViewBag.Roles = AllowedRoles;

            if (!ModelState.IsValid)
                return View(model);

            string username = (model.Username ?? string.Empty).Trim();

            if (username.Length == 0)
            {
                model.Error = "Username is required.";
                return View(model);
            }

            if (!AllowedRoles.Contains(model.Role))
            {
                model.Error = "Invalid role.";
                return View(model);
            }

            try
            {
                await using var conn = new SqlConnection(GetConnectionString());
                await conn.OpenAsync();

                const string existsSql = "SELECT COUNT(1) FROM dbo.Users WHERE Username = @u;";
                await using (var existsCmd = new SqlCommand(existsSql, conn))
                {
                    existsCmd.Parameters.Add(new SqlParameter("@u", SqlDbType.NVarChar, 50) { Value = username });
                    int count = Convert.ToInt32(await existsCmd.ExecuteScalarAsync());
                    if (count > 0)
                    {
                        model.Error = "That username already exists.";
                        return View(model);
                    }
                }

                const string insertSql = @"
INSERT INTO dbo.Users (UserID, Username, PasswordHash, Role, Email, IsActive)
VALUES (@id, @u, @ph, @r, @e, @a);";

                await using (var cmd = new SqlCommand(insertSql, conn))
                {
                    cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = Guid.NewGuid() });
                    cmd.Parameters.Add(new SqlParameter("@u", SqlDbType.NVarChar, 50) { Value = username });
                    cmd.Parameters.Add(new SqlParameter("@ph", SqlDbType.NVarChar, 200) { Value = Sha256(model.Password) });
                    cmd.Parameters.Add(new SqlParameter("@r", SqlDbType.NVarChar, 20) { Value = model.Role });
                    cmd.Parameters.Add(new SqlParameter("@e", SqlDbType.NVarChar, 100) { Value = (object?)model.Email?.Trim() ?? DBNull.Value });
                    cmd.Parameters.Add(new SqlParameter("@a", SqlDbType.Bit) { Value = model.IsActive });

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch
            {
                model.Error = "Could not create user. Please try again.";
                return View(model);
            }

            TempData["Flash"] = "User created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(Guid id)
        {
            ViewBag.Roles = AllowedRoles;

            try
            {
                await using var conn = new SqlConnection(GetConnectionString());
                await conn.OpenAsync();

                const string sql = @"
SELECT UserID, Username, Role, Email, IsActive
FROM dbo.Users
WHERE UserID = @id;";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id });

                await using var rdr = await cmd.ExecuteReaderAsync();
                if (!await rdr.ReadAsync())
                    return NotFound();

                var vm = new UserEditViewModel
                {
                    UserId = rdr.GetGuid(0),
                    Username = rdr.GetString(1),
                    Role = rdr.IsDBNull(2) ? "Staff" : rdr.GetString(2),
                    Email = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                    IsActive = !rdr.IsDBNull(4) && rdr.GetBoolean(4)
                };

                return View(vm);
            }
            catch
            {
                TempData["Flash"] = "Could not load user.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserEditViewModel model)
        {
            ViewBag.Roles = AllowedRoles;

            if (!ModelState.IsValid)
                return View(model);

            if (!AllowedRoles.Contains(model.Role))
            {
                model.Error = "Invalid role.";
                return View(model);
            }

            string? currentUsername = User?.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(currentUsername) &&
                string.Equals(currentUsername, model.Username, StringComparison.OrdinalIgnoreCase))
            {
                if (!model.IsActive)
                {
                    model.Error = "You can't deactivate your own account.";
                    return View(model);
                }

                if (!string.Equals(model.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    model.Error = "You can't remove Admin role from your own account.";
                    return View(model);
                }
            }

            try
            {
                await using var conn = new SqlConnection(GetConnectionString());
                await conn.OpenAsync();

                const string sql = @"
UPDATE dbo.Users
SET Username = @u,
    Role = @r,
    Email = @e,
    IsActive = @a
WHERE UserID = @id;";

                await using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = model.UserId });
                cmd.Parameters.Add(new SqlParameter("@u", SqlDbType.NVarChar, 50) { Value = model.Username.Trim() });
                cmd.Parameters.Add(new SqlParameter("@r", SqlDbType.NVarChar, 20) { Value = model.Role });
                cmd.Parameters.Add(new SqlParameter("@e", SqlDbType.NVarChar, 100) { Value = (object?)model.Email?.Trim() ?? DBNull.Value });
                cmd.Parameters.Add(new SqlParameter("@a", SqlDbType.Bit) { Value = model.IsActive });

                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return NotFound();
            }
            catch
            {
                model.Error = "Could not update user.";
                return View(model);
            }

            TempData["Flash"] = "User updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(Guid id)
        {
            try
            {
                await using var conn = new SqlConnection(GetConnectionString());
                await conn.OpenAsync();

                const string sql = "SELECT UserID, Username FROM dbo.Users WHERE UserID = @id;";
                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = id });

                await using var rdr = await cmd.ExecuteReaderAsync();
                if (!await rdr.ReadAsync())
                    return NotFound();

                var vm = new UserResetPasswordViewModel
                {
                    UserId = rdr.GetGuid(0),
                    Username = rdr.GetString(1)
                };

                return View(vm);
            }
            catch
            {
                TempData["Flash"] = "Could not load user.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(UserResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                await using var conn = new SqlConnection(GetConnectionString());
                await conn.OpenAsync();

                const string sql = "UPDATE dbo.Users SET PasswordHash = @ph WHERE UserID = @id;";
                await using var cmd = new SqlCommand(sql, conn);

                cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = model.UserId });
                cmd.Parameters.Add(new SqlParameter("@ph", SqlDbType.NVarChar, 200) { Value = Sha256(model.NewPassword) });

                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return NotFound();
            }
            catch
            {
                model.Error = "Could not reset password.";
                return View(model);
            }

            TempData["Flash"] = "Password reset.";
            return RedirectToAction(nameof(Index));
        }

        private static string Sha256(string input)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
