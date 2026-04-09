using MongoDB.Driver;
using ServerWeb.Models;

namespace ServerWeb.Data
{
    public class AppDbContext
    {
        private readonly IMongoDatabase _database;

        public AppDbContext(IMongoDatabase database)
        {
            _database = database;
        }

        public IMongoCollection<Song> Songs => _database.GetCollection<Song>("songs");
        public IMongoCollection<User> Users => _database.GetCollection<User>("users");
        public IMongoCollection<Playlist> Playlists => _database.GetCollection<Playlist>("playlists");
        public IMongoCollection<PlaylistSong> PlaylistSongs => _database.GetCollection<PlaylistSong>("playlistSongs");
    }
}