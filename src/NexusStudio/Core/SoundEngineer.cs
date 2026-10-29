using System;

namespace Курсовая_ооп.Core
{
  
    public class SoundEngineer : StudioEmployee
    {
        public string Profile { get; set; }    // Профиль (Сведение/Мастеринг)
        public string Position { get; set; }   // Должность (Стажер/Шеф)
        public string Notes { get; set; }      // Примечания
        public int Experience 
        { 
            get => ExperienceYears;  //паттерн проектирования «Адаптер»
            set => ExperienceYears = value; 
        }

        public SoundEngineer(string name, int experience, string profile, string position, string notes = "")
            : base(name, experience)
        {
            Profile = profile;
            Position = position;
            Notes = notes;
        }
        public override string GetWorkDescription()
        {
            return $"[Специалист] {Name} | Должность: {Position} | Профиль: {Profile} | Стаж: {ExperienceYears} л.";
        }
    }
}