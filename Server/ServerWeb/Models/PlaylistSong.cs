using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServerWeb.Models
{
    public class PlaylistSong
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string? PlaylistId { get; set; }
        public virtual Playlist? Playlist { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string? SongId { get; set; }
        public virtual Song? Song { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.Now;
    }
}