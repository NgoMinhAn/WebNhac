using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ServerWeb.Models
{
    public class Playlist
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên danh sách phát không được để trống")]
        [StringLength(100)]
        public string Name { get; set; } = "Danh sách phát của tôi";

        // Cột này đã được thêm để xử lý tính năng "Đặt thành riêng tư"
        public bool IsPrivate { get; set; } = false;

        public string? Description { get; set; }

        // Đảm bảo có dấu ? để cho phép null khi chưa có ảnh bìa
        public string? ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Khóa ngoại liên kết với User
        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        // Khởi tạo List để tránh lỗi Null Reference khi truy cập PlaylistSongs.Count
        public virtual ICollection<PlaylistSong> PlaylistSongs { get; set; } = new List<PlaylistSong>();
    }
}