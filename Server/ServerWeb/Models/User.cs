using System.ComponentModel.DataAnnotations;

namespace ServerWeb.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Username { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public string? PhoneNumber { get; set; }
        public string? Role { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Bio { get; set; }
        public virtual ICollection<Playlist>? Playlists { get; set; }
    }
}
