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
using System.Security.Claims;
using System.Collections.Generic;

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

        // --- Action Discovery mới thêm vào ---
        public IActionResult Discovery()
        {
            var songs = _dbContext.Songs.OrderByDescending(s => s.Id).ToList();
            ViewBag.Genres = _dbContext.Songs
                .Where(s => !string.IsNullOrWhiteSpace(s.Genre))
                .Select(s => s.Genre)
                .Distinct()
                .OrderBy(g => g)
                .ToList();
            ViewBag.TopArtists = _dbContext.Songs
                .Where(s => !string.IsNullOrWhiteSpace(s.Author))
                .Select(s => s.Author)
                .Distinct()
                .OrderBy(a => a)
                .Take(12)
                .ToList();
            ViewBag.SearchQuery = string.Empty;
            ViewBag.SelectedGenre = string.Empty;
            ViewBag.PageTitle = "Khám phá";
            return View(songs);
        }

        public IActionResult Search(string query, string genre)
        {
            var songsQuery = _dbContext.Songs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(query))
            {
                var lower = query.Trim().ToLower();
                songsQuery = songsQuery.Where(s =>
                    (!string.IsNullOrEmpty(s.Name) && s.Name.ToLower().Contains(lower)) ||
                    (!string.IsNullOrEmpty(s.Author) && s.Author.ToLower().Contains(lower)) ||
                    (!string.IsNullOrEmpty(s.Album) && s.Album.ToLower().Contains(lower)) ||
                    (!string.IsNullOrEmpty(s.Genre) && s.Genre.ToLower().Contains(lower)));
            }
            if (!string.IsNullOrWhiteSpace(genre))
            {
                var selectedGenre = genre.Trim();
                songsQuery = songsQuery.Where(s => s.Genre == selectedGenre);
            }

            var songs = songsQuery.OrderByDescending(s => s.Id).ToList();
            ViewBag.Genres = _dbContext.Songs
                .Where(s => !string.IsNullOrWhiteSpace(s.Genre))
                .Select(s => s.Genre)
                .Distinct()
                .OrderBy(g => g)
                .ToList();
            ViewBag.TopArtists = _dbContext.Songs
                .Where(s => !string.IsNullOrWhiteSpace(s.Author))
                .GroupBy(s => s.Author)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(12)
                .ToList();
            ViewBag.SearchQuery = query ?? string.Empty;
            ViewBag.SelectedGenre = genre ?? string.Empty;
            ViewBag.PageTitle = string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(genre) ? "Khám phá" : "Kết quả tìm kiếm";
            return View("Discovery", songs);
        }

        public IActionResult Artist(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return RedirectToAction("Discovery");

            var artistName = name.Trim();
            var songs = _dbContext.Songs
                .Where(s => s.Author == artistName)
                .OrderByDescending(s => s.Id)
                .ToList();

            if (!songs.Any()) return NotFound();

            ViewBag.ArtistName = artistName;
            ViewBag.ArtistSongCount = songs.Count;
            ViewBag.Genres = _dbContext.Songs
                .Where(s => !string.IsNullOrWhiteSpace(s.Genre))
                .Select(s => s.Genre)
                .Distinct()
                .OrderBy(g => g)
                .ToList();
            ViewBag.TopArtists = _dbContext.Songs
                .Where(s => !string.IsNullOrWhiteSpace(s.Author))
                .GroupBy(s => s.Author)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(12)
                .ToList();
            ViewBag.SearchQuery = string.Empty;
            ViewBag.SelectedGenre = string.Empty;
            ViewBag.PageTitle = $"Nghệ sĩ: {artistName}";
            return View(songs);
        }

        [Authorize]
        public IActionResult Library()
        {
            ViewData["Title"] = "Thư viện";
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return RedirectToAction("Login", "Auth");
            }
            int userId = int.Parse(userIdClaim.Value);
            var likedPlaylist = _dbContext.Playlists.FirstOrDefault(p => p.UserId == userId && p.Name == "Liked");
            List<Song> likedSongs = new List<Song>();
            if (likedPlaylist != null)
            {
                likedSongs = _dbContext.PlaylistSongs
                    .Where(ps => ps.PlaylistId == likedPlaylist.Id)
                    .Include(ps => ps.Song)
                    .GroupBy(ps => ps.SongId)
                    .Select(g => g.First().Song)
                    .ToList();
            }
            ViewBag.LikedSongs = likedSongs;
            var songs = _dbContext.Songs.OrderBy(s => s.Id).ToList();
            System.Diagnostics.Debug.WriteLine($"Library: Found {songs.Count} songs in database");
            foreach (var song in songs)
            {
                System.Diagnostics.Debug.WriteLine($"Song ID: {song.Id}, Name: {song.Name}");
            }
            return View(songs);
        }

        public class ToggleLikeRequest
        {
            public int SongId { get; set; }
        }

        [HttpPost]
        [Authorize]
        [IgnoreAntiforgeryToken]
        public IActionResult ToggleLike([FromBody] ToggleLikeRequest request)
        {
            int songId = request?.SongId ?? 0;
            _logger.LogInformation("ToggleLike called with songId: {SongId}", songId);
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                _logger.LogWarning("User not authenticated");
                return Json(new { success = false, message = "User not authenticated" });
            }
            int userId = int.Parse(userIdClaim.Value);
            _logger.LogInformation("User ID: {UserId}", userId);

            // Check if song exists
            _logger.LogInformation("Looking for song with ID: {SongId} (type: {SongIdType})", songId, songId.GetType());

            // Try multiple ways to find the song
            var song = _dbContext.Songs.Find(songId);
            _logger.LogInformation("Song.Find result: {Song}", song?.Name ?? "null");

            if (song == null)
            {
                _logger.LogInformation("Trying FirstOrDefault...");
                song = _dbContext.Songs.FirstOrDefault(s => s.Id == songId);
                _logger.LogInformation("FirstOrDefault result: {Song}", song?.Name ?? "null");
            }

            if (song == null)
            {
                _logger.LogInformation("Trying Where + FirstOrDefault...");
                song = _dbContext.Songs.Where(s => s.Id == songId).FirstOrDefault();
                _logger.LogInformation("Where + FirstOrDefault result: {Song}", song?.Name ?? "null");
            }

            if (song == null)
            {
                _logger.LogInformation("Trying AsNoTracking...");
                song = _dbContext.Songs.AsNoTracking().FirstOrDefault(s => s.Id == songId);
                _logger.LogInformation("AsNoTracking result: {Song}", song?.Name ?? "null");
            }

            if (song == null)
            {
                _logger.LogError("All queries failed for songId: {SongId}", songId);
                var allSongs = _dbContext.Songs.ToList();
                _logger.LogInformation("Available songs in database: {Count}", allSongs.Count);
                foreach (var s in allSongs)
                {
                    _logger.LogInformation("  ID: {SongId}, Name: {SongName}", s.Id, s.Name);
                }
                return Json(new { success = false, message = $"Song not found. Available songs: {string.Join(", ", allSongs.Select(s => $"{s.Id}"))}" });
            }
            _logger.LogInformation("Song found: {SongName}", song.Name);

            var likedPlaylist = _dbContext.Playlists.FirstOrDefault(p => p.UserId == userId && p.Name == "Liked");
            if (likedPlaylist == null)
            {
                System.Diagnostics.Debug.WriteLine("Creating new Liked playlist");
                likedPlaylist = new Playlist { Name = "Liked", UserId = userId };
                _dbContext.Playlists.Add(likedPlaylist);
                _dbContext.SaveChanges();
            }

            var existingEntries = _dbContext.PlaylistSongs
                .Where(ps => ps.PlaylistId == likedPlaylist.Id && ps.SongId == songId)
                .ToList();

            if (existingEntries.Any())
            {
                // Unlike
                System.Diagnostics.Debug.WriteLine("Unliking song");
                _dbContext.PlaylistSongs.RemoveRange(existingEntries);
                _dbContext.SaveChanges();
                return Json(new { success = true, liked = false, message = "Song unliked" });
            }
            else
            {
                // Like
                System.Diagnostics.Debug.WriteLine("Liking song");
                var playlistSong = new PlaylistSong { PlaylistId = likedPlaylist.Id, SongId = songId };
                _dbContext.PlaylistSongs.Add(playlistSong);
                _dbContext.SaveChanges();
                return Json(new { success = true, liked = true, message = "Song liked" });
            }
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

        // --- CÁC HÀM XỬ LÝ UPLOAD BÀI HÁT (CHỈ ADMIN) ---

        [Authorize(Roles = "Admin")] // Chặn User thường vào xem giao diện upload
        [HttpGet]
        public IActionResult UploadSong() => View();

        [Authorize(Roles = "Admin")] // Chặn User thường cố tình gửi file qua API/Postman
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

        public IActionResult AccessDenied()
        {
            return View();
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}