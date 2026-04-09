using Microsoft.AspNetCore.Mvc;
using ServerWeb.Data;
using ServerWeb.Models;
using ServerWeb.Services;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace ServerWeb.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _dbContext;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<HomeController> _logger;

        public HomeController(AppDbContext dbContext, IWebHostEnvironment webHostEnvironment, ILogger<HomeController> logger)
        {
            _dbContext = dbContext;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        // Main view displaying all songs
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var songs = await _dbContext.Songs.Find(Builders<Song>.Filter.Empty)
                    .SortByDescending(s => s.Id)
                    .ToListAsync();
                return View(songs);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi tải danh sách bài hát: {ex.Message}");
                return View(new List<Song>());
            }
        }

        // Discovery page - Featured songs and playlists
        [HttpGet]
        public async Task<IActionResult> Discovery()
        {
            try
            {
                var songs = await _dbContext.Songs.Find(Builders<Song>.Filter.Empty)
                    .SortByDescending(s => s.Id)
                    .Limit(20)
                    .ToListAsync();

                var playlists = await _dbContext.Playlists.Find(p => !p.IsPrivate)
                    .SortByDescending(p => p.CreatedAt)
                    .Limit(10)
                    .ToListAsync();

                var topArtists = songs
                    .Where(s => !string.IsNullOrWhiteSpace(s.Author))
                    .GroupBy(s => s.Author)
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Key)
                    .Take(8)
                    .Select(g => g.Key)
                    .ToList();

                var genres = await _dbContext.Songs.Find(Builders<Song>.Filter.Empty)
                    .Project(s => s.Genre)
                    .ToListAsync();

                ViewBag.PageTitle = "Khám phá";
                ViewBag.Genres = genres
                    .Where(g => !string.IsNullOrWhiteSpace(g))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(g => g)
                    .ToList();
                ViewBag.TopArtists = topArtists;
                ViewBag.PopularPlaylists = playlists;
                return View(songs);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi tải trang khám phá: {ex.Message}");
                ViewBag.PageTitle = "Khám phá";
                ViewBag.Genres = new List<string>();
                ViewBag.TopArtists = new List<string>();
                ViewBag.PopularPlaylists = new List<Playlist>();
                return View(new List<Song>());
            }
        }

        // Search for songs and playlists
        [HttpGet]
        public async Task<IActionResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return RedirectToAction("Index");
            }

            try
            {
                var songNameFilter = Builders<Song>.Filter.Regex(s => s.Name, new BsonRegularExpression(query, "i"));
                var songAuthorFilter = Builders<Song>.Filter.Regex(s => s.Author, new BsonRegularExpression(query, "i"));
                var songGenreFilter = Builders<Song>.Filter.Regex(s => s.Genre, new BsonRegularExpression(query, "i"));
                var songAlbumFilter = Builders<Song>.Filter.Regex(s => s.Album, new BsonRegularExpression(query, "i"));

                var combinedSongFilter = Builders<Song>.Filter.Or(
                    songNameFilter, songAuthorFilter, songGenreFilter, songAlbumFilter
                );

                var songs = await _dbContext.Songs.Find(combinedSongFilter)
                    .Limit(50)
                    .ToListAsync();

                var playlistNameFilter = Builders<Playlist>.Filter.Regex(p => p.Name, new BsonRegularExpression(query, "i"));
                var playlistDescFilter = Builders<Playlist>.Filter.Regex(p => p.Description, new BsonRegularExpression(query, "i"));
                var isNotPrivateFilter = Builders<Playlist>.Filter.Eq(p => p.IsPrivate, false);

                var combinedPlaylistFilter = Builders<Playlist>.Filter.And(
                    isNotPrivateFilter,
                    Builders<Playlist>.Filter.Or(playlistNameFilter, playlistDescFilter)
                );

                var playlists = await _dbContext.Playlists.Find(combinedPlaylistFilter)
                    .Limit(20)
                    .ToListAsync();

                ViewBag.SearchQuery = query;
                ViewBag.Songs = songs;
                ViewBag.Playlists = playlists;
                return View();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi tìm kiếm: {ex.Message}");
                ViewBag.SearchQuery = query;
                return View();
            }
        }

        // View songs by artist
        [HttpGet]
        public async Task<IActionResult> Artist(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return RedirectToAction("Index");
            }

            try
            {
                var authorFilter = Builders<Song>.Filter.Eq(s => s.Author, name);
                var songs = await _dbContext.Songs.Find(authorFilter)
                    .SortByDescending(s => s.Id)
                    .ToListAsync();

                if (songs.Count == 0)
                {
                    ViewBag.Message = $"Không tìm thấy bài hát của {name}";
                }

                ViewBag.ArtistName = name;
                ViewBag.SongCount = songs.Count;
                return View(songs);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi: {ex.Message}");
                return View();
            }
        }

        // GET: Upload page
        [HttpGet]
        public IActionResult UploadSong()
        {
            return View(new Song());
        }

        // POST: Upload song
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadSong(string name, string author, string album, string genre, string duration, IFormFile musicFile, IFormFile coverFile)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(author))
                {
                    ModelState.AddModelError("", "Tên bài hát và nghệ sĩ không được để trống");
                    return View();
                }

                if (musicFile == null || musicFile.Length == 0)
                {
                    ModelState.AddModelError("", "Vui lòng chọn tệp nhạc");
                    return View();
                }

                // Parse duration from "mm:ss" format
                TimeSpan songDuration = TimeSpan.Zero;
                if (!string.IsNullOrWhiteSpace(duration))
                {
                    if (TimeSpan.TryParse(duration, out var parsed))
                    {
                        songDuration = parsed;
                    }
                }

                var song = new Song
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    Name = name.Trim(),
                    Author = author.Trim(),
                    Album = album?.Trim() ?? "Không xác định",
                    Genre = genre?.Trim() ?? "Khác",
                    Duration = songDuration
                };

                string musicFileName = Guid.NewGuid().ToString() + Path.GetExtension(musicFile.FileName);
                string musicPath = Path.Combine(_webHostEnvironment.WebRootPath, "Music", musicFileName);

                Directory.CreateDirectory(Path.GetDirectoryName(musicPath));
                using (var stream = new FileStream(musicPath, FileMode.Create))
                {
                    await musicFile.CopyToAsync(stream);
                }
                song.FilePath = "/Music/" + musicFileName;

                if (coverFile != null && coverFile.Length > 0)
                {
                    string coverFileName = Guid.NewGuid().ToString() + Path.GetExtension(coverFile.FileName);
                    string coverPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", coverFileName);

                    Directory.CreateDirectory(Path.GetDirectoryName(coverPath));
                    using (var stream = new FileStream(coverPath, FileMode.Create))
                    {
                        await coverFile.CopyToAsync(stream);
                    }
                    song.CoverPath = "/images/" + coverFileName;
                }

                await _dbContext.Songs.InsertOneAsync(song);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi tải lên: {ex.Message}");
                return View();
            }
        }

        // Like/Unlike song - add to favorites playlist
        [HttpPost]
        public async Task<IActionResult> LikeSong(string songId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Cần đăng nhập" });
                }

                var song = await _dbContext.Songs.Find(Builders<Song>.Filter.Eq(s => s.Id, songId))
                    .FirstOrDefaultAsync();
                if (song == null)
                {
                    return Json(new { success = false, message = "Bài hát không tồn tại" });
                }

                var likesPlaylistFilter = Builders<Playlist>.Filter.And(
                    Builders<Playlist>.Filter.Eq(p => p.UserId, userId),
                    Builders<Playlist>.Filter.Eq(p => p.Name, "Yêu thích")
                );

                var likesPlaylist = await _dbContext.Playlists.Find(likesPlaylistFilter)
                    .FirstOrDefaultAsync();

                if (likesPlaylist == null)
                {
                    likesPlaylist = new Playlist
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        Name = "Yêu thích",
                        Description = "Danh sách bài hát yêu thích",
                        IsPrivate = true,
                        UserId = userId,
                        CreatedAt = DateTime.Now,
                        PlaylistSongs = new List<PlaylistSong>()
                    };
                    await _dbContext.Playlists.InsertOneAsync(likesPlaylist);
                }

                var existingEntry = await _dbContext.PlaylistSongs.Find(
                    Builders<PlaylistSong>.Filter.And(
                        Builders<PlaylistSong>.Filter.Eq(ps => ps.PlaylistId, likesPlaylist.Id),
                        Builders<PlaylistSong>.Filter.Eq(ps => ps.SongId, songId)
                    )
                ).FirstOrDefaultAsync();

                if (existingEntry == null)
                {
                    var playlistSong = new PlaylistSong
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        PlaylistId = likesPlaylist.Id,
                        SongId = songId,
                        AddedAt = DateTime.Now
                    };
                    await _dbContext.PlaylistSongs.InsertOneAsync(playlistSong);
                    return Json(new { success = true, message = "Đã thêm vào yêu thích", liked = true });
                }
                else
                {
                    await _dbContext.PlaylistSongs.DeleteOneAsync(
                        Builders<PlaylistSong>.Filter.Eq(ps => ps.Id, existingEntry.Id)
                    );
                    return Json(new { success = true, message = "Đã xóa khỏi yêu thích", liked = false });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // Add song to playlist
        [HttpPost]
        public async Task<IActionResult> AddToPlaylist(string songId, string playlistId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Cần đăng nhập" });
                }

                var playlist = await _dbContext.Playlists.Find(Builders<Playlist>.Filter.Eq(p => p.Id, playlistId))
                    .FirstOrDefaultAsync();

                if (playlist == null || playlist.UserId != userId)
                {
                    return Json(new { success = false, message = "Danh sách phát không hợp lệ" });
                }

                var song = await _dbContext.Songs.Find(Builders<Song>.Filter.Eq(s => s.Id, songId))
                    .FirstOrDefaultAsync();
                if (song == null)
                {
                    return Json(new { success = false, message = "Bài hát không tồn tại" });
                }

                var existingEntry = await _dbContext.PlaylistSongs.Find(
                    Builders<PlaylistSong>.Filter.And(
                        Builders<PlaylistSong>.Filter.Eq(ps => ps.PlaylistId, playlistId),
                        Builders<PlaylistSong>.Filter.Eq(ps => ps.SongId, songId)
                    )
                ).FirstOrDefaultAsync();

                if (existingEntry != null)
                {
                    return Json(new { success = false, message = "Bài hát đã có trong danh sách" });
                }

                var playlistSong = new PlaylistSong
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    PlaylistId = playlistId,
                    SongId = songId,
                    AddedAt = DateTime.Now
                };

                await _dbContext.PlaylistSongs.InsertOneAsync(playlistSong);
                return Json(new { success = true, message = "Đã thêm vào danh sách phát" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // Get user playlists
        [HttpGet]
        public async Task<IActionResult> GetUserPlaylists()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "Cần đăng nhập" });
                }

                var playlists = await _dbContext.Playlists.Find(Builders<Playlist>.Filter.Eq(p => p.UserId, userId))
                    .ToListAsync();

                return Json(new { success = true, playlists = playlists });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // Songs by genre
        [HttpGet]
        public async Task<IActionResult> ByGenre(string genre)
        {
            if (string.IsNullOrWhiteSpace(genre))
            {
                return RedirectToAction("Index");
            }

            try
            {
                var genreFilter = Builders<Song>.Filter.Eq(s => s.Genre, genre);
                var songs = await _dbContext.Songs.Find(genreFilter)
                    .SortByDescending(s => s.Id)
                    .ToListAsync();

                ViewBag.GenreName = genre;
                ViewBag.SongCount = songs.Count;
                return View(songs);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi: {ex.Message}");
                return View();
            }
        }

        // All genres
        [HttpGet]
        public async Task<IActionResult> Genres()
        {
            try
            {
                var songs = await _dbContext.Songs.Find(Builders<Song>.Filter.Empty).ToListAsync();
                var genres = songs.Where(s => !string.IsNullOrEmpty(s.Genre))
                    .Select(s => s.Genre)
                    .Distinct()
                    .OrderBy(g => g)
                    .ToList();

                return View(genres);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Lỗi tải thể loại: {ex.Message}");
                return View(new List<string>());
            }
        }

        // Delete song (Admin only)
        [HttpPost]
        public async Task<IActionResult> DeleteSong([FromBody] SongIdRequest request)
        {
            try
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole != "Admin")
                {
                    return Json(new { success = false, message = "Bạn không có quyền xóa" });
                }

                var song = await _dbContext.Songs.Find(Builders<Song>.Filter.Eq(s => s.Id, request.Id))
                    .FirstOrDefaultAsync();
                if (song == null)
                {
                    return Json(new { success = false, message = "Bài hát không tồn tại" });
                }

                await _dbContext.PlaylistSongs.DeleteManyAsync(
                    Builders<PlaylistSong>.Filter.Eq(ps => ps.SongId, request.Id)
                );

                if (!string.IsNullOrEmpty(song.FilePath))
                {
                    try
                    {
                        string filePath = Path.Combine(_webHostEnvironment.WebRootPath, song.FilePath.TrimStart("/"[0]));
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(song.CoverPath))
                {
                    try
                    {
                        string coverPath = Path.Combine(_webHostEnvironment.WebRootPath, song.CoverPath.TrimStart("/"[0]));
                        if (System.IO.File.Exists(coverPath))
                        {
                            System.IO.File.Delete(coverPath);
                        }
                    }
                    catch { }
                }

                await _dbContext.Songs.DeleteOneAsync(Builders<Song>.Filter.Eq(s => s.Id, request.Id));

                return Json(new { success = true, message = "Đã xóa bài hát thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        public async Task<IActionResult> Library()
        {
            ViewData["Title"] = "Thư viện";
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var userId = userIdClaim.Value;
            var userFilter = Builders<Playlist>.Filter.Eq(p => p.UserId, userId) 
                & Builders<Playlist>.Filter.Eq(p => p.Name, "Liked");
            var likedPlaylist = await _dbContext.Playlists
                .Find(userFilter)
                .FirstOrDefaultAsync();

            var likedSongs = new List<Song>();
            if (likedPlaylist != null)
            {
                var playlistSongFilter = Builders<PlaylistSong>.Filter.Eq(ps => ps.PlaylistId, likedPlaylist.Id);
                var playlistSongs = await _dbContext.PlaylistSongs
                    .Find(playlistSongFilter)
                    .ToListAsync();

                var songIds = playlistSongs.Select(ps => ps.SongId).Distinct().ToList();
                if (songIds.Count > 0)
                {
                    var songFilter = Builders<Song>.Filter.In(s => s.Id, songIds);
                    likedSongs = await _dbContext.Songs
                        .Find(songFilter)
                        .ToListAsync();
                }
            }

            ViewBag.LikedSongs = likedSongs;
            var allSongs = await _dbContext.Songs
                .Find(_ => true)
                .SortBy(s => s.Id)
                .ToListAsync();

            _logger.LogInformation($"Library: Found {allSongs.Count} songs in database");
            foreach (var song in allSongs)
            {
                _logger.LogInformation($"Song ID: {song.Id}, Name: {song.Name}");
            }

            return View(allSongs);
        }

        public class ToggleLikeRequest
        {
            public string SongId { get; set; }
        }

        [HttpPost]
        [Authorize]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ToggleLike([FromBody] ToggleLikeRequest request)
        {
            try
            {
                var songId = request?.SongId;
                if (string.IsNullOrWhiteSpace(songId))
                {
                    _logger.LogWarning("Invalid song ID");
                    return Json(new { success = false, message = "Song ID is invalid" });
                }

                _logger.LogInformation("ToggleLike called with songId: {SongId}", songId);
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    _logger.LogWarning("User not authenticated");
                    return Json(new { success = false, message = "User not authenticated" });
                }

                var userId = userIdClaim.Value;
                _logger.LogInformation("User ID: {UserId}", userId);

                var song = await _dbContext.Songs.Find(s => s.Id == songId).FirstOrDefaultAsync();
                _logger.LogInformation("Song result: {Song}", song?.Name ?? "null");

                if (song == null)
                {
                    _logger.LogError("Song not found with ID: {SongId}", songId);
                    var allSongs = await _dbContext.Songs.Find(_ => true).ToListAsync();
                    _logger.LogInformation("Available songs in database: {Count}", allSongs.Count);
                    foreach (var s in allSongs)
                    {
                        _logger.LogInformation("  ID: {SongId}, Name: {SongName}", s.Id, s.Name);
                    }
                    return Json(new { success = false, message = $"Song not found" });
                }

                _logger.LogInformation("Song found: {SongName}", song.Name);

                var userFilter = Builders<Playlist>.Filter.Eq(p => p.UserId, userId)
                    & Builders<Playlist>.Filter.Eq(p => p.Name, "Liked");
                var likedPlaylist = await _dbContext.Playlists
                    .Find(userFilter)
                    .FirstOrDefaultAsync();

                if (likedPlaylist == null)
                {
                    _logger.LogInformation("Creating new Liked playlist");
                    likedPlaylist = new Playlist 
                    { 
                        Id = ObjectId.GenerateNewId().ToString(),
                        Name = "Liked", 
                        UserId = userId,
                        CreatedAt = DateTime.Now
                    };
                    await _dbContext.Playlists.InsertOneAsync(likedPlaylist);
                }

                var existingFilter = Builders<PlaylistSong>.Filter.Eq(ps => ps.PlaylistId, likedPlaylist.Id)
                    & Builders<PlaylistSong>.Filter.Eq(ps => ps.SongId, songId);
                var existingEntries = await _dbContext.PlaylistSongs
                    .Find(existingFilter)
                    .ToListAsync();

                if (existingEntries.Any())
                {
                    _logger.LogInformation("Unliking song");
                    await _dbContext.PlaylistSongs.DeleteManyAsync(existingFilter);
                    return Json(new { success = true, liked = false, message = "Song unliked" });
                }
                else
                {
                    _logger.LogInformation("Liking song");
                    var playlistSong = new PlaylistSong 
                    { 
                        Id = ObjectId.GenerateNewId().ToString(),
                        PlaylistId = likedPlaylist.Id, 
                        SongId = songId,
                        AddedAt = DateTime.Now
                    };
                    await _dbContext.PlaylistSongs.InsertOneAsync(playlistSong);
                    return Json(new { success = true, liked = true, message = "Song liked" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ToggleLike: {ExceptionMessage}", ex.Message);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        public IActionResult Admin()
        {
            return RedirectToAction("QuanLyBaiHat");
        }

        [Authorize(Roles = "Admin")]
        [Route("Home/Admin/QuanLyBaiHat")]
        public async Task<IActionResult> QuanLyBaiHat()
        {
            var songs = await _dbContext.Songs.Find(_ => true).ToListAsync();
            return View("~/Views/Admin/QuanLyBaiHat.cshtml", songs);
        }

        [Authorize(Roles = "Admin")]
        [Route("Home/Admin/QuanLyNguoiDung")]
        public async Task<IActionResult> QuanLyNguoiDung()
        {
            var users = await _dbContext.Users.Find(_ => true).ToListAsync();
            return View("~/Views/Admin/QuanLyNguoiDung.cshtml", users);
        }

        // User profile
        [Authorize]
        public async Task<IActionResult> Profile(string id = null)
        {
            string userId;
            if (!string.IsNullOrEmpty(id))
            {
                userId = id;
            }
            else
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return RedirectToAction("Login", "Auth");
                userId = userIdClaim.Value;
            }

            var user = await _dbContext.Users.FindByIdAsync(userId);
            if (user == null)
                return NotFound("User not found");

            return View(user);
        }

        // Edit user profile
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return RedirectToAction("Login", "Auth");

            var userId = userIdClaim.Value;
            var user = await _dbContext.Users.FindByIdAsync(userId);
            if (user == null)
                return NotFound("User not found");

            return View(user);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(string id, User model, IFormFile avatarFile)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return RedirectToAction("Login", "Auth");

            var userId = userIdClaim.Value;
            if (id != userId)
                return Forbid();

            var user = await _dbContext.Users.FindByIdAsync(userId);
            if (user == null)
                return NotFound("User not found");

            user.Username = model.Username;
            user.Bio = model.Bio;

            if (avatarFile != null && avatarFile.Length > 0)
            {
                try
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "avatars");
                    Directory.CreateDirectory(uploadsFolder);

                    string fileName = $"{userId}_{Guid.NewGuid()}{Path.GetExtension(avatarFile.FileName)}";
                    string filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await avatarFile.CopyToAsync(stream);
                    }

                    user.AvatarUrl = $"/uploads/avatars/{fileName}";
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error uploading avatar: {ex.Message}");
                    return View(user);
                }
            }

            try
            {
                await _dbContext.Users.UpdateAsync(userId, user);
                return RedirectToAction("Profile", new { id = userId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error updating profile: {ex.Message}");
                return View(user);
            }
        }

        // Privacy policy page
        public IActionResult Privacy()
        {
            return View();
        }

        // Access denied page
        public IActionResult AccessDenied()
        {
            return View();
        }

        // Request class for delete operations
        public class SongIdRequest 
        { 
            public string Id { get; set; } 
        }

        public class AdminUpdateUserRequest
        {
            public string? Id { get; set; }
            public string? Name { get; set; }
            public string? Username { get; set; }
            public string? Email { get; set; }
            public string? PhoneNumber { get; set; }
            public string? Role { get; set; }
            public bool IsActive { get; set; }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> UpdateUser([FromBody] AdminUpdateUserRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Id))
                    return Json(new { success = false, message = "ID người dùng không hợp lệ" });

                var user = await _dbContext.Users.FindByIdAsync(request.Id);
                if (user == null)
                    return Json(new { success = false, message = "Người dùng không tồn tại" });

                if (string.IsNullOrWhiteSpace(request.Username))
                    return Json(new { success = false, message = "Username không được để trống" });

                if (string.IsNullOrWhiteSpace(request.Email))
                    return Json(new { success = false, message = "Email không được để trống" });

                var isDuplicateUsername = await _dbContext.Users.Find(u => u.Username == request.Username && u.Id != request.Id).AnyAsync();
                if (isDuplicateUsername)
                    return Json(new { success = false, message = "Username đã tồn tại" });

                var isDuplicateEmail = await _dbContext.Users.Find(u => u.Email == request.Email && u.Id != request.Id).AnyAsync();
                if (isDuplicateEmail)
                    return Json(new { success = false, message = "Email đã tồn tại" });

                user.Name = string.IsNullOrWhiteSpace(request.Name) ? user.Name : request.Name.Trim();
                user.Username = request.Username.Trim();
                user.Email = request.Email.Trim();
                user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
                user.Role = request.Role == "Admin" ? "Admin" : "User";
                user.IsActive = request.IsActive;
                user.UpdatedAt = DateTime.UtcNow;

                if (string.IsNullOrWhiteSpace(user.Id))
                    return Json(new { success = false, message = "Dữ liệu người dùng không hợp lệ" });

                await _dbContext.Users.UpdateAsync(user.Id, user);
                return Json(new { success = true, message = "Cập nhật người dùng thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // Delete user
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteUser([FromBody] SongIdRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.Id))
                    return Json(new { success = false, message = "ID người dùng không hợp lệ" });

                var user = await _dbContext.Users.FindByIdAsync(request.Id);
                if (user == null)
                    return Json(new { success = false, message = "Người dùng không tồn tại" });

                // Delete all playlists associated with this user
                await _dbContext.Playlists.DeleteManyAsync(
                    Builders<Playlist>.Filter.Eq(p => p.UserId, request.Id)
                );

                // Delete the user
                await _dbContext.Users.DeleteAsync(request.Id);

                return Json(new { success = true, message = "Xóa người dùng thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // Error handling
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
