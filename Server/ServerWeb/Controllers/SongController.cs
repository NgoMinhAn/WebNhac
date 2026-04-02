using Microsoft.AspNetCore.Mvc;
using ServerWeb.Data;
using ServerWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ServerWeb.Controllers
{
    public class SongController : Controller
    {
        private readonly AppDbContext _dbContext;

        public SongController(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IActionResult Details(int id)
        {
            var song = _dbContext.Songs.Find(id);
            if (song == null)
            {
                return NotFound();
            }
            return View(song);
        }
        // 1. Giao diện trang Chỉnh sửa: GET /Song/Edit/3
        public IActionResult Edit(int id)
        {
            var song = _dbContext.Songs.Find(id);
            if (song == null)
            {
                return NotFound();
            }
            return View(song);
        }

        // 2. Xử lý lưu dữ liệu sau khi sửa: POST /Song/Edit/3
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Song song, IFormFile? musicFile, IFormFile? coverFile)
        {
            if (id != song.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingSong = await _dbContext.Songs.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
                    if (existingSong == null) return NotFound();

                    // Giữ lại đường dẫn cũ nếu không upload file mới
                    song.FilePath = existingSong.FilePath;
                    song.CoverPath = existingSong.CoverPath;

                    // Xử lý upload file nhạc mới (nếu có)
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

                    // Xử lý upload ảnh bìa mới (nếu có)
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

                    _dbContext.Update(song);
                    await _dbContext.SaveChangesAsync();
                    return RedirectToAction("Index", "Home");
                }
                catch (Exception)
                {
                    ModelState.AddModelError("", "Có lỗi xảy ra khi lưu dữ liệu.");
                }
            }
            return View(song);
        }
    }
}