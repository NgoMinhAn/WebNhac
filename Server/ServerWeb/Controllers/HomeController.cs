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
        // Đường dẫn: /Home/Admin
        public IActionResult Admin()
        {
            return RedirectToAction("QuanLyBaiHat");
        }

        // 3. Quản lý bài hát (Trỏ trực tiếp vào thư mục Views/Admin)
        // Đường dẫn: /Home/Admin/QuanLyBaiHat
        // Chỉ những ai có Role là 'Admin' mới được vào các hàm này
        [Authorize(Roles = "Admin")]
        [Route("Home/Admin/QuanLyBaiHat")]
        public IActionResult QuanLyBaiHat()
        {
            var songs = _dbContext.Songs.ToList();
            return View("~/Views/Admin/QuanLyBaiHat.cshtml", songs);
        }

        [Authorize(Roles = "Admin")]
        [Route("Home/Admin/QuanLyNguoiDung")]
        public IActionResult QuanLyNguoiDung()
        {
            var users = _dbContext.Users.ToList();
            return View("~/Views/Admin/QuanLyNguoiDung.cshtml", users);
        }
    
        // --- CÁC HÀM XỬ LÝ DỮ LIỆU (POST/API) ---

        public class SongIdRequest { public int Id { get; set; } }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteSong([FromBody] SongIdRequest request)
        {
            var song = await _dbContext.Songs.FindAsync(request.Id);
            if (song == null) return Json(new { success = false, message = "Bài hát không tồn tại" });

            // Xóa file vật lý
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
        public IActionResult UploadSong()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadSong(string name, string author, string album, string genre, IFormFile file, IFormFile? cover, string? duration)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (string.IsNullOrWhiteSpace(name) || file == null)
            {
                if (isAjax) return Json(new { success = false, message = "Thiếu thông tin." });
                return View();
            }

            // Lưu file
            var fileName = Path.GetFileName(file.FileName);
            var musicPath = Path.Combine(_env.WebRootPath, "Music", fileName);
            using (var stream = new FileStream(musicPath, FileMode.Create)) { await file.CopyToAsync(stream); }

            string? coverPath = null;
            if (cover != null)
            {
                var coverName = Path.GetFileName(cover.FileName);
                var coverFullPath = Path.Combine(_env.WebRootPath, "Covers", coverName);
                using (var stream = new FileStream(coverFullPath, FileMode.Create)) { await cover.CopyToAsync(stream); }
                coverPath = $"/Covers/{coverName}";
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

            if (!string.IsNullOrEmpty(duration))
            {
                if (TimeSpan.TryParseExact(duration.Trim(), new[] { "m\\:ss", "mm\\:ss", "h\\:mm\\:ss", "hh\\:mm\\:ss" }, null, out var parsedDuration))
                {
                    song.Duration = parsedDuration;
                }
            }

            _dbContext.Songs.Add(song);
            await _dbContext.SaveChangesAsync();

            if (isAjax) return Json(new { success = true, song });

            return RedirectToAction("QuanLyBaiHat");
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