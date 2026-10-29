using System;
using System.IO;

namespace Курсовая_ооп.Core
{
    public class RecordingSession : IRecordable, IPlayable
    {
        public string TrackName { get; set; }
        public Artist CurrentArtist { get; set; }
        public SoundEngineer ActiveEngineer { get; set; }
        public string FilePath { get; private set; }
        public string BeatFilePath { get; set; }

        public event StudioNotificationHandler StateChanged;
        private readonly AudioEngine _engine = new AudioEngine();
        public bool IsPlaying => _engine.IsPlaying; 
        public bool IsPaused => _engine.IsPaused; // Добавлено свойство фиксации паузы

        public RecordingSession(string trackName, Artist artist, SoundEngineer engineer)
        {
            if (string.IsNullOrWhiteSpace(trackName))
                throw new StudioException("Невозможно начать сессию: Название пустое", "ERR_EMPTY_TITLE");

            CurrentArtist = artist;
            ActiveEngineer = engineer;
            string ext = Path.GetExtension(trackName);
            string safeName = string.IsNullOrEmpty(ext) ? $"{trackName}.wav" : trackName;
            if (Path.IsPathRooted(safeName))
            {
                FilePath = safeName;
            }
            else
            {
                string studioFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NexusStudio");
                FilePath = Path.Combine(studioFolder, safeName);
            }
            
            TrackName = Path.GetFileNameWithoutExtension(safeName);
        }

        public void StartRecording()
        {
            _engine.StartRecord(FilePath, BeatFilePath);
            string beatMsg = string.IsNullOrEmpty(BeatFilePath) ? "" : " [С МИНУСОВКОЙ]";
            StateChanged?.Invoke(this, $"[REC] Накапливаем аудиодорожку '{TrackName}'{beatMsg}...");
        }

        public void StopRecording()
        {
            _engine.StopRecord();
            StateChanged?.Invoke(this, $"[STOP] Запись завершена. Файл сохранен: {Path.GetFileName(FilePath)}");
        }

        public void Play() => PlayProcessed(1.0f, false);
        
        public void Pause() => _engine.PausePlayback();
        public void Resume()
        {
            _engine.ResumePlayback();
            StateChanged?.Invoke(this, $"[ПЛЕЕР] Воспроизведение трека '{TrackName}' возобновлено.");
        }

        public void PlayProcessed(float speed, bool isReversed)
        {
            if (!File.Exists(FilePath))
                throw new StudioException("Файл не найден на диске!", "ERR_FILE_NOT_FOUND");

            _engine.PlayFile(FilePath, speed, isReversed);
            StateChanged?.Invoke(this, $"[ПЛЕЕР] Воспроизведение '{TrackName}'. Скорость: {speed:F1}x");
        }

        public void Stop()
        {
            _engine.StopPlayback();
            StateChanged?.Invoke(this, "[ПЛЕЕР] Воспроизведение полностью остановлено.");
        }

        public void TriggerDrumPad(string padName)
        {
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string soundPath = Path.Combine(winDir, "Media", "chimes.wav"); 
            if (padName == "KICK") soundPath = Path.Combine(winDir, "Media", "tada.wav");
            if (padName == "SNARE") soundPath = Path.Combine(winDir, "Media", "notify.wav");

            if (File.Exists(soundPath))
            {
                _engine.PlayOneShot(soundPath, 1.0f);
                StateChanged?.Invoke(this, $"[СЭМПЛЕР] Триггер пэда: {padName}");
            }
        }

        public double TotalSeconds => _engine.GetTotalSeconds();
        public double CurrentSeconds => _engine.GetCurrentSeconds();
        public void Seek(double seconds) => _engine.SetPosition(seconds);
    }
}