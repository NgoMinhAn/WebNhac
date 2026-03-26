using Microsoft.AspNetCore.Mvc;
using ServerWeb.Data;
using ServerWeb.Models;
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

        // GET: Auth/Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: Auth/Register
        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Validate that required fields are not null
                if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
                {
                    ModelState.AddModelError("", "Email, tên người dùng, và mật khẩu là bắt buộc!");
                    return HandleAjaxResponse(false, "Email, tên người dùng, và mật khẩu là bắt buộc!");
                }

                // Check if user already exists
                var existingUser = _context.Users.FirstOrDefault(u => u.Email == model.Email || u.Username == model.Username);
                if (existingUser != null)
                {
                    return HandleAjaxResponse(false, "Email hoặc tên người dùng đã tồn tại!");
                }

                // Create new user
                var user = new User
                {
                    Email = model.Email,
                    Username = model.Username,
                    PasswordHash = HashPassword(model.Password),
                    PhoneNumber = model.PhoneNumber
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Store user info in session
                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("Username", user.Username ?? "");
                HttpContext.Session.SetString("Email", user.Email ?? "");

                return HandleAjaxResponse(true, "Đăng ký thành công!");
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return HandleAjaxResponse(false, string.Join(" ", errors));
        }

        // GET: Auth/Login
        public IActionResult Login()
        {
            return View();
        }

        // POST: Auth/Login
        [HttpPost]
        public IActionResult Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Validate that required fields are not null
                if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
                {
                    return HandleAjaxResponse(false, "Email/Tên người dùng và mật khẩu là bắt buộc!");
                }

                var user = _context.Users.FirstOrDefault(u => u.Email == model.Email || u.Username == model.Email);
                
                if (user != null && VerifyPassword(model.Password, user.PasswordHash))
                {
                    // Store user info in session
                    HttpContext.Session.SetInt32("UserId", user.Id);
                    HttpContext.Session.SetString("Username", user.Username ?? "");
                    HttpContext.Session.SetString("Email", user.Email ?? "");

                    return HandleAjaxResponse(true, "Đăng nhập thành công!");
                }

                return HandleAjaxResponse(false, "Email/Tên người dùng hoặc mật khẩu không chính xác!");
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return HandleAjaxResponse(false, string.Join(" ", errors));
        }

        // GET: Auth/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        // Helper method to hash password
        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        // Helper method to verify password
        private bool VerifyPassword(string password, string hash)
        {
            var hashOfInput = HashPassword(password);
            return hashOfInput.Equals(hash);
        }

        // Helper method to handle AJAX responses
        private IActionResult HandleAjaxResponse(bool success, string message)
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success, message });
            }
            else
            {
                if (success)
                {
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    TempData["Error"] = message;
                    return RedirectToAction("Index", "Home");
                }
            }
        }
    }

    // View Models
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
