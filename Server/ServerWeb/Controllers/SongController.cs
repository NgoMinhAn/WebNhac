using Microsoft.AspNetCore.Mvc;
using ServerWeb.Data;
using ServerWeb.Models;

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
    }
}