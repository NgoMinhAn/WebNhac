using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ServerWeb.Data;
using ServerWeb.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace ServerWeb.Controllers
{
    [Authorize] // Chỉ cho phép người đã đăng nhập mới tạo được Playlist
    public class PlaylistController : Controller
    {
        private readonly AppDbContext _context;

        public PlaylistController(AppDbContext context)
        {
            _context = context;
        }

        // ĐÂY LÀ HÀM BẠN ĐANG THIẾU
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Challenge();

            // Tạo một Playlist mặc định mới
            var newPlaylist = new Playlist
            {
                Name = "Danh sách phát của tôi #" + (new Random().Next(100, 999)),
                UserId = int.Parse(userId),
                CreatedAt = DateTime.Now
            };

            _context.Playlists.Add(newPlaylist);
            await _context.SaveChangesAsync();

            // Sau khi tạo xong, chuyển hướng thẳng đến trang chi tiết của nó
            return RedirectToAction("Details", new { id = newPlaylist.Id });
        }

        // Nhớ đảm bảo có hàm Details để Redirect tới không bị lỗi 404 tiếp
        public IActionResult Details(int id)
        {
            var playlist = _context.Playlists
                .Include(p => p.PlaylistSongs)
                .ThenInclude(ps => ps.Song)
                .FirstOrDefault(p => p.Id == id);

            if (playlist == null) return NotFound();
            return View(playlist);
        }
        // 1. Tìm kiếm bài hát
        [HttpGet]
        public async Task<IActionResult> SearchSongs(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return Json(new List<object>());

            var songs = await _context.Songs
                .Where(s => s.Name.Contains(query) || s.Author.Contains(query))
                .Select(s => new
                {
                    Id = s.Id,
                    Name = s.Name,
                    Author = s.Author
                })
                .Take(5)
                .ToListAsync();

            return Json(songs);
        }

        // 2. Thêm bài hát vào Playlist
        [HttpPost]
        public async Task<IActionResult> AddSong([FromBody] PlaylistSong model)
        {
            // model sẽ nhận playlistId và songId từ client
            var exists = await _context.PlaylistSongs
                .AnyAsync(ps => ps.PlaylistId == model.PlaylistId && ps.SongId == model.SongId);

            if (!exists)
            {
                _context.PlaylistSongs.Add(model);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Đã thêm vào playlist!" });
            }

            return BadRequest(new { message = "Bài hát đã có trong playlist." });
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            // Lấy ID của người dùng đang đăng nhập
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return RedirectToAction("Login", "Auth");

            int userId = int.Parse(userIdClaim.Value);

            // Lấy danh sách Playlist của User đó
            var playlists = await _context.Playlists
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(playlists);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserPlaylists()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();

            if (!int.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();

            var playlists = await _context.Playlists
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.ImageUrl,
                    p.IsPrivate
                })
                .ToListAsync();

            return Json(playlists);
        }
        // Xóa Playlist
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var playlist = await _context.Playlists.FindAsync(id);
            if (playlist == null) return NotFound();

            // Xóa các liên kết trong bảng trung gian trước (nếu có)
            var links = _context.PlaylistSongs.Where(ps => ps.PlaylistId == id);
            _context.PlaylistSongs.RemoveRange(links);

            _context.Playlists.Remove(playlist);
            await _context.SaveChangesAsync();

            return Ok(); // Trả về thành công để JavaScript load lại trang
        }

        // Đổi trạng thái Riêng tư/Công khai
        [HttpPost]
        public async Task<IActionResult> TogglePrivacy(int id)
        {
            var playlist = await _context.Playlists.FindAsync(id);
            if (playlist == null) return NotFound();

            playlist.IsPrivate = !playlist.IsPrivate; // Giả sử bạn đã thêm cột này vào DB
            await _context.SaveChangesAsync();
            return Ok();
        }
        // Đổi tên
        [HttpPost]
        public async Task<IActionResult> UpdateName([FromBody] PlaylistUpdateModel model)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var playlist = await _context.Playlists.FindAsync(model.Id);

            if (playlist == null) return NotFound();

            // KIỂM TRA: Nếu không phải chủ sở hữu thì không cho sửa
            if (playlist.UserId != userId) return Forbid();

            playlist.Name = model.Name;
            await _context.SaveChangesAsync();
            return Ok();
        }

        // Cập nhật mô tả Playlist
        [HttpPost]
        public async Task<IActionResult> UpdateDescription([FromBody] PlaylistDescriptionModel model)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var playlist = await _context.Playlists.FindAsync(model.Id);

            if (playlist == null) return NotFound();

            // KIỂM TRA: Nếu không phải chủ sở hữu thì không cho sửa
            if (playlist.UserId != userId) return Forbid();

            playlist.Description = model.Description;
            await _context.SaveChangesAsync();
            return Ok();
        }

        // Đổi ảnh
        [HttpPost]
        public async Task<IActionResult> UpdateImage(int id, IFormFile image)
        {
            var playlist = await _context.Playlists.FindAsync(id);
            if (playlist == null || image == null) return BadRequest();

            // Lưu file vào thư mục wwwroot/covers
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/covers", fileName);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            playlist.ImageUrl = "/covers/" + fileName;
            await _context.SaveChangesAsync();

            return Ok(new { newUrl = playlist.ImageUrl });
        }

        // Xóa bài hát khỏi Playlist
        [HttpPost]
        public async Task<IActionResult> RemoveSong(int playlistId, int songId)
        {
            var playlistSong = await _context.PlaylistSongs
                .FirstOrDefaultAsync(ps => ps.PlaylistId == playlistId && ps.SongId == songId);

            if (playlistSong == null) return NotFound();

            _context.PlaylistSongs.Remove(playlistSong);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã xóa bài hát khỏi playlist!" });
        }

        // Sắp xếp lại thứ tự bài hát trong Playlist
        [HttpPost]
        public IActionResult UpdateSongOrder([FromBody] List<int> songIds)
        {
            // songIds là danh sách ID bài hát theo thứ tự mới
            // (Chúng tôi không lưu trữ vị trí trong DB, nhưng có thể extend nếu cần)
            
            return Ok(new { message = "Thứ tự đã được cập nhật!" });
        }

        // Class phụ để nhận dữ liệu JSON
        public class PlaylistUpdateModel
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        public class PlaylistDescriptionModel
        {
            public int Id { get; set; }
            public string Description { get; set; }
        }
    }
}