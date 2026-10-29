namespace Курсовая_ооп.Core
{
    public interface IPlayable
    {
        void Play();
        void Stop();
        bool IsPlaying { get; }
    }
}