using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ServerWeb.Data;
using ServerWeb.Models;
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
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return HandleResponse(false, "Dữ liệu không hợp lệ!", "Register");

            if (model.Password != model.ConfirmPassword)
                return HandleResponse(false, "Mật khẩu xác nhận không khớp!", "Register");

            var existingUser = _context.Users.FirstOrDefault(u => u.Email == model.Email || u.Username == model.Username);
            if (existingUser != null)
                return HandleResponse(false, "Email hoặc tên người dùng đã tồn tại!", "Register");

            var user = new User
            {
                Email = model.Email!,
                Username = model.Username!,
                PasswordHash = HashPassword(model.Password!),
                PhoneNumber = model.PhoneNumber,
                Role = "User"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Đăng ký xong cho đăng nhập luôn để về trang chủ
            await SignInUser(user);

            return HandleResponse(true, "Đăng ký thành công!", "Index", "Home");
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
                return HandleResponse(false, "Vui lòng nhập đầy đủ thông tin!", "Login");

            var user = _context.Users.FirstOrDefault(u => u.Email == model.Email || u.Username == model.Email);

            if (user != null && VerifyPassword(model.Password, user.PasswordHash))
            {
                await SignInUser(user);

                // Đăng nhập xong quay về trang chủ
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

        private bool VerifyPassword(string password, string hash)
            => HashPassword(password) == hash;

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