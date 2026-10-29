namespace Курсовая_ооп.Core
{
    public abstract class StudioEmployee
    {
        public string Name { get; set; }
        public int ExperienceYears { get; set; }

        protected StudioEmployee(string name, int experience)
        {
            Name = name;
            ExperienceYears = experience;
        }

        public abstract string GetWorkDescription();
    }
}