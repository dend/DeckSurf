using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeckSurf.App.Services;

namespace DeckSurf.App.ViewModels
{
    /// <summary>
    /// The runtime activity feed: every session event, newest first, filterable by
    /// the profile it came from.
    /// </summary>
    public partial class ActivityViewModel : ObservableObject
    {
        private const int MaxEntries = 500;
        private const string AllProfilesFilter = "All profiles";

        private readonly WindowService windowService;
        private readonly List<ActivityEntry> allEntries = [];

        public ActivityViewModel(RuntimeService runtimeService, WindowService windowService)
        {
            this.windowService = windowService;
            runtimeService.ActivityLogged += OnActivityLogged;
            ProfileFilters.Add(AllProfilesFilter);
            SelectedFilter = AllProfilesFilter;
        }

        public ObservableCollection<ActivityEntry> Entries { get; } = [];

        public ObservableCollection<string> ProfileFilters { get; } = [];

        [ObservableProperty]
        public partial string? SelectedFilter { get; set; }

        public bool HasEntries => Entries.Count > 0;

        public bool HasNoEntries => Entries.Count == 0;

        [RelayCommand]
        private void Clear()
        {
            allEntries.Clear();
            Entries.Clear();

            // Filters for profiles no longer represented reset with the log.
            for (var i = ProfileFilters.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(ProfileFilters[i], AllProfilesFilter, StringComparison.Ordinal))
                {
                    ProfileFilters.RemoveAt(i);
                }
            }

            SelectedFilter = AllProfilesFilter;
            NotifyCountsChanged();
        }

        partial void OnSelectedFilterChanged(string? value)
        {
            RebuildEntries();
        }

        private void OnActivityLogged(object? sender, ActivityEntry entry)
        {
            windowService.RunOnUIThread(() =>
            {
                allEntries.Insert(0, entry);
                if (allEntries.Count > MaxEntries)
                {
                    allEntries.RemoveAt(allEntries.Count - 1);
                }

                if (entry.ProfileName is not null && !ProfileFilters.Contains(entry.ProfileName))
                {
                    ProfileFilters.Add(entry.ProfileName);
                }

                if (Matches(entry))
                {
                    Entries.Insert(0, entry);
                    while (Entries.Count > MaxEntries)
                    {
                        Entries.RemoveAt(Entries.Count - 1);
                    }
                }

                NotifyCountsChanged();
            });
        }

        private bool Matches(ActivityEntry entry) =>
            SelectedFilter is null
            || string.Equals(SelectedFilter, AllProfilesFilter, StringComparison.Ordinal)
            || string.Equals(entry.ProfileName, SelectedFilter, StringComparison.OrdinalIgnoreCase);

        private void RebuildEntries()
        {
            Entries.Clear();
            foreach (var entry in allEntries.Where(Matches))
            {
                Entries.Add(entry);
            }

            NotifyCountsChanged();
        }

        private void NotifyCountsChanged()
        {
            OnPropertyChanged(nameof(HasEntries));
            OnPropertyChanged(nameof(HasNoEntries));
        }
    }
}
