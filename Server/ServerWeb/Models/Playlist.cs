using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ServerWeb.Models
{
    public class Playlist
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public int UserId { get; set; }

        public List<PlaylistSong> PlaylistSongs { get; set; } = new();
    }
}