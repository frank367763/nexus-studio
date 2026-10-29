using System;
using System.Collections.Generic;
using System.IO;
using System.Collections.Concurrent;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Курсовая_ооп.Core
{
    public class CleanTapeSpeedProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly List<float> _cachedSamples = new List<float>();
        private double _currentFrameIndex = 0;
        private readonly float _speed;

        public WaveFormat WaveFormat => _source.WaveFormat;
        public double CurrentTimeSeconds => _currentFrameIndex / (double)WaveFormat.SampleRate;
        public double TotalTimeSeconds => (_cachedSamples.Count / WaveFormat.Channels) / (double)WaveFormat.SampleRate;

        public CleanTapeSpeedProvider(ISampleProvider source, float speed)
        {
            _source = source;
            _speed = speed;

            float[] tempBuffer = new float[32768];
            int read;
            while ((read = _source.Read(tempBuffer, 0, tempBuffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    _cachedSamples.Add(tempBuffer[i]);
                }
            }
        }
        public void SetPositionSeconds(double seconds)
        {
            _currentFrameIndex = seconds * WaveFormat.SampleRate;
            if (_currentFrameIndex < 0) _currentFrameIndex = 0;
            if (_currentFrameIndex > (_cachedSamples.Count / WaveFormat.Channels)) 
                _currentFrameIndex = (_cachedSamples.Count / WaveFormat.Channels);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (_cachedSamples.Count == 0) return 0;

            int channels = WaveFormat.Channels;
            int totalFrames = _cachedSamples.Count / channels;
            int framesToWrite = count / channels;
            int samplesWritten = 0;

            for (int f = 0; f < framesToWrite; f++)
            {
                int frame1 = (int)_currentFrameIndex;
                int frame2 = frame1 + 1;

                if (frame1 >= totalFrames) break;
                if (frame2 >= totalFrames) frame2 = frame1;

                double fraction = _currentFrameIndex - frame1;

                for (int ch = 0; ch < channels; ch++)
                {
                    float f1 = _cachedSamples[frame1 * channels + ch];
                    float f2 = _cachedSamples[frame2 * channels + ch];

                    float interpolated = (float)(f1 + (f2 - f1) * fraction);
                    buffer[offset + samplesWritten] = interpolated;
                    samplesWritten++;
                }

                _currentFrameIndex += _speed;
            }

            return samplesWritten;
        }
    }

    public class AudioEngine
    {
        private WaveInEvent _waveIn;
        private WaveFileWriter _waveWriter;
        private WaveOutEvent _waveOut;
        private AudioFileReader _fileReader;
        private WaveOutEvent _beatOut;
        private AudioFileReader _beatReader;

        private CleanTapeSpeedProvider _cleanPitchProvider;
        private string _currentPlaybackPath;
        private string _activeReverseTempFile;
        private float _currentSpeed = 1.0f;
        private readonly object _lockObject = new object();

        private static readonly ConcurrentQueue<string> _globalLivePadQueue = new ConcurrentQueue<string>();
        private readonly List<AudioFileReader> _activeLivePads = new List<AudioFileReader>();

        public bool IsRecording { get; private set; }
        public bool IsPlaying => _waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing;
        public bool IsPaused => _waveOut != null && _waveOut.PlaybackState == PlaybackState.Paused;

        public void StartRecord(string path, string beatPath = null)
        {
            lock (_lockObject)
            {
                try
                {
                    StopPlayback();
                    foreach (var pad in _activeLivePads) pad.Dispose();
                    _activeLivePads.Clear();
                    while (_globalLivePadQueue.TryDequeue(out _)) { }
                    
                    if (!string.IsNullOrEmpty(beatPath) && File.Exists(beatPath))
                    {
                        _beatReader = new AudioFileReader(beatPath);
                        _beatOut = new WaveOutEvent();
                        _beatOut.Init(_beatReader);
                        _beatOut.Play();
                    }

                    _waveIn = new WaveInEvent { WaveFormat = new WaveFormat(44100, 16, 1) }; 
                    _waveWriter = new WaveFileWriter(path, _waveIn.WaveFormat);

                    _waveIn.DataAvailable += (s, e) =>
                    {
                        lock (_lockObject)
                        {
                            try
                            {
                                if (_waveWriter == null) return;
                                byte[] micBuffer = e.Buffer;
                                int bytesRecorded = e.BytesRecorded;

                                while (_globalLivePadQueue.TryDequeue(out string padPath))
                                {
                                    if (File.Exists(padPath)) _activeLivePads.Add(new AudioFileReader(padPath));
                                }

                                if (_activeLivePads.Count > 0)
                                {
                                    int samplesCount = bytesRecorded / 2; 
                                    float[] mixBuffer = new float[samplesCount];

                                    for (int i = _activeLivePads.Count - 1; i >= 0; i--)
                                    {
                                        var padReader = _activeLivePads[i];
                                        float[] padSamples = new float[samplesCount * padReader.WaveFormat.Channels];
                                        int readSamples = padReader.Read(padSamples, 0, padSamples.Length);

                                        if (readSamples == 0)
                                        {
                                            padReader.Dispose();
                                            _activeLivePads.RemoveAt(i);
                                            continue;
                                        }

                                        int padChannels = padReader.WaveFormat.Channels;
                                        int framesRead = readSamples / padChannels;

                                        for (int j = 0; j < framesRead && j < samplesCount; j++)
                                        {
                                            float sum = 0;
                                            for (int c = 0; c < padChannels; c++) sum += padSamples[j * padChannels + c];
                                            mixBuffer[j] += (sum / padChannels) * 0.8f; 
                                        }
                                    }

                                    for (int j = 0; j < samplesCount; j++)
                                    {
                                        short micSample = BitConverter.ToInt16(micBuffer, j * 2);
                                        float combined = (micSample / 32768f) + mixBuffer[j];

                                        if (combined > 1.0f) combined = 1.0f;
                                        if (combined < -1.0f) combined = -1.0f;

                                        short finalSample = (short)(combined * 32767);
                                        byte[] finalBytes = BitConverter.GetBytes(finalSample);
                                        micBuffer[j * 2] = finalBytes[0];
                                        micBuffer[j * 2 + 1] = finalBytes[1];
                                    }
                                }

                                _waveWriter.Write(micBuffer, 0, bytesRecorded);
                            }
                            catch { }
                        }
                    };

                    _waveIn.StartRecording();
                    IsRecording = true;
                }
                catch (Exception ex)
                {
                    throw new Exception("Не удалось запустить аппаратную шину записи: " + ex.Message);
                }
            }
        }

        public void StopRecord()
        {
            lock (_lockObject)
            {
                if (!IsRecording) return;
                try { _waveIn?.StopRecording(); }
                finally
                {
                    _waveWriter?.Dispose(); _waveWriter = null;
                    _waveIn?.Dispose(); _waveIn = null;
                    IsRecording = false;

                    if (_beatOut != null)
                    {
                        _beatOut.Stop(); _beatOut.Dispose(); _beatOut = null;
                        _beatReader?.Dispose(); _beatReader = null;
                    }
                }
            }
        }

        public void PlayFile(string path, float speed = 1.0f, bool reverse = false)
        {
            lock (_lockObject)
            {
                try
                {
                    StopPlayback();
                    _currentPlaybackPath = path;
                    _currentSpeed = speed;

                    if (reverse)
                    {
                        string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
                        string revPath = Path.Combine(Path.GetDirectoryName(path) ?? AppDomain.CurrentDomain.BaseDirectory, $"rev_cache_{uniqueId}.wav");
                        
                        using (var reader = new AudioFileReader(path))
                        {
                            List<float> audioData = new List<float>();
                            float[] buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
                            int samplesRead;
                            while ((samplesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                float[] validSamples = new float[samplesRead];
                                Array.Copy(buffer, validSamples, samplesRead);
                                audioData.AddRange(validSamples);
                            }
                            audioData.Reverse();

                            using (var writer = new WaveFileWriter(revPath, reader.WaveFormat))
                            {
                                writer.WriteSamples(audioData.ToArray(), 0, audioData.Count);
                            }
                        }
                        path = revPath;
                        _activeReverseTempFile = revPath;
                    }

                    _fileReader = new AudioFileReader(path);
                    _waveOut = new WaveOutEvent();

                    if (Math.Abs(speed - 1.0f) > 0.02f)
                    {
                        _cleanPitchProvider = new CleanTapeSpeedProvider(_fileReader, speed);
                        _waveOut.Init(_cleanPitchProvider);
                    }
                    else
                    {
                        _cleanPitchProvider = null;
                        _waveOut.Init(_fileReader);
                    }

                    _waveOut.Play();
                }
                catch (Exception ex)
                {
                    throw new Exception("Ошибка инициализации аудио-плеера: " + ex.Message);
                }
            }
        }
        public void ResumePlayback()
        {
            lock (_lockObject)
            {
                if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Paused)
                {
                    _waveOut.Play();
                }
            }
        }

        public void PausePlayback()
        {
            if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing) _waveOut.Pause();
        }

        public void StopPlayback()
        {
            lock (_lockObject)
            {
                _waveOut?.Stop(); _waveOut?.Dispose(); _waveOut = null;
                _fileReader?.Dispose(); _fileReader = null;
                _cleanPitchProvider = null;
                _currentPlaybackPath = null;

                if (!string.IsNullOrEmpty(_activeReverseTempFile) && File.Exists(_activeReverseTempFile))
                {
                    try { File.Delete(_activeReverseTempFile); } catch { }
                    finally { _activeReverseTempFile = null; }
                }
            }
        }

        public void PlayOneShot(string samplePath, float volumeFactor)
        {
            if (!File.Exists(samplePath)) return;
            try
            {
                var output = new WaveOutEvent();
                var reader = new AudioFileReader(samplePath);
                reader.Volume = volumeFactor;
                output.Init(reader);
                output.Play();
                output.PlaybackStopped += (s, e) => { output.Dispose(); reader.Dispose(); };
                _globalLivePadQueue.Enqueue(samplePath);
            }
            catch { }
        }

        public double GetTotalSeconds() => _cleanPitchProvider != null ? _cleanPitchProvider.TotalTimeSeconds : (_fileReader?.TotalTime.TotalSeconds ?? 0);
        public double GetCurrentSeconds() => _cleanPitchProvider != null ? _cleanPitchProvider.CurrentTimeSeconds : (_fileReader?.CurrentTime.TotalSeconds ?? 0);
        
        public void SetPosition(double seconds)
        {
            if (_cleanPitchProvider != null)
            {
                _cleanPitchProvider.SetPositionSeconds(seconds);
            }
            else if (_fileReader != null && seconds >= 0 && seconds <= _fileReader.TotalTime.TotalSeconds)
            {
                _fileReader.CurrentTime = TimeSpan.FromSeconds(seconds);
            }
        }
    }
}