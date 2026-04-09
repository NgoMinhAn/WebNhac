using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ServerWeb.Models
{
    public class Song
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [Required]
        public string? Name { get; set; }

        [Required]
        public string? Author { get; set; } 

        [Required]
        public string? Album { get; set; }  // Album name

        [Required]
        public string? Genre { get; set; } // Genre of the song

        public string? FilePath { get; set; } // Path to the song file in the Music folder

        public string? CoverPath { get; set; } // Optional cover image path

        public TimeSpan Duration { get; set; } // Duration of the song
    }
}
