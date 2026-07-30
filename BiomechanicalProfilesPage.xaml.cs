using System.Collections.ObjectModel;
using System.Globalization;
using RouteLab.Models;

namespace RouteLab;

public partial class BiomechanicalProfilesPage : ContentPage
{
    private readonly ObservableCollection<ClimberProfileDefinition> profiles = new();
    private readonly App app;
    private ClimberProfileDefinition? editingProfile;
    private bool isRefreshing;

    public BiomechanicalProfilesPage()
    {
        InitializeComponent();
        app = (App)Application.Current!;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadProfiles();
    }

    private async void OnProfileChanged(object? sender, EventArgs e)
    {
        if (isRefreshing)
        {
            return;
        }

        var selectedProfile = ProfilePicker.SelectedItem as ClimberProfileDefinition;
        try
        {
            if (editingProfile is not null)
            {
                UpdateEditingProfile();
            }
        }
        catch (InvalidOperationException ex)
        {
            isRefreshing = true;
            ProfilePicker.SelectedItem = editingProfile;
            isRefreshing = false;
            await DisplayAlertAsync("Profilo atleta", ex.Message, "OK");
            return;
        }

        LoadProfileIntoEditor(selectedProfile);
    }

    private async void OnAddProfileClicked(object? sender, EventArgs e)
    {
        try
        {
            UpdateEditingProfile();
            var source = editingProfile ?? profiles.First(profile => profile.IsDefault);
            var profile = source.Clone();
            profile.Id = Guid.NewGuid().ToString("N");
            profile.Name = GetNextProfileName();
            profiles.Add(profile);
            ProfilePicker.SelectedItem = profile;
            ProfileNameEntry.Focus();
            StatusLabel.Text = "Nuovo profilo pronto. Inserisci i dati e premi Salva.";
        }
        catch (InvalidOperationException ex)
        {
            await DisplayAlertAsync("Profilo atleta", ex.Message, "OK");
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        using var busy = AppBusy.Show("Salvataggio profili...");
        try
        {
            UpdateEditingProfile();
            var selectedProfileId = editingProfile?.Id;
            var settings = app.AppSettingsService.Load();
            settings.ClimberProfiles = profiles.Select(profile => profile.Clone()).ToList();
            app.AppSettingsService.Save(settings);
            LoadProfiles(selectedProfileId);
            StatusLabel.Text = "Profili biomeccanici salvati.";
            await DisplayAlertAsync("Profili biomeccanici", "Profili salvati correttamente.", "OK");
        }
        catch (InvalidOperationException ex)
        {
            StatusLabel.Text = ex.Message;
            await DisplayAlertAsync("Profili biomeccanici", ex.Message, "OK");
        }
    }

    private void LoadProfiles(string? selectedProfileId = null)
    {
        var settings = app.AppSettingsService.Load();
        isRefreshing = true;
        profiles.Clear();
        foreach (var profile in settings.ClimberProfiles)
        {
            profiles.Add(profile.Clone());
        }

        if (profiles.All(profile => !profile.IsDefault))
        {
            profiles.Insert(0, new ClimberProfileDefinition());
        }

        ProfilePicker.ItemsSource = profiles;
        var selected = profiles.FirstOrDefault(profile =>
                           string.Equals(profile.Id, selectedProfileId, StringComparison.OrdinalIgnoreCase))
                       ?? profiles.First(profile => profile.IsDefault);
        ProfilePicker.SelectedItem = selected;
        isRefreshing = false;
        LoadProfileIntoEditor(selected);
    }

    private void LoadProfileIntoEditor(ClimberProfileDefinition? profile)
    {
        isRefreshing = true;
        ProfileNameEntry.Text = profile?.Name ?? string.Empty;
        ProfileNameEntry.IsReadOnly = profile?.IsDefault == true;
        HeightEntry.Text = profile?.HeightMm.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty;
        ArmSpanEntry.Text = profile?.ArmSpanMm.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty;
        MassEntry.Text = profile?.MassKg.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty;
        WallDistanceEntry.Text = profile?.BodyDistanceFromWallMm.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty;
        ProfileHintLabel.Text = profile?.IsDefault == true
            ? "Il profilo predefinito rimane sempre disponibile; puoi modificarne le misure."
            : "Questo profilo puo' essere associato a uno o piu' circuiti.";
        editingProfile = profile;
        isRefreshing = false;
    }

    private void UpdateEditingProfile()
    {
        if (editingProfile is null)
        {
            throw new InvalidOperationException("Seleziona un profilo biomeccanico.");
        }

        var name = editingProfile.IsDefault
            ? "Persona predefinita"
            : ReadRequiredText(ProfileNameEntry.Text, "Inserisci il nome della persona.");
        if (profiles.Any(other =>
                !ReferenceEquals(other, editingProfile) &&
                string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Esiste gia' un profilo chiamato {name}.");
        }

        editingProfile.Name = name;
        editingProfile.HeightMm = ParseRangeDouble(HeightEntry.Text, 1200d, 2300d, "L'altezza atleta deve essere tra 1200 e 2300 mm.");
        editingProfile.ArmSpanMm = ParseRangeDouble(ArmSpanEntry.Text, 1200d, 2500d, "L'apertura braccia deve essere tra 1200 e 2500 mm.");
        editingProfile.MassKg = ParseRangeDouble(MassEntry.Text, 30d, 180d, "La massa atleta deve essere tra 30 e 180 kg.");
        editingProfile.BodyDistanceFromWallMm = ParseRangeDouble(WallDistanceEntry.Text, 50d, 600d, "La distanza corpo-parete deve essere tra 50 e 600 mm.");
    }

    private string GetNextProfileName()
    {
        var index = profiles.Count(profile => !profile.IsDefault) + 1;
        string name;
        do
        {
            name = $"Atleta {index++}";
        }
        while (profiles.Any(profile =>
                   string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)));

        return name;
    }

    private static string ReadRequiredText(string? text, string errorMessage)
    {
        var value = text?.Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException(errorMessage);
    }

    private static double ParseRangeDouble(string? text, double min, double max, string errorMessage)
    {
        var normalized = text?.Trim().Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
            value >= min &&
            value <= max)
        {
            return value;
        }

        throw new InvalidOperationException(errorMessage);
    }
}
