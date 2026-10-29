using System;
using System.Windows;
using System.Windows.Controls;
using Курсовая_ооп.Core;

namespace Курсовая_ооп
{
    public partial class PersonnelWindow : Window
    {
        private StudioManager _studio;
        private MainWindow _main;

        public PersonnelWindow(StudioManager studio, MainWindow main)
        {
            InitializeComponent();
            _studio = studio;
            _main = main;
            RefreshLists();
        }

        private void RefreshLists()
        {
            lbArtists.Items.Clear();
            foreach (var a in _studio.ArtistRoster) lbArtists.Items.Add(a.Name);

            lbEngineers.Items.Clear();
            foreach (var e in _main.EngineersList) lbEngineers.Items.Add(e.Name);
            
            txtProfileInfo.Text = "Выберите сотрудника для просмотра досье.";
            btnFire.IsEnabled = false;
        }

        private void LbArtists_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbArtists.SelectedItem == null) return;
            lbEngineers.SelectedItem = null; // сброс соседа
            
            var artist = _studio.ArtistRoster.Find(a => a.Name == lbArtists.SelectedItem.ToString());
            if (artist != null)
            {
                txtProfileInfo.Text = $"✦ КАРТОЧКА АРТИСТА ✦\n\nФИО: {artist.Name}\nЖанр: {artist.Genre}\n\nПримечания:\n{artist.Notes}";
                btnFire.IsEnabled = true;
            }
        }

        private void LbEngineers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lbEngineers.SelectedItem == null) return;
            lbArtists.SelectedItem = null; // сброс соседа

            var eng = _main.EngineersList.Find(x => x.Name == lbEngineers.SelectedItem.ToString());
            if (eng != null)
            {
                txtProfileInfo.Text = $"✦ КАРТОЧКА СПЕЦИАЛИСТА ✦\n\nФИО: {eng.Name}\nСтаж работы: {eng.Experience} л.\nПрофиль: {eng.Profile}\nДолжность: {eng.Position}\n\nПримечания:\n{eng.Notes}";
                btnFire.IsEnabled = true;
            }
        }

        private void BtnFire_Click(object sender, RoutedEventArgs e)
        {
            if (lbArtists.SelectedItem != null)
            {
                _studio.ArtistRoster.RemoveAll(x => x.Name == lbArtists.SelectedItem.ToString());
            }
            else if (lbEngineers.SelectedItem != null)
            {
                _main.EngineersList.RemoveAll(x => x.Name == lbEngineers.SelectedItem.ToString());
            }
            RefreshLists();
            _main.SyncDropdownsPublic();
        }

        private void BtnAddArtist_Click(object sender, RoutedEventArgs e)
        {
            _studio.ArtistRoster.Add(new Artist(addArtistName.Text, addArtistGenre.Text, addArtistNotes.Text));
            RefreshLists();
            _main.SyncDropdownsPublic();
        }

        private void BtnAddEngineer_Click(object sender, RoutedEventArgs e)
        {
            int.TryParse(addEngExp.Text, out int exp);
            _main.EngineersList.Add(new SoundEngineer(addEngName.Text, exp, addEngProfile.Text, addEngPosition.Text, addEngNotes.Text));
            RefreshLists();
            _main.SyncDropdownsPublic();
        }
    }
}