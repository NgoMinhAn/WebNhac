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

        [Required]
        public string Album { get; set; }  // Album name

        [Required]
        public string Genre { get; set; } // Genre of the song

        public string? FilePath { get; set; } // Path to the song file in the Music folder

        public string? CoverPath { get; set; } // Optional cover image path

        public TimeSpan Duration { get; set; } // Duration of the song
    }
}
