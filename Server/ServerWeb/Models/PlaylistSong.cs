namespace ServerWeb.Models
{
    public class PlaylistSong
    {
        public int Id { get; set; }   // 👈 thêm dòng này

        public int PlaylistId { get; set; }
        public int SongId { get; set; }

        public Playlist Playlist { get; set; }
        public Song Song { get; set; }
    }
}