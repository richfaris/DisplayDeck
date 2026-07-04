using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplayDeck.Core.Models;

namespace DisplayDeck.App.ViewModels;

/// <summary>
/// Backs the Profiles page: lists saved profiles, saves the current configuration
/// as a new profile, and applies/deletes/renames existing ones. All applies flow
/// through the main view model so they share the auto-revert safety net.
/// </summary>
public sealed partial class ProfilesViewModel : ObservableObject
{
    private readonly MainViewModel _main;

    public ProfilesViewModel(MainViewModel main)
    {
        _main = main;
        Profiles = new ObservableCollection<ProfileItemViewModel>();
        Reload();
    }

    public ObservableCollection<ProfileItemViewModel> Profiles { get; }

    [ObservableProperty] private string _newProfileName = string.Empty;
    [ObservableProperty] private bool _isEmpty;

    /// <summary>Reload the list from disk (call when navigating to the page).</summary>
    public void Reload()
    {
        Profiles.Clear();
        foreach (var p in _main.Profiles.LoadAll())
            Profiles.Add(new ProfileItemViewModel(p, this));

        IsEmpty = Profiles.Count == 0;
    }

    [RelayCommand]
    private void SaveCurrent()
    {
        string name = string.IsNullOrWhiteSpace(NewProfileName)
            ? $"Setup {DateTime.Now:MMM d, h:mm tt}"
            : NewProfileName.Trim();

        var profile = _main.Profiles.CaptureCurrent(name);
        _main.Profiles.Save(profile);
        NewProfileName = string.Empty;
        Reload();
        _main.NotifyStatus($"Saved profile \u201C{profile.Name}\u201D.");
    }

    public void Apply(DisplayProfile profile) => _main.ApplyProfileWithRevert(profile);

    public void PersistRename(DisplayProfile profile) => _main.Profiles.Rename(profile, profile.Name);

    public void Delete(ProfileItemViewModel item)
    {
        _main.Profiles.Delete(item.Model);
        Profiles.Remove(item);
        IsEmpty = Profiles.Count == 0;
        _main.NotifyStatus($"Deleted profile \u201C{item.Model.Name}\u201D.");
    }
}

/// <summary>A single saved profile in the list, with inline rename + per-item commands.</summary>
public sealed partial class ProfileItemViewModel : ObservableObject
{
    private readonly ProfilesViewModel _parent;

    public ProfileItemViewModel(DisplayProfile model, ProfilesViewModel parent)
    {
        Model = model;
        _parent = parent;
    }

    public DisplayProfile Model { get; }

    public string Name
    {
        get => Model.Name;
        set
        {
            string trimmed = value?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmed) || trimmed == Model.Name)
                return;

            Model.Name = trimmed;
            _parent.PersistRename(Model);
            OnPropertyChanged();
        }
    }

    public string Summary => Model.Summary;

    /// <summary>Per-monitor scaling captured in this profile, e.g. "Scale · LG ULTRAWIDE 100%, U2723QE 150%".
    /// Empty for older profiles saved before scaling was captured (so the line hides).</summary>
    public string ScalingSummary
    {
        get
        {
            var withScale = Model.Displays.Where(d => d.ScalingPercent > 0).ToList();
            if (withScale.Count == 0)
                return string.Empty;

            var parts = withScale.Select(d =>
            {
                string name = !string.IsNullOrWhiteSpace(d.FriendlyName)
                    ? d.FriendlyName
                    : d.DeviceName.Replace(@"\\.\", string.Empty);
                return $"{name} {d.ScalingPercent}%";
            });

            return "Scale · " + string.Join(", ", parts);
        }
    }

    public string CreatedText =>
        DateTimeOffset.TryParse(Model.Created, out var dt)
            ? $"Saved {dt.LocalDateTime:MMM d, yyyy · h:mm tt}"
            : string.Empty;

    [RelayCommand]
    private void Apply() => _parent.Apply(Model);

    [RelayCommand]
    private void Delete() => _parent.Delete(this);
}
