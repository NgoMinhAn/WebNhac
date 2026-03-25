using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using ServerWeb.Models;

namespace ServerWeb.Pages
{
    public class UploadSongModel : PageModel
    {
        [BindProperty]
        public string Name { get; set; }

        [BindProperty]
        public string Author { get; set; }

        [BindProperty]
        public IFormFile File { get; set; }

        public string Message { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                Message = "Invalid input.";
                return Page();
            }

            if (File != null && File.Length > 0)
            {
                var musicFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Music");
                Directory.CreateDirectory(musicFolder);

                var filePath = Path.Combine(musicFolder, File.FileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await File.CopyToAsync(stream);
                }

                // Save song details to database (if applicable)
                // Example: Save to a list or database context

                Message = "Song uploaded successfully.";
            }
            else
            {
                Message = "Please upload a valid file.";
            }

            return Page();
        }
    }
}