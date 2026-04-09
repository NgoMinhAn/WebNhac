using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ServerWeb.Data;
using ServerWeb.Models;
using ServerWeb.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ServerWeb.Controllers
{
    public class AuthController : Controller
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register([FromForm] RegisterViewModel model)
        {
            try
            {
                model.Email ??= Request.Form["Email"].FirstOrDefault();
                model.Username ??= Request.Form["Username"].FirstOrDefault();
                model.Password ??= Request.Form["Password"].FirstOrDefault();
                model.ConfirmPassword ??= Request.Form["ConfirmPassword"].FirstOrDefault();
                model.PhoneNumber ??= Request.Form["PhoneNumber"].FirstOrDefault();

                if (!ModelState.IsValid)
                    return HandleResponse(false, "Dữ liệu không hợp lệ!", "Register");

                if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
                    return HandleResponse(false, "Vui lòng nhập đầy đủ thông tin!", "Register");

                if (model.Password != model.ConfirmPassword)
                    return HandleResponse(false, "Mật khẩu xác nhận không khớp!", "Register");

                // Support mixed legacy/current schemas in the same collection.
                var usersCollection = _context.Users.Database.GetCollection<BsonDocument>("users");
                var normalizedEmail = model.Email.Trim();
                var normalizedUsername = model.Username.Trim();

                var combinedFilter = Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Eq("Email", normalizedEmail),
                    Builders<BsonDocument>.Filter.Eq("email", normalizedEmail),
                    Builders<BsonDocument>.Filter.Eq("Username", normalizedUsername),
                    Builders<BsonDocument>.Filter.Eq("username", normalizedUsername)
                );

                var existingUser = await usersCollection.Find(combinedFilter).FirstOrDefaultAsync();
                if (existingUser != null)
                    return HandleResponse(false, "Email hoặc tên người dùng đã tồn tại!", "Register");

                var userId = ObjectId.GenerateNewId().ToString();
                var hashedPassword = HashPassword(model.Password);
                var now = DateTime.UtcNow;

                var userDoc = new BsonDocument
                {
                    { "_id", ObjectId.Parse(userId) },
                    { "Email", normalizedEmail },
                    { "email", normalizedEmail },
                    { "Username", normalizedUsername },
                    { "username", normalizedUsername },
                    { "PasswordHash", hashedPassword },
                    { "password", hashedPassword },
                    { "PhoneNumber", string.IsNullOrWhiteSpace(model.PhoneNumber) ? BsonNull.Value : model.PhoneNumber.Trim() },
                    { "phoneNumber", string.IsNullOrWhiteSpace(model.PhoneNumber) ? BsonNull.Value : model.PhoneNumber.Trim() },
                    { "Role", "User" },
                    { "role", "User" },
                    { "IsActive", true },
                    { "isActive", true },
                    { "CreatedAt", now },
                    { "createdAt", now },
                    { "UpdatedAt", now },
                    { "updatedAt", now }
                };

                await usersCollection.InsertOneAsync(userDoc);

                // Sign in after registration
                var user = new User
                {
                    Id = userId,
                    Email = normalizedEmail,
                    Username = normalizedUsername,
                    Role = "User"
                };
                await SignInUser(user);

                return HandleResponse(true, "Đăng ký thành công!", "Index", "Home");
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                return HandleResponse(false, "Email hoặc tên người dùng đã tồn tại!", "Register");
            }
            catch
            {
                return HandleResponse(false, "Đăng ký thất bại, vui lòng thử lại.", "Register");
            }
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
                return HandleResponse(false, "Vui lòng nhập đầy đủ thông tin!", "Login");

            // Find by email or username while supporting mixed schemas.
            var usersCollection = _context.Users.Database.GetCollection<BsonDocument>("users");
            var loginValue = model.Email.Trim();
            var lookupFilter = Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq("Email", loginValue),
                Builders<BsonDocument>.Filter.Eq("email", loginValue),
                Builders<BsonDocument>.Filter.Eq("Username", loginValue),
                Builders<BsonDocument>.Filter.Eq("username", loginValue)
            );

            var userDoc = await usersCollection.Find(lookupFilter).FirstOrDefaultAsync();
            if (userDoc != null)
            {
                var passwordHash = GetStringField(userDoc, "PasswordHash", "password");
                if (!VerifyPassword(model.Password, passwordHash))
                    return HandleResponse(false, "Tài khoản hoặc mật khẩu không chính xác!", "Login");

                var objectId = userDoc.GetValue("_id", BsonNull.Value);
                var user = new User
                {
                    Id = objectId.IsObjectId ? objectId.AsObjectId.ToString() : userDoc.GetValue("Id", "").ToString(),
                    Email = GetStringField(userDoc, "Email", "email"),
                    Username = GetStringField(userDoc, "Username", "username"),
                    Role = GetStringField(userDoc, "Role", "role") ?? "User"
                };

                await SignInUser(user);

                // Return to home after login
                return HandleResponse(true, "Đăng nhập thành công!", "Index", "Home");
            }

            return HandleResponse(false, "Tài khoản hoặc mật khẩu không chính xác!", "Login");
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied() => View();

        // --- Helpers ---

        private async Task SignInUser(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username ?? ""),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Role, user.Role ?? "User")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties { IsPersistent = true };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }

        private string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        private bool VerifyPassword(string password, string? hash)
            => !string.IsNullOrWhiteSpace(hash) && HashPassword(password) == hash;

        private string? GetStringField(BsonDocument doc, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (doc.TryGetValue(key, out var value) && !value.IsBsonNull)
                {
                    return value.ToString();
                }
            }

            return null;
        }

        // Hàm xử lý phản hồi thông minh: Nếu là Ajax (Modal) thì trả về Json, nếu là Form thường thì Redirect
        private IActionResult HandleResponse(bool success, string message, string action, string controller = "Auth")
        {
            // Kiểm tra xem yêu cầu có phải từ Ajax (Modal) không
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success, message, redirectUrl = Url.Action(action, controller) });
            }

            if (!success) TempData["Error"] = message;
            return RedirectToAction(action, controller);
        }
    }

    public class RegisterViewModel
    {
        public string? Email { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class LoginViewModel
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
    }
}