using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ServerWeb.Data;
using ServerWeb.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ServerWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _dbContext;
        private readonly IWebHostEnvironment _env;

        public HomeController(ILogger<HomeController> logger, AppDbContext dbContext, IWebHostEnvironment env)
        {
            _logger = logger;
            _dbContext = dbContext;
            _env = env;
        }

        // 1. Trang chủ cho người dùng
        public IActionResult Index()
        {
            var songs = _dbContext.Songs.OrderBy(s => s.Id).ToList();
            return View(songs);
        }

        // 2. Trang Quản lý chính (Admin)
        public IActionResult Admin()
        {
            return RedirectToAction("QuanLyBaiHat");
        }

        // 3. Quản lý bài hát (Admin)
        [Authorize(Roles = "Admin")]
        [Route("Home/Admin/QuanLyBaiHat")]
        public IActionResult QuanLyBaiHat()
        {
            var songs = _dbContext.Songs.ToList();
            return View("~/Views/Admin/QuanLyBaiHat.cshtml", songs);
        }

        // 4. Quản lý người dùng (Admin)
        [Authorize(Roles = "Admin")]
        [Route("Home/Admin/QuanLyNguoiDung")]
        public IActionResult QuanLyNguoiDung()
        {
            var users = _dbContext.Users.ToList();
            return View("~/Views/Admin/QuanLyNguoiDung.cshtml", users);
        }

        // --- CÁC HÀM XỬ LÝ DỮ LIỆU BÀI HÁT ---

        public class SongIdRequest { public int Id { get; set; } }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteSong([FromBody] SongIdRequest request)
        {
            var song = await _dbContext.Songs.FindAsync(request.Id);
            if (song == null) return Json(new { success = false, message = "Bài hát không tồn tại" });

            DeletePhysicalFile(song.FilePath);
            DeletePhysicalFile(song.CoverPath);

            _dbContext.Songs.Remove(song);
            await _dbContext.SaveChangesAsync();
            return Json(new { success = true, message = "Xóa bài hát thành công." });
        }

        [HttpPost]
        public async Task<IActionResult> EditSong(int id, string name, string author, string album, string genre, string? duration)
        {
            var song = await _dbContext.Songs.FindAsync(id);
            if (song == null) return Json(new { success = false, message = "Bài hát không tồn tại" });

            song.Name = name;
            song.Author = author;
            song.Album = album;
            song.Genre = genre;

            if (!string.IsNullOrEmpty(duration))
            {
                if (TimeSpan.TryParseExact(duration.Trim(), new[] { "m\\:ss", "mm\\:ss", "h\\:mm\\:ss", "hh\\:mm\\:ss" }, null, out var parsedDuration))
                {
                    song.Duration = parsedDuration;
                }
            }

            await _dbContext.SaveChangesAsync();
            return Json(new { success = true, message = "Cập nhật thành công.", song = new { song.Id, song.Name, song.Author, song.Album, song.Genre, duration = song.Duration.ToString(@"mm\:ss"), song.CoverPath } });
        }

        [HttpGet]
        public IActionResult UploadSong() => View();

        [HttpPost]
        public async Task<IActionResult> UploadSong(string name, string author, string album, string genre, IFormFile file, IFormFile? coverFile, string? duration)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (string.IsNullOrWhiteSpace(name) || file == null)
            {
                if (isAjax) return Json(new { success = false, message = "Thiếu thông tin tên bài hát hoặc tệp nhạc." });
                ModelState.AddModelError("", "Tên bài hát và tệp nhạc là bắt buộc.");
                return View();
            }

            try
            {
                string musicFolder = Path.Combine(_env.WebRootPath, "Music");
                string coverFolder = Path.Combine(_env.WebRootPath, "images");

                if (!Directory.Exists(musicFolder)) Directory.CreateDirectory(musicFolder);
                if (!Directory.Exists(coverFolder)) Directory.CreateDirectory(coverFolder);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var musicPath = Path.Combine(musicFolder, fileName);
                using (var stream = new FileStream(musicPath, FileMode.Create)) { await file.CopyToAsync(stream); }

                string? coverPath = null;
                if (coverFile != null && coverFile.Length > 0)
                {
                    var coverName = Guid.NewGuid().ToString() + Path.GetExtension(coverFile.FileName);
                    var coverFullPath = Path.Combine(coverFolder, coverName);
                    using (var stream = new FileStream(coverFullPath, FileMode.Create)) { await coverFile.CopyToAsync(stream); }
                    coverPath = $"/images/{coverName}";
                }

                var song = new Song
                {
                    Name = name,
                    Author = author,
                    Album = album,
                    Genre = genre,
                    FilePath = $"/Music/{fileName}",
                    CoverPath = coverPath,
                    Duration = TimeSpan.Zero
                };

                if (!string.IsNullOrEmpty(duration) && TimeSpan.TryParseExact(duration.Trim(), new[] { "m\\:ss", "mm\\:ss", "h\\:mm\\:ss", "hh\\:mm\\:ss" }, null, out var parsedDuration))
                {
                    song.Duration = parsedDuration;
                }

                _dbContext.Songs.Add(song);
                await _dbContext.SaveChangesAsync();

                if (isAjax) return Json(new { success = true, song });
                return RedirectToAction("QuanLyBaiHat");
            }
            catch (Exception ex)
            {
                if (isAjax) return Json(new { success = false, message = "Lỗi: " + ex.Message });
                return View();
            }
        }

        // --- CÁC HÀM XỬ LÝ HỒ SƠ NGƯỜI DÙNG (PROFILE) ---

        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName)) return RedirectToAction("Index");

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == userName);
            if (user == null) return NotFound();

            return View(user);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrEmpty(userName)) return RedirectToAction("Index");

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == userName);
            if (user == null) return NotFound();

            return View(user);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(User model, IFormFile? avatarFile)
        {
            // 1. Tìm user trong DB bằng ID truyền từ Form ẩn
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == model.Id);

            if (user == null)
            {
                // Nếu vào đây là do <input type="hidden" asp-for="Id" /> của bạn bị sai
                return NotFound("Không tìm thấy User với ID: " + model.Id);
            }

            // 2. Gán dữ liệu mới (Chỉ gán nếu có dữ liệu để tránh ghi đè NULL)
            user.Username = model.Username ?? user.Username;
            user.Bio = model.Bio;

            // Nếu Form có gửi Email lên thì cập nhật, không thì giữ nguyên bản cũ trong DB
            if (!string.IsNullOrEmpty(model.Email))
            {
                user.Email = model.Email;
            }

            // 3. Xử lý Ảnh
            if (avatarFile != null && avatarFile.Length > 0)
            {
                string folder = Path.Combine(_env.WebRootPath, "images");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatarFile.FileName);
                string filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(stream);
                }
                user.AvatarUrl = "/images/" + fileName;
            }

            // 4. Lưu - Bỏ qua ModelState check tạm thời để test xem có lưu được không
            try
            {
                _dbContext.Users.Update(user);
                await _dbContext.SaveChangesAsync();
                return RedirectToAction("Profile"); // Lưu xong quay về trang cá nhân
            }
            catch (Exception ex)
            {
                // Nếu có lỗi SQL, nó sẽ hiện ra ở đây
                ModelState.AddModelError("", "Lỗi lưu DB: " + ex.InnerException?.Message ?? ex.Message);
                return View(model);
            }
        }

        private void DeletePhysicalFile(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var fullPath = Path.Combine(_env.WebRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
            }
            catch { }
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}