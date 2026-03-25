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
    }
}
