using System.ComponentModel.DataAnnotations;

namespace ServerWeb.Models
{
    public class Song
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Author { get; set; }

        public string? FilePath { get; set; } // Path to the song file in the Music folder
    }
}
