using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Курсовая_ооп
{
    public class DAWTrackProvider : ISampleProvider
    {
        private readonly List<float> _processedSamples = new List<float>();
        private double _currentFrameIndex = 0;
        private readonly WaveFormat _format;
        private readonly float _speed;

        public WaveFormat WaveFormat => _format;
        public double CurrentTimeSeconds => (_currentFrameIndex / WaveFormat.Channels) / WaveFormat.SampleRate;
        public double TotalTimeSeconds => (_processedSamples.Count / WaveFormat.Channels) / (double)WaveFormat.SampleRate;

        public DAWTrackProvider(AudioFileReader reader, double trimStartSec, double trimEndSec, float speed, bool applyReverse)
        {
            _format = reader.WaveFormat;
            _speed = speed;

            int channels = reader.WaveFormat.Channels;
            int sampleRate = reader.WaveFormat.SampleRate;
            List<float> rawSamples = new List<float>();
            float[] tempBuffer = new float[32768];
            int read;
            while ((read = reader.Read(tempBuffer, 0, tempBuffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++) rawSamples.Add(tempBuffer[i]);
            }
            if (applyReverse && rawSamples.Count > 0)
            {
                int totalFrames = rawSamples.Count / channels;
                List<float> reversedSamples = new List<float>(rawSamples.Count);
                for (int f = totalFrames - 1; f >= 0; f--)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        reversedSamples.Add(rawSamples[f * channels + c]);
                    }
                }
                rawSamples = reversedSamples;
            }
            int startSampleIndex = (int)(trimStartSec * sampleRate * channels);
            int endSampleIndex = (int)(trimEndSec * sampleRate * channels);

            if (startSampleIndex < 0) startSampleIndex = 0;
            if (endSampleIndex > rawSamples.Count || endSampleIndex <= startSampleIndex) endSampleIndex = rawSamples.Count;

            for (int i = startSampleIndex; i < endSampleIndex; i++)
            {
                _processedSamples.Add(rawSamples[i]);
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (_processedSamples.Count == 0) return 0;

            int channels = WaveFormat.Channels;
            int totalFrames = _processedSamples.Count / channels;
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
                    float s1 = _processedSamples[frame1 * channels + ch];
                    float s2 = _processedSamples[frame2 * channels + ch];

                    float interpolated = (float)(s1 + (s2 - s1) * fraction);
                    buffer[offset + samplesWritten] = interpolated;
                    samplesWritten++;
                }

                _currentFrameIndex += _speed;
            }

            return samplesWritten;
        }
    }

    public partial class ArrangerWindow : Window
    {
        private string _t1Path = "";
        private string _t2Path = "";
        private string _t3Path = "";

        private WaveOutEvent _mixOut;
        private MixingSampleProvider _mixInputAggregator;
        private DispatcherTimer _playbackTimer;
        private DAWTrackProvider _track1Provider;
        private DAWTrackProvider _track2Provider;
        private DAWTrackProvider _track3Provider;

        private AudioFileReader _r1, _r2, _r3;

        public ArrangerWindow()
        {
            InitializeComponent();
            _playbackTimer = new DispatcherTimer();
            _playbackTimer.Interval = TimeSpan.FromMilliseconds(100);
            _playbackTimer.Tick += PlaybackTimer_Tick;
        }

        private void BtnLoadT1_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Audio|*.wav;*.mp3" };
            if (ofd.ShowDialog() == true) { _t1Path = ofd.FileName; LoadTrackMetadata(1, _t1Path); }
        }

        private void BtnPasteVocal_Click(object sender, RoutedEventArgs e)
        {
            if (this.Owner is MainWindow mainWin && !string.IsNullOrEmpty(mainWin.CurrentActiveTrackPath) && File.Exists(mainWin.CurrentActiveTrackPath))
            {
                _t1Path = mainWin.CurrentActiveTrackPath;
                LoadTrackMetadata(1, _t1Path);
                lblRenderProgress.Text = $"Статус: Активный трек студии [{Path.GetFileName(_t1Path)}] импортирован!";
            }
            else
            {
                MessageBox.Show("На основном пульте студии сейчас нет выбранного или записанного трека!", "Шина пуста");
            }
        }

        private void BtnLoadT2_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Audio|*.wav;*.mp3" };
            if (ofd.ShowDialog() == true) { _t2Path = ofd.FileName; LoadTrackMetadata(2, _t2Path); }
        }

        private void BtnLoadT3_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Audio|*.wav;*.mp3" };
            if (ofd.ShowDialog() == true) { _t3Path = ofd.FileName; LoadTrackMetadata(3, _t3Path); }
        }

        private void LoadTrackMetadata(int trackNum, string path)
        {
            try
            {
                using (var reader = new AudioFileReader(path))
                {
                    double duration = reader.TotalTime.TotalSeconds;
                    if (trackNum == 1)
                    {
                        slT1Play.Maximum = duration; slT1TrimStart.Maximum = duration; slT1TrimEnd.Maximum = duration;
                        slT1TrimEnd.Value = duration; lblT1Name.Text = Path.GetFileName(path);
                    }
                    else if (trackNum == 2)
                    {
                        slT2Play.Maximum = duration; slT2TrimStart.Maximum = duration; slT2TrimEnd.Maximum = duration;
                        slT2TrimEnd.Value = duration; lblT2Name.Text = Path.GetFileName(path);
                    }
                    else if (trackNum == 3)
                    {
                        slT3Play.Maximum = duration; slT3TrimStart.Maximum = duration; slT3TrimEnd.Maximum = duration;
                        slT3TrimEnd.Value = duration; lblT3Name.Text = Path.GetFileName(path);
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Ошибка импорта метаданных: " + ex.Message); }
        }

        private void SlT1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lblT1TrimStartVal == null) return;
            lblT1TrimStartVal.Text = $"{slT1TrimStart.Value:F1} с";
            lblT1TrimEndVal.Text = $"{slT1TrimEnd.Value:F1} с";
        }

        private void SlT2_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lblT2TrimStartVal == null) return;
            lblT2TrimStartVal.Text = $"{slT2TrimStart.Value:F1} с";
            lblT2TrimEndVal.Text = $"{slT2TrimEnd.Value:F1} с";
        }

        private void SlT3_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lblT3TrimStartVal == null) return;
            lblT3TrimStartVal.Text = $"{slT3TrimStart.Value:F1} с";
            lblT3TrimEndVal.Text = $"{slT3TrimEnd.Value:F1} с";
        }

        private void BtnT1TrimStartMinus_Click(object sender, RoutedEventArgs e) => slT1TrimStart.Value = Math.Max(0, slT1TrimStart.Value - 1);
        private void BtnT1TrimStartPlus_Click(object sender, RoutedEventArgs e) => slT1TrimStart.Value = Math.Min(slT1TrimStart.Maximum, slT1TrimStart.Value + 1);
        private void BtnT1TrimEndMinus_Click(object sender, RoutedEventArgs e) => slT1TrimEnd.Value = Math.Max(0, slT1TrimEnd.Value - 1);
        private void BtnT1TrimEndPlus_Click(object sender, RoutedEventArgs e) => slT1TrimEnd.Value = Math.Min(slT1TrimEnd.Maximum, slT1TrimEnd.Value + 1);

        private void BtnT2TrimStartMinus_Click(object sender, RoutedEventArgs e) => slT2TrimStart.Value = Math.Max(0, slT2TrimStart.Value - 1);
        private void BtnT2TrimStartPlus_Click(object sender, RoutedEventArgs e) => slT2TrimStart.Value = Math.Min(slT2TrimStart.Maximum, slT2TrimStart.Value + 1);
        private void BtnT2TrimEndMinus_Click(object sender, RoutedEventArgs e) => slT2TrimEnd.Value = Math.Max(0, slT2TrimEnd.Value - 1);
        private void BtnT2TrimEndPlus_Click(object sender, RoutedEventArgs e) => slT2TrimEnd.Value = Math.Min(slT2TrimEnd.Maximum, slT2TrimEnd.Value + 1);

        private void BtnT3TrimStartMinus_Click(object sender, RoutedEventArgs e) => slT3TrimStart.Value = Math.Max(0, slT3TrimStart.Value - 1);
        private void BtnT3TrimStartPlus_Click(object sender, RoutedEventArgs e) => slT3TrimStart.Value = Math.Min(slT3TrimStart.Maximum, slT3TrimStart.Value + 1);
        private void BtnT3TrimEndMinus_Click(object sender, RoutedEventArgs e) => slT3TrimEnd.Value = Math.Max(0, slT3TrimEnd.Value - 1);
        private void BtnT3TrimEndPlus_Click(object sender, RoutedEventArgs e) => slT3TrimEnd.Value = Math.Min(slT3TrimEnd.Maximum, slT3TrimEnd.Value + 1);

        private void BtnPlayMix_Click(object sender, RoutedEventArgs e)
        {
            StopMixPlayback();

            if (string.IsNullOrEmpty(_t1Path) && string.IsNullOrEmpty(_t2Path) && string.IsNullOrEmpty(_t3Path))
            {
                MessageBox.Show("Дорожки пульта пусты!", "Нечего сводить");
                return;
            }

            try
            {
                lblRenderProgress.Text = "Статус: Синхронное проигрывание мультитрека...";
                _mixInputAggregator = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
                float activeStudioSpeed = 1.0f;
                bool activeStudioReverse = false;
                if (this.Owner is MainWindow mainWin)
                {
                    activeStudioSpeed = (float)(mainWin.slSpeed.Value / 10.0);
                    activeStudioReverse = mainWin.IsReverseActive;
                }

                if (!string.IsNullOrEmpty(_t1Path) && File.Exists(_t1Path))
                {
                    _r1 = new AudioFileReader(_t1Path);
                    _track1Provider = new DAWTrackProvider(_r1, slT1TrimStart.Value, slT1TrimEnd.Value, activeStudioSpeed, activeStudioReverse);
                    ISampleProvider stereo = _track1Provider.WaveFormat.Channels == 1 ? new MonoToStereoSampleProvider(_track1Provider) : (ISampleProvider)_track1Provider;
                    _mixInputAggregator.AddMixerInput(new WdlResamplingSampleProvider(stereo, 44100));
                }

                if (!string.IsNullOrEmpty(_t2Path) && File.Exists(_t2Path))
                {
                    _r2 = new AudioFileReader(_t2Path);
                    _track2Provider = new DAWTrackProvider(_r2, slT2TrimStart.Value, slT2TrimEnd.Value, 1.0f, false);
                    ISampleProvider stereo = _track2Provider.WaveFormat.Channels == 1 ? new MonoToStereoSampleProvider(_track2Provider) : (ISampleProvider)_track2Provider;
                    _mixInputAggregator.AddMixerInput(new WdlResamplingSampleProvider(stereo, 44100));
                }

                if (!string.IsNullOrEmpty(_t3Path) && File.Exists(_t3Path))
                {
                    _r3 = new AudioFileReader(_t3Path);
                    _track3Provider = new DAWTrackProvider(_r3, slT3TrimStart.Value, slT3TrimEnd.Value, 1.0f, false);
                    ISampleProvider stereo = _track3Provider.WaveFormat.Channels == 1 ? new MonoToStereoSampleProvider(_track3Provider) : (ISampleProvider)_track3Provider;
                    _mixInputAggregator.AddMixerInput(new WdlResamplingSampleProvider(stereo, 44100));
                }

                _mixOut = new WaveOutEvent();
                _mixOut.Init(_mixInputAggregator);
                _mixOut.Play();
                _playbackTimer.Start();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка пульта превью аранжировки: " + ex.Message); }
        }

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (_track1Provider != null) { slT1Play.Value = _track1Provider.CurrentTimeSeconds; lblT1Time.Text = TimeSpan.FromSeconds(_track1Provider.CurrentTimeSeconds).ToString(@"mm\:ss"); }
            if (_track2Provider != null) { slT2Play.Value = _track2Provider.CurrentTimeSeconds; lblT2Time.Text = TimeSpan.FromSeconds(_track2Provider.CurrentTimeSeconds).ToString(@"mm\:ss"); }
            if (_track3Provider != null) { slT3Play.Value = _track3Provider.CurrentTimeSeconds; lblT3Time.Text = TimeSpan.FromSeconds(_track3Provider.CurrentTimeSeconds).ToString(@"mm\:ss"); }
        }

        private void BlackboxResetSliders()
        {
            slT1Play.Value = 0; lblT1Time.Text = "00:00";
            slT2Play.Value = 0; lblT2Time.Text = "00:00";
            slT3Play.Value = 0; lblT3Time.Text = "00:00";
        }

        private void BtnStopMix_Click(object sender, RoutedEventArgs e)
        {
            StopMixPlayback();
            lblRenderProgress.Text = "Статус: Воспроизведение микса остановлено.";
        }

        private void StopMixPlayback()
        {
            _playbackTimer.Stop();
            _mixOut?.Stop(); _mixOut?.Dispose(); _mixOut = null;
            
            _r1?.Dispose(); _r1 = null;
            _r2?.Dispose(); _r2 = null;
            _r3?.Dispose(); _r3 = null;

            _track1Provider = null;
            _track2Provider = null;
            _track3Provider = null;

            BlackboxResetSliders();
        }

        private void BtnBounce_Click(object sender, RoutedEventArgs e)
        {
            StopMixPlayback();

            if (string.IsNullOrEmpty(_t1Path) && string.IsNullOrEmpty(_t2Path) && string.IsNullOrEmpty(_t3Path))
            {
                MessageBox.Show("Сетка пуста! Нечего рендерить.", "Ошибка");
                return;
            }

            string outputName = $"Arranged_Mix_By_[MultiTrack]_{DateTime.Now:HHmmss}.wav";
            string outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, outputName);

            try
            {
                lblRenderProgress.Text = "Статус: Финальный рендеринг мастер-файла с учетом питча и реверса...";
                btnBounce.IsEnabled = false;

                var finalMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));

                float activeStudioSpeed = 1.0f;
                bool activeStudioReverse = false;
                if (this.Owner is MainWindow mainWin)
                {
                    activeStudioSpeed = (float)(mainWin.slSpeed.Value / 10.0);
                    activeStudioReverse = mainWin.IsReverseActive;
                }

                if (!string.IsNullOrEmpty(_t1Path) && File.Exists(_t1Path))
                {
                    var reader = new AudioFileReader(_t1Path);
                    var track = new DAWTrackProvider(reader, slT1TrimStart.Value, slT1TrimEnd.Value, activeStudioSpeed, activeStudioReverse);
                    ISampleProvider stereo = track.WaveFormat.Channels == 1 ? new MonoToStereoSampleProvider(track) : (ISampleProvider)track;
                    finalMixer.AddMixerInput(new WdlResamplingSampleProvider(stereo, 44100));
                }

                if (!string.IsNullOrEmpty(_t2Path) && File.Exists(_t2Path))
                {
                    var reader = new AudioFileReader(_t2Path);
                    var track = new DAWTrackProvider(reader, slT2TrimStart.Value, slT2TrimEnd.Value, 1.0f, false);
                    ISampleProvider stereo = track.WaveFormat.Channels == 1 ? new MonoToStereoSampleProvider(track) : (ISampleProvider)track;
                    finalMixer.AddMixerInput(new WdlResamplingSampleProvider(stereo, 44100));
                }

                if (!string.IsNullOrEmpty(_t3Path) && File.Exists(_t3Path))
                {
                    var reader = new AudioFileReader(_t3Path);
                    var track = new DAWTrackProvider(reader, slT3TrimStart.Value, slT3TrimEnd.Value, 1.0f, false);
                    ISampleProvider stereo = track.WaveFormat.Channels == 1 ? new MonoToStereoSampleProvider(track) : (ISampleProvider)track;
                    finalMixer.AddMixerInput(new WdlResamplingSampleProvider(stereo, 44100));
                }

                WaveFileWriter.CreateWaveFile16(outputPath, finalMixer);

                lblRenderProgress.Text = "Статус: Успешно сведено с учетом эффектов!";
                MessageBox.Show($"Композиция успешно сведена и отправлена в Архив студии!\nИмя файла: {outputName}", "Рендеринг завершен");
                this.Close();
            }
            catch (Exception ex)
            {
                lblRenderProgress.Text = "Статус: Сбой рендеринга!";
                MessageBox.Show($"Критический сбой финального суммирования: {ex.Message}", "Ошибка");
            }
            finally
            {
                btnBounce.IsEnabled = true;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            StopMixPlayback();
            this.Close();
        }
    }
}