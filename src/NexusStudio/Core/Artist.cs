using System;

namespace Курсовая_ооп.Core
{
    public class Artist
    {
        public string Name { get; set; }
        public string Genre { get; set; }
        public string Notes { get; set; } // Примечания к артисту

        public Artist(string name, string genre, string notes = "")
        {
            Name = name;
            Genre = genre;
            Notes = notes;
        }

        public string GetWorkDescription()
        {
            return $"[Артист] {Name} | Жанр: {Genre} | Примечание: {Notes}";
        }
    }
}