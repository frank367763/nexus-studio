namespace Курсовая_ооп.Core
{
    public class Microphone : Equipment
    {
        public string Type { get; set; } 
        public Microphone(string model, string type) : base(model) => Type = type;

        public override string PowerOn() => $"[Железо] Микрофон {Model} ({Type}) активен. Фантомное питание 48V подано.";
    }
}