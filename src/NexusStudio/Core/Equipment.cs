namespace Курсовая_ооп.Core
{
    public abstract class Equipment
    {
        public string Model { get; set; }
        protected Equipment(string model) => Model = model;
        public virtual string PowerOn() => $"Устройство {Model} запущено в линию.";
    }
}