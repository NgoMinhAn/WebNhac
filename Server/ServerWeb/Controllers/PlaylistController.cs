using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ServerWeb.Data;
using ServerWeb.Models;
using ServerWeb.Services;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Security.Claims;

namespace ServerWeb.Controllers
{
    [Authorize]
    public class PlaylistController : Controller
    {
        private readonly AppDbContext _context;

        public PlaylistController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Challenge();

            var newPlaylist = new Playlist
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Name = "Danh sách phát được tôi #" + (new Random().Next(100, 999)),
                UserId = userId,
                CreatedAt = DateTime.Now
            };

            await _context.Playlists.InsertOneAsync(newPlaylist);
            return RedirectToAction("Details", new { id = newPlaylist.Id });
        }

        public async Task<IActionResult> Details(string id)
        {
            var playlist = await _context.Playlists.FindByIdAsync(id);
            if (playlist == null)
                return NotFound();

            var filter = Builders<PlaylistSong>.Filter.Eq(ps => ps.PlaylistId, id);
            var playlistSongs = await _context.PlaylistSongs.Find(filter).ToListAsync();

            var songIds = playlistSongs
                .Select(ps => ps.SongId)
                .Where(songId => !string.IsNullOrWhiteSpace(songId))
                .Distinct()
                .ToList();

            if (songIds.Any())
            {
                var songsFilter = Builders<Song>.Filter.In(s => s.Id, songIds);
                var songs = await _context.Songs.Find(songsFilter).ToListAsync();
                var songMap = songs
                    .Where(s => !string.IsNullOrWhiteSpace(s.Id))
                    .ToDictionary(s => s.Id!, s => s);

                foreach (var playlistSong in playlistSongs)
                {
                    if (!string.IsNullOrWhiteSpace(playlistSong.SongId) &&
                        songMap.TryGetValue(playlistSong.SongId, out var song))
                    {
                        playlistSong.Song = song;
                    }
                }
            }

            playlist.PlaylistSongs = playlistSongs;
            return View(playlist);
        }

        [HttpGet]
        public async Task<IActionResult> SearchSongs(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Json(Array.Empty<object>());

            var trimmedQuery = query.Trim();
            var regex = new BsonRegularExpression(trimmedQuery, "i");

            var filter = Builders<Song>.Filter.Or(
                Builders<Song>.Filter.Regex(s => s.Name, regex),
                Builders<Song>.Filter.Regex(s => s.Author, regex)
            );

            var songs = await _context.Songs
                .Find(filter)
                .Limit(20)
                .ToListAsync();

            return Json(songs.Select(song => new
            {
                id = song.Id,
                name = song.Name,
                author = song.Author
            }));
        }

        [HttpPost]
        public async Task<IActionResult> AddSong([FromBody] PlaylistSongRequest? request, string? playlistId, string? songId)
        {
            playlistId ??= request?.PlaylistId;
            songId ??= request?.SongId;

            if (string.IsNullOrWhiteSpace(playlistId) || string.IsNullOrWhiteSpace(songId))
                return Json(new { success = false, message = "Thiếu thông tin playlist hoặc bài hát" });

            var playlist = await _context.Playlists.FindByIdAsync(playlistId);
            if (playlist == null)
                return Json(new { success = false, message = "Playlist không tồn tại" });

            var song = await _context.Songs.FindByIdAsync(songId);
            if (song == null)
                return Json(new { success = false, message = "Bài hát không tồn tại" });

            var filter = Builders<PlaylistSong>.Filter.And(
                Builders<PlaylistSong>.Filter.Eq(ps => ps.PlaylistId, playlistId),
                Builders<PlaylistSong>.Filter.Eq(ps => ps.SongId, songId)
            );

            var existingSong = await _context.PlaylistSongs.Find(filter).FirstOrDefaultAsync();
            if (existingSong != null)
                return Json(new { success = false, message = "Bài hát đã có trong playlist" });

            var playlistSong = new PlaylistSong
            {
                Id = ObjectId.GenerateNewId().ToString(),
                PlaylistId = playlistId,
                SongId = songId,
                AddedAt = DateTime.Now
            };

            await _context.PlaylistSongs.InsertOneAsync(playlistSong);

            return Json(new { success = true, message = "Thêm bài hát thành công" });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveSong(string playlistId, string songId)
        {
            var playlist = await _context.Playlists.FindByIdAsync(playlistId);
            if (playlist == null)
                return Json(new { success = false, message = "Playlist không tồn tại" });

            var filter = Builders<PlaylistSong>.Filter.And(
                Builders<PlaylistSong>.Filter.Eq(ps => ps.PlaylistId, playlistId),
                Builders<PlaylistSong>.Filter.Eq(ps => ps.SongId, songId)
            );

            var result = await _context.PlaylistSongs.DeleteOneAsync(filter);
            if (result.DeletedCount == 0)
                return Json(new { success = false, message = "Bài hát không có trong playlist" });

            return Json(new { success = true, message = "Xóa bài hát thành công" });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateName(string playlistId, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                return Json(new { success = false, message = "Tên playlist không được trống" });

            var playlist = await _context.Playlists.FindByIdAsync(playlistId);
            if (playlist == null)
                return Json(new { success = false, message = "Playlist không tồn tại" });

            playlist.Name = newName;
            await _context.Playlists.UpdateAsync(playlistId, playlist);

            return Json(new { success = true, message = "Cập nhật tên playlist thành công" });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateImage(string playlistId, IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                return Json(new { success = false, message = "Tệp hình ảnh không hợp lệ" });

            if (!IsValidImageFile(imageFile))
                return Json(new { success = false, message = "Chỉ hỗ trợ các tệp hình ảnh (jpg, png, gif, webp)" });

            var playlist = await _context.Playlists.FindByIdAsync(playlistId);
            if (playlist == null)
                return Json(new { success = false, message = "Playlist không tồn tại" });

            try
            {
                string imageFileName = Path.Combine("uploads", "playlists", $"{playlistId}_{Guid.NewGuid()}.{GetFileExtension(imageFile.FileName)}");
                string imageFullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imageFileName);

                Directory.CreateDirectory(Path.GetDirectoryName(imageFullPath));

                using (var stream = new FileStream(imageFullPath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                var existingImagePath = playlist.ImageUrl != null ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", playlist.ImageUrl) : null;
                if (!string.IsNullOrEmpty(existingImagePath) && System.IO.File.Exists(existingImagePath))
                {
                    System.IO.File.Delete(existingImagePath);
                }

                playlist.ImageUrl = "/" + imageFileName.Replace("\\", "/");
                await _context.Playlists.UpdateAsync(playlistId, playlist);

                return Json(new { success = true, message = "Cập nhật hình ảnh thành công", imageUrl = "/" + imageFileName.Replace("\\", "/") });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi cập nhật hình ảnh: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            var playlist = await _context.Playlists.FindByIdAsync(id);
            if (playlist == null)
                return NotFound();

            return View(playlist);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var playlist = await _context.Playlists.FindByIdAsync(id);
            if (playlist == null)
                return NotFound();

            var filterSongs = Builders<PlaylistSong>.Filter.Eq(ps => ps.PlaylistId, id);
            await _context.PlaylistSongs.DeleteManyAsync(filterSongs);

            await _context.Playlists.DeleteOneAsync(p => p.Id == id);

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Challenge();

            var playlists = await _context.Playlists.Find(p => p.UserId == userId).ToListAsync();
            return View(playlists);
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Challenge();

            var playlists = await _context.Playlists.Find(p => p.UserId == userId).ToListAsync();
            return View(playlists);
        }

        private bool IsValidImageFile(IFormFile file)
        {
            var validImageTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
            return validImageTypes.Contains(file.ContentType);
        }

        private string GetFileExtension(string fileName)
        {
            return Path.GetExtension(fileName).TrimStart('.');
        }
    }

    public class PlaylistResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string UserId { get; set; }
        public string ImageUrl { get; set; }
        public int SongCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PlaylistSongRequest
    {
        public string PlaylistId { get; set; }
        public string SongId { get; set; }
    }

    public class PlaylistUpdateRequest
    {
        public string PlaylistId { get; set; }
        public string NewName { get; set; }
    }
}
