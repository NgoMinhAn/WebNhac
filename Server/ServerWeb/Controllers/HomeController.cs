using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ServerWeb.Data;
using ServerWeb.Models;

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

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Admin()
        {
            var songs = _dbContext.Songs
                .OrderBy(s => s.Id)
                .ToList();
            return View(songs);
        }

        public class SongIdRequest { public int Id { get; set; } }

        [HttpPost]
        public async Task<IActionResult> DeleteSong([FromBody] SongIdRequest request)
        {
            var song = await _dbContext.Songs.FindAsync(request.Id);
            if (song == null) return Json(new { success = false, message = "Bài hát không tồn tại" });

            if (!string.IsNullOrEmpty(song.FilePath))
            {
                try
                {
                    var fullPath = Path.Combine(_env.WebRootPath, song.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(song.CoverPath))
            {
                try
                {
                    var fullPath = Path.Combine(_env.WebRootPath, song.CoverPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
                }
                catch { }
            }

            _dbContext.Songs.Remove(song);
            await _dbContext.SaveChangesAsync();
            return Json(new { success = true, message = "Xóa bài hát thành công." });
        }

        [HttpPost]
        public async Task<IActionResult> EditSong(int id, string name, string author, string album, string genre)
        {
            var song = await _dbContext.Songs.FindAsync(id);
            if (song == null) return Json(new { success = false, message = "Bài hát không tồn tại" });

            song.Name = name;
            song.Author = author;
            song.Album = album;
            song.Genre = genre;

            await _dbContext.SaveChangesAsync();
            return Json(new { success = true, message = "Cập nhật bài hát thành công.", song = new { song.Id, song.Name, song.Author, song.Album, song.Genre, duration = song.Duration.ToString(@"mm\:ss"), song.CoverPath } });
        }

        [HttpGet]
        public IActionResult UploadSong()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadSong(string name, string author, string album, string genre, IFormFile file, IFormFile? cover)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(album) || string.IsNullOrWhiteSpace(genre) || file == null || file.Length == 0)
            {
                if (isAjax)
                {
                    return Json(new { success = false, message = "Vui lòng điền đầy đủ thông tin và chọn tệp âm thanh." });
                }

                ModelState.AddModelError(string.Empty, "Vui lòng điền đầy đủ thông tin và chọn tệp âm thanh.");
                return View();
            }

            var musicFolder = Path.Combine(_env.WebRootPath, "Music");
            Directory.CreateDirectory(musicFolder);

            var fileName = Path.GetFileName(file.FileName);
            var filePath = Path.Combine(musicFolder, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            string? coverPath = null;
            if (cover != null && cover.Length > 0)
            {
                var coversFolder = Path.Combine(_env.WebRootPath, "Covers");
                Directory.CreateDirectory(coversFolder);
                var coverFileName = Path.GetFileName(cover.FileName);
                var coverFilePath = Path.Combine(coversFolder, coverFileName);
                using var coverStream = new FileStream(coverFilePath, FileMode.Create);
                await cover.CopyToAsync(coverStream);
                coverPath = $"/Covers/{coverFileName}";
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

            _dbContext.Songs.Add(song);
            await _dbContext.SaveChangesAsync();

            if (isAjax)
            {
                var jsonSong = new
                {
                    song.Id,
                    song.Name,
                    song.Author,
                    song.Album,
                    song.Genre,
                    duration = song.Duration.ToString(@"mm\:ss"),
                    song.CoverPath
                };
                return Json(new { success = true, message = "Thêm bài hát thành công.", song = jsonSong });
            }

            TempData["SuccessMessage"] = "Thêm bài hát thành công.";
            return RedirectToAction("Admin");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
