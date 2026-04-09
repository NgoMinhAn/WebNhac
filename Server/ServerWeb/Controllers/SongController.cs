using Microsoft.AspNetCore.Mvc;
using ServerWeb.Data;
using ServerWeb.Models;
using ServerWeb.Services;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Security.Claims;

namespace ServerWeb.Controllers
{
    public class SongController : Controller
    {
        private readonly AppDbContext _dbContext;

        public SongController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Details(string id)
        {
            var song = await _dbContext.Songs.FindByIdAsync(id);
            if (song == null)
            {
                return NotFound();
            }
            return View(song);
        }

        // GET: Edit page
        public async Task<IActionResult> Edit(string id)
        {
            var song = await _dbContext.Songs.FindByIdAsync(id);
            if (song == null)
            {
                return NotFound();
            }
            return View(song);
        }

        // POST: Save changes
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Song song, IFormFile? musicFile, IFormFile? coverFile)
        {
            if (id != song.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingSong = await _dbContext.Songs.FindByIdAsync(id);
                    if (existingSong == null)
                        return NotFound();

                    // Keep old paths if no new files uploaded
                    song.FilePath = existingSong.FilePath;
                    song.CoverPath = existingSong.CoverPath;

                    // Upload new music file if provided
                    if (musicFile != null)
                    {
                        string musicName = Guid.NewGuid().ToString() + Path.GetExtension(musicFile.FileName);
                        string musicPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/Music", musicName);
                        using (var stream = new FileStream(musicPath, FileMode.Create))
                        {
                            await musicFile.CopyToAsync(stream);
                        }
                        song.FilePath = "/Music/" + musicName;
                    }

                    // Upload new cover image if provided
                    if (coverFile != null)
                    {
                        string coverName = Guid.NewGuid().ToString() + Path.GetExtension(coverFile.FileName);
                        string coverPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images", coverName);
                        using (var stream = new FileStream(coverPath, FileMode.Create))
                        {
                            await coverFile.CopyToAsync(stream);
                        }
                        song.CoverPath = "/images/" + coverName;
                    }

                    await _dbContext.Songs.UpdateAsync(id, song);
                    return RedirectToAction("Index", "Home");
                }
                catch (Exception)
                {
                    ModelState.AddModelError("", "Có lỗi xảy ra khi lưu dữ liệu.");
                }
            }
            return View(song);
        }

        [HttpGet]
        public async Task<IActionResult> GetSongDetails(string id)
        {
            var song = await _dbContext.Songs.FindByIdAsync(id);
            if (song == null)
                return NotFound();

            var liked = false;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null)
                {
                    var likedPlaylistFilter = Builders<Playlist>.Filter.And(
                        Builders<Playlist>.Filter.Eq(p => p.UserId, userIdClaim.Value),
                        Builders<Playlist>.Filter.Eq(p => p.Name, "Liked")
                    );
                    var likedPlaylist = await _dbContext.Playlists.Find(likedPlaylistFilter).FirstOrDefaultAsync();

                    if (likedPlaylist != null)
                    {
                        var playlistSongFilter = Builders<PlaylistSong>.Filter.And(
                            Builders<PlaylistSong>.Filter.Eq(ps => ps.PlaylistId, likedPlaylist.Id),
                            Builders<PlaylistSong>.Filter.Eq(ps => ps.SongId, id)
                        );
                        liked = await _dbContext.PlaylistSongs.Find(playlistSongFilter).AnyAsync();
                    }
                }
            }

            return Json(new
            {
                name = song.Name,
                author = song.Author,
                album = song.Album,
                genre = song.Genre,
                duration = song.Duration.TotalHours >= 1 ? song.Duration.ToString(@"h\:mm\:ss") : song.Duration.ToString(@"mm\:ss"),
                imageUrl = song.CoverPath ?? "/images/default-disk.png",
                audioUrl = song.FilePath,
                liked
            });
        }
    }
}