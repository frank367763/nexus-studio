using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Text.Json;
using Microsoft.Win32;
using NAudio.Wave;
using Курсовая_ооп.Core;

namespace Курсовая_ооп
{
    public partial class MainWindow : Window
    {
        private StudioManager _studio = new StudioManager();
        private RecordingSession _session;
        private DispatcherTimer _timer;
        private AudioEngine _samplerEngine = new AudioEngine();
        private AudioEngine _archivePreviewEngine = new AudioEngine();
        public System.Collections.Generic.List<SoundEngineer> EngineersList = new System.Collections.Generic.List<SoundEngineer>();
        public string CurrentActiveTrackPath { get; set; } = "";
        public bool IsReverseActive { get; set; } = false;
        private string _beatPath = "";
        private bool _isSliderDragging = false;
        private string _ch1KickPath = "";
        private string _ch2SnarePath = "";
        private string _ch3ClapPath = "";
        private string _ch4CustomPath = "";

        public MainWindow()
        {
            InitializeComponent();
            InitAudioStation();
        }
        private void InitAudioStation()
        {
            try
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromMilliseconds(50);
                _timer.Tick += Timer_Tick;
                lblSavePath.Text = "Папка локальной базы: " + AppDomain.CurrentDomain.BaseDirectory;
                string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                _ch1KickPath = Path.Combine(winDir, "Media", "tada.wav");
                _ch2SnarePath = Path.Combine(winDir, "Media", "notify.wav");
                _ch3ClapPath = Path.Combine(winDir, "Media", "chimes.wav");
                if (!LoadCollectionsFromDisk())
                {
                    _studio.ArtistRoster.Add(new Artist("Pharaoh", "Cloud Rap", "Топовый артист"));
                    _studio.ArtistRoster.Add(new Artist("Mona", "Pop / Soul", "Чистый глубокий вокал"));
                    _studio.ArtistRoster.Add(new Artist("Скриптонит", "Experimental", "Сложные ритмические структуры"));

                    EngineersList.Add(new SoundEngineer("Никита", 5, "Сведение", "Шеф-режиссер", "Специализация на хип-хоп вокале"));
                    EngineersList.Add(new SoundEngineer("Илья", 8, "Мастеринг", "Старший инженер", "Работа со стерео-шиной и аналоговым железом"));
                    
                    SaveCollectionsToDisk();
                }
                _studio.MainRack.Add("MIC_MAIN", new Microphone("Neumann U87 Ai", "Конденсаторный Студийный"));
                UpdatePersonnelDropdowns();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критический сбой инициализации звуковых плат: {ex.Message}", "Ошибка ядра", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public void SyncDropdownsPublic()
        {
            UpdatePersonnelDropdowns();
            SaveCollectionsToDisk(); // Автоматически сохраняем изменения базы на жесткий диск
        }
        private void UpdatePersonnelDropdowns()
        {
            cbArtists.Items.Clear();
            foreach (var artist in _studio.ArtistRoster) cbArtists.Items.Add(artist.Name);
            if (cbArtists.Items.Count > 0) cbArtists.SelectedIndex = 0;

            cbEngineers.Items.Clear();
            foreach (var eng in EngineersList) cbEngineers.Items.Add(eng.Name);
            if (cbEngineers.Items.Count > 0) cbEngineers.SelectedIndex = 0;

            cbFilterArtist.Items.Clear();
            cbFilterArtist.Items.Add("Все артисты");
            foreach (var artist in _studio.ArtistRoster) cbFilterArtist.Items.Add(artist.Name);
            cbFilterArtist.SelectedIndex = 0;

            cbFilterEngineer.Items.Clear();
            cbFilterEngineer.Items.Add("Все инженеры");
            foreach (var eng in EngineersList) cbFilterEngineer.Items.Add(eng.Name);
            cbFilterEngineer.SelectedIndex = 0;
        }

        private void SaveCollectionsToDisk()
        {
            try
            {
                string artistJson = JsonSerializer.Serialize(_studio.ArtistRoster, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "artists.json"), artistJson);

                string engJson = JsonSerializer.Serialize(EngineersList, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engineers.json"), engJson);
            }
            catch (Exception ex)
            {
                Log($"[БД] Не удалось сохранить списки на диск: {ex.Message}");
            }
        }

        private bool LoadCollectionsFromDisk()
        {
            try
            {
                string artistPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "artists.json");
                string engPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engineers.json");

                if (!File.Exists(artistPath) || !File.Exists(engPath)) return false;

                string artistJson = File.ReadAllText(artistPath);
                var loadedArtists = JsonSerializer.Deserialize<System.Collections.Generic.List<Artist>>(artistJson);
                if (loadedArtists != null)
                {
                    _studio.ArtistRoster.Clear();
                    _studio.ArtistRoster.AddRange(loadedArtists);
                }

                string engJson = File.ReadAllText(engPath);
                var loadedEngineers = JsonSerializer.Deserialize<System.Collections.Generic.List<SoundEngineer>>(engJson);
                if (loadedEngineers != null)
                {
                    EngineersList.Clear();
                    EngineersList.AddRange(loadedEngineers);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void BtnNavStudio_Click(object sender, RoutedEventArgs e)
        {
            gridStudio.Visibility = Visibility.Visible;
            gridArchive.Visibility = Visibility.Collapsed;

            btnNavStudio.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00FFAA"));
            btnNavStudio.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00FFAA"));
            btnNavArchive.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
            btnNavArchive.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#252535"));
        }

        private void BtnNavArchive_Click(object sender, RoutedEventArgs e)
        {
            gridStudio.Visibility = Visibility.Collapsed;
            gridArchive.Visibility = Visibility.Visible;

            btnNavArchive.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00FFAA"));
            btnNavArchive.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00FFAA"));
            btnNavStudio.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
            btnNavStudio.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#252535"));

            RefreshFileArchive();
        }

        private void BtnLoadBeat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = "Аудиофайлы (*.wav;*.mp3)|*.wav;*.mp3";
                ofd.Title = "Выберите фоновый минус для записи вокала";

                if (ofd.ShowDialog() == true)
                {
                    _beatPath = ofd.FileName;
                    lblBeatStatus.Text = "Минус: " + Path.GetFileName(_beatPath);
                    Log($"[СИСТЕМА] В линию фонового мониторинга подан бит: {Path.GetFileName(_beatPath)}");
                }
            }
            catch (Exception ex) { MessageBox.Show("Ошибка выбора файла: " + ex.Message, "Сбой"); }
        }

        private void BtnLoadKick_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Audio|*.wav;*.mp3" };
            if (ofd.ShowDialog() == true) { _ch1KickPath = ofd.FileName; Log($"[СЭМПЛЕР] Канал CH1 (KICK) переназначен: {Path.GetFileName(_ch1KickPath)}"); }
        }

        private void BtnLoadSnare_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Audio|*.wav;*.mp3" };
            if (ofd.ShowDialog() == true) { _ch2SnarePath = ofd.FileName; Log($"[СЭМПЛЕР] Канал CH2 (SNARE) переназначен: {Path.GetFileName(_ch2SnarePath)}"); }
        }

        private void BtnLoadClap_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Audio|*.wav;*.mp3" };
            if (ofd.ShowDialog() == true) { _ch3ClapPath = ofd.FileName; Log($"[СЭМПЛЕР] Канал CH3 (CLAP) переназначен: {Path.GetFileName(_ch3ClapPath)}"); }
        }

        private void BtnLoadCustom_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Audio|*.wav;*.mp3", Title = "Загрузка пользовательского сэмпла на CH4" };
            if (ofd.ShowDialog() == true)
            {
                _ch4CustomPath = ofd.FileName;
                btnCustomPad.Content = Path.GetFileNameWithoutExtension(_ch4CustomPath).ToUpper();
                btnCustomPad.IsEnabled = true;
                Log($"[СЭМПЛЕР] Канал CH4 (USER) успешно инициализирован: {Path.GetFileName(_ch4CustomPath)}");
            }
        }

        private void BtnKick_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_ch1KickPath)) _samplerEngine.PlayOneShot(_ch1KickPath, (float)(slKickVol.Value / 100.0));
        }

        private void BtnSnare_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_ch2SnarePath)) _samplerEngine.PlayOneShot(_ch2SnarePath, (float)(slSnareVol.Value / 100.0));
        }

        private void BtnClap_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_ch3ClapPath)) _samplerEngine.PlayOneShot(_ch3ClapPath, (float)(slClapVol.Value / 100.0));
        }

        private void BtnCustomPad_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(_ch4CustomPath)) _samplerEngine.PlayOneShot(_ch4CustomPath, (float)(slCustomVol.Value / 100.0));
        }

        private void BtnRoute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _session?.Stop();
                if (string.IsNullOrWhiteSpace(txtTrackTitle.Text))
                    throw new StudioException("Невозможно открыть шину: имя трека пустое.", "ERR_BLANK_TITLE");

                if (cbArtists.SelectedItem == null || cbEngineers.SelectedItem == null)
                    throw new StudioException("Не выбран кадровый персонал для фиксации сессии.", "ERR_NO_PERSONNEL");

                string selectedArtist = cbArtists.SelectedItem.ToString();
                string selectedEngineer = cbEngineers.SelectedItem.ToString();

                string safeTitle = $"{txtTrackTitle.Text}_By_[{selectedArtist}]_Eng_[{selectedEngineer}]_{DateTime.Now:HHmmss}";

                Artist artist = _studio.ArtistRoster.Find(a => a.Name == selectedArtist);
                SoundEngineer engineer = EngineersList.Find(x => x.Name == selectedEngineer);

                if (artist == null) artist = new Artist(selectedArtist, "Pop");
                if (engineer == null) engineer = new SoundEngineer(selectedEngineer, 5, "Сведение", "Инженер");

                _session = new RecordingSession(safeTitle, artist, engineer);
                _session.BeatFilePath = _beatPath;
                _session.StateChanged += (src, msg) => Log(msg);
                CurrentActiveTrackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, safeTitle + ".wav");

                lblActiveTrack.Text = "АКТИВНЫЙ ТРЕК: " + safeTitle + ".wav";
                Log("--- СТУДИЙНАЯ ШИНА АУДИОМАРШРУТИЗАЦИИ УСПЕШНО ЗАПУЩЕНА ---");
                Log(artist.GetWorkDescription());
                Log(engineer.GetWorkDescription());
                Log(_studio.GetEquipment("MIC_MAIN").PowerOn());

                btnRecord.IsEnabled = true;
            }
            catch (StudioException ex)
            {
                MessageBox.Show($"{ex.Message}\nКод ошибки ядра: {ex.ErrorCode}", "Сбой маршрутизации", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Непредвиденный сбой коммутации: " + ex.Message, "Ошибка");
            }
        }

        private void BtnRecord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _session?.StartRecording();
                btnRecord.IsEnabled = false;
                btnStop.IsEnabled = true;
            }
            catch (Exception ex) { MessageBox.Show("Не удалось запустить микрофонную шину: " + ex.Message, "Сбой записи"); }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _session?.StopRecording();
                btnRecord.IsEnabled = true;
                btnStop.IsEnabled = false;
                
                btnPlay.IsEnabled = true;
                btnStopPlay.IsEnabled = true;
                btnReverse.IsEnabled = true;
            }
            catch (Exception ex) { MessageBox.Show("Ошибка остановки записи: " + ex.Message); }
        }

        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_session == null) return;

                if (_session.IsPlaying)
                {
                    _session.Pause();
                    btnPlay.Content = "▶ RESUME";
                }
                else
                {
                    IsReverseActive = false; // Снимаем флаг реверса при обычном воспроизведении
                    double currentPositionSeconds = slStudioTimeline.Value;
                    float pitchSpeed = (float)(slSpeed.Value / 10.0);
                    
                    _session.PlayProcessed(pitchSpeed, false);
                    slStudioTimeline.Maximum = _session.TotalSeconds;
                    
                    _session.Seek(currentPositionSeconds);
                    
                    _timer.Start();
                    btnPlay.Content = "⏸ PAUSE";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка аудио-декадера: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnReverse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsReverseActive = true; // Активируем флаг реверса для аранжировщика
                float pitchSpeed = (float)(slSpeed.Value / 10.0);
                _session?.PlayProcessed(pitchSpeed, true); 
                if (_session != null) slStudioTimeline.Maximum = _session.TotalSeconds;
                _timer.Start();
            }
            catch (Exception ex) { Log($"[КРИТ] Ошибка репроцессинга реверса: {ex.Message}"); }
        }

        private void BtnStopPlay_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IsReverseActive = false; // Сбрасываем эффекты на дефолт при полном стопе
                _session?.Stop();
                _timer.Stop();
                slStudioTimeline.Value = 0;
                lblStudioTime.Text = "00:00 / 00:00";
                btnPlay.Content = "▶ PLAY / PAUSE";
            }
            catch (Exception ex) { MessageBox.Show("Ошибка сброса: " + ex.Message); }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_session != null && _session.TotalSeconds > 0)
            {
                if (!_isSliderDragging)
                {
                    slStudioTimeline.Value = _session.CurrentSeconds;
                }
                TimeSpan current = TimeSpan.FromSeconds(_session.CurrentSeconds);
                TimeSpan total = TimeSpan.FromSeconds(_session.TotalSeconds);
                lblStudioTime.Text = $"{current:mm\\:ss} / {total:mm\\:ss}";
            }
        }

        private void SlStudioTimeline_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isSliderDragging = true;
        }

        private void SlStudioTimeline_Seek(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                _isSliderDragging = false;
                _session?.Seek(slStudioTimeline.Value); 
            }
            catch (Exception ex) { Log("Ошибка перемотки: " + ex.Message); }
        }

        private void SlSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (lblSpeedVal != null) lblSpeedVal.Text = $"{e.NewValue / 10.0:F1}x";
        }

        private void BtnOpenArranger_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _session?.Stop();
                _archivePreviewEngine?.StopPlayback();
                
                ArrangerWindow arranger = new ArrangerWindow();
                arranger.Owner = this; 
                arranger.ShowDialog(); 
            }
            catch (Exception ex) { MessageBox.Show("Не удалось запустить пульт аранжировки: " + ex.Message); }
        }

        private void BtnOpenPersonnel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _session?.Stop();
                _archivePreviewEngine?.StopPlayback();

                PersonnelWindow hrWindow = new PersonnelWindow(_studio, this);
                hrWindow.Owner = this;
                hrWindow.ShowDialog();
            }
            catch (Exception ex) { MessageBox.Show("Не удалось открыть кадровый реестр: " + ex.Message); }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog { Filter = "Audio|*.wav;*.mp3", Title = "Импорт внешнего трека в архив" };
                if (ofd.ShowDialog() == true)
                {
                    string destination = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileName(ofd.FileName));
                    if (!File.Exists(destination)) File.Copy(ofd.FileName, destination);
                    RefreshFileArchive();
                    MessageBox.Show("Файл успешно импортирован в архивный репозиторий студии!", "Импорт завершен");
                }
            }
            catch (Exception ex) { MessageBox.Show("Ошибка импорта: " + ex.Message); }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lbFiles.SelectedItem == null) return;
                
                string selectedLine = lbFiles.SelectedItem.ToString();
                string fileName = selectedLine.Substring(0, selectedLine.IndexOf("  [")).Trim();
                string source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

                SaveFileDialog sfd = new SaveFileDialog { FileName = fileName, Filter = "Audio File|*.wav" };
                if (sfd.ShowDialog() == true)
                {
                    File.Copy(source, sfd.FileName, true);
                    MessageBox.Show("Мастер-микс успешно выгружен на внешний носитель!", "Экспорт");
                }
            }
            catch (Exception ex) { MessageBox.Show("Ошибка экспорта: " + ex.Message); }
        }

        private void RefreshFileArchive()
        {
            try
            {
                lbFiles.Items.Clear();
                string path = AppDomain.CurrentDomain.BaseDirectory;
                string[] rawFiles = Directory.GetFiles(path, "*.*");

                string targetArtist = cbFilterArtist.SelectedItem?.ToString() ?? "Все артисты";
                string targetEngineer = cbFilterEngineer.SelectedItem?.ToString() ?? "Все инженеры";

                foreach (var f in rawFiles)
                {
                    string name = Path.GetFileName(f);
                    if (name.EndsWith(".wav") || name.EndsWith(".mp3"))
                    {
                        if (targetArtist != "Все артисты" && !name.Contains($"_By_[{targetArtist}]")) continue;
                        if (targetEngineer != "Все инженеры" && !name.Contains($"_Eng_[{targetEngineer}]")) continue;

                        using (var reader = new AudioFileReader(f))
                        {
                            lbFiles.Items.Add($"{name}  [{reader.TotalTime:mm\\:ss}]");
                        }
                    }
                }
            }
            catch { }
        }

        private void CbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshFileArchive(); 
        }

        private void BtnResetFilters_Click(object sender, RoutedEventArgs e)
        {
            cbFilterArtist.SelectedIndex = 0;
            cbFilterEngineer.SelectedIndex = 0;
            RefreshFileArchive();
        }

        private void LbFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (lbFiles.SelectedItem == null) return;
                string selectedLine = lbFiles.SelectedItem.ToString();
                string fileName = selectedLine.Substring(0, selectedLine.IndexOf("  [")).Trim();
                
                _session?.Stop();
                _session = new RecordingSession(fileName, new Artist("Archive", "Studio"), new SoundEngineer("Archive", 0, "Studio", "Инженер"));
                CurrentActiveTrackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            }
            catch { }
        }

        private void BtnLoadToStudio_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lbFiles.SelectedItem == null) return;
                
                string selectedLine = lbFiles.SelectedItem.ToString();
                string fileName = selectedLine.Substring(0, selectedLine.IndexOf("  [")).Trim();
                
                _session?.Stop();
                _archivePreviewEngine?.StopPlayback();

                Artist a = _studio.ArtistRoster.Find(x => x.Name == cbArtists.SelectedItem?.ToString()) ?? new Artist("Неизвестный", "Pop");
                SoundEngineer eng = EngineersList.Find(x => x.Name == cbEngineers.SelectedItem?.ToString()) ?? new SoundEngineer("Studio", 0, "Сведение", "Инженер");
                
                _session = new RecordingSession(fileName, a, eng);
                _session.StateChanged += (src, msg) => Log(msg);

                CurrentActiveTrackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

                lblActiveTrack.Text = "АКТИВНЫЙ ТРЕК: " + fileName;
                btnPlay.IsEnabled = true; btnStopPlay.IsEnabled = true; btnReverse.IsEnabled = true;
                
                BtnNavStudio_Click(null, null);
                Log($"[СИСТЕМА] Трек '{fileName}' успешно выгружен из архива на пульт студии.");
            }
            catch (Exception ex) { MessageBox.Show("Ошибка выгрузки трека на пульт: " + ex.Message); }
        }

        private void MenuPlayArchive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lbFiles.SelectedItem == null) return;
                string selectedLine = lbFiles.SelectedItem.ToString();
                string fileName = selectedLine.Substring(0, selectedLine.IndexOf("  [")).Trim();
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

                if (File.Exists(fullPath))
                {
                    _session?.Stop();
                    _archivePreviewEngine.PlayFile(fullPath, 1.0f);
                    Log($"[АРХИВ] Запущен плеер предпрослушивания трека: {fileName}");
                }
            }
            catch (Exception ex) { MessageBox.Show("Не удалось запустить превью: " + ex.Message); }
        }

        private void MenuStopArchive_Click(object sender, RoutedEventArgs e)
        {
            _archivePreviewEngine.StopPlayback();
            Log("[АРХИВ] Воспроизведение превью файла остановлено.");
        }

        private void MenuDeleteArchive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (lbFiles.SelectedItem == null) return;
                string selectedLine = lbFiles.SelectedItem.ToString();
                string fileName = selectedLine.Substring(0, selectedLine.IndexOf("  [")).Trim();
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

                var result = MessageBox.Show($"Вы уверены, что хотите НАВСЕГДА стереть файл {fileName} с жесткого диска компьютера?", "Внимание: удаление файла", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    _archivePreviewEngine.StopPlayback();
                    _session?.Stop();
                    
                    if (File.Exists(fullPath)) File.Delete(fullPath);
                    RefreshFileArchive();
                    
                    if (CurrentActiveTrackPath == fullPath) CurrentActiveTrackPath = "";

                    Log($"[БД] Файл '{fileName}' успешно удален из репозитория диска.");
                }
            }
            catch (Exception ex) { MessageBox.Show("Сбой при физическом удалении файла: " + ex.Message, "Файл заблокирован"); }
        }

        private void Log(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                lbLog.Items.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
                lbLog.ScrollIntoView(lbLog.Items[lbLog.Items.Count - 1]);
            });
        }
    }
}