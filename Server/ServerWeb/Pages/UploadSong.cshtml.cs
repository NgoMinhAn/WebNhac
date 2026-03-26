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
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string Author { get; set; } = string.Empty;

        [BindProperty]
        public IFormFile UploadedFile { get; set; } = default!;

        public string Message { get; set; } = string.Empty;

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                Message = "Invalid input.";
                return Page();
            }

            if (UploadedFile != null && UploadedFile.Length > 0)
            {
                var musicFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Music");
                Directory.CreateDirectory(musicFolder);

                var filePath = Path.Combine(musicFolder, UploadedFile.FileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await UploadedFile.CopyToAsync(stream);
                }

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