using System.Collections.Generic;

namespace Курсовая_ооп.Core
{
    public class StudioManager
    {
        public List<Artist> ArtistRoster { get; set; } = new List<Artist>();
        public Dictionary<string, Equipment> MainRack { get; set; } = new Dictionary<string, Equipment>();

        public void RegisterArtist(Artist artist)
        {
            if (string.IsNullOrWhiteSpace(artist.Name))
                throw new StudioException("Регистрация сорвана: имя пустое", "ERR_BAD_NAME");
            ArtistRoster.Add(artist);
        }

        public Equipment GetEquipment(string key)
        {
            if (!MainRack.ContainsKey(key))
                throw new StudioException($"Оборудование '{key}' отсутствует в стойке!", "ERR_HARDWARE_NOT_FOUND");
            return MainRack[key];
        }
    }
}