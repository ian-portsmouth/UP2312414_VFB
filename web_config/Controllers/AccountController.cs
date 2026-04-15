using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using FoodBankApp.Models;
using Microsoft.AspNetCore.Authorization;

namespace FoodBankApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _config;

        public AccountController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            var vm = new LoginViewModel
            {
                ReturnUrl = returnUrl
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string connectionString = _config.GetConnectionString("DefaultConnection");
            string? role = null;

            try
            {
                await using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                const string sql = "SELECT PasswordHash, Role, IsActive FROM dbo.Users WHERE Username = @u";

                await using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.Add(new SqlParameter("@u", SqlDbType.NVarChar, 50) { Value = model.Username });

                await using SqlDataReader rdr = await cmd.ExecuteReaderAsync();

                if (!await rdr.ReadAsync())
                {
                    model.Error = "Invalid username or password.";
                    return View(model);
                }

                string storedHash = rdr.GetString(0);
                role = rdr.IsDBNull(1) ? "Staff" : rdr.GetString(1);

                bool isActive = !rdr.IsDBNull(2) && rdr.GetBoolean(2);
                if (!isActive)
                {
                    model.Error = "This account is inactive.";
                    return View(model);
                }

                string inputHash = Sha256(model.Password);

                if (!string.Equals(storedHash, inputHash, StringComparison.OrdinalIgnoreCase))
                {
                    model.Error = "Invalid username or password.";
                    return View(model);
                }
            }
            catch
            {
                model.Error = "Login error. Please try again.";
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, model.Username),
                new Claim(ClaimTypes.Role, role ?? "Staff")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "StaffOrders");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        private static string Sha256(string input)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));

            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
