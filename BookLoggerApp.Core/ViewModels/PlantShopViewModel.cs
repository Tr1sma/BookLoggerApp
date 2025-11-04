using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

/// <summary>
/// ViewModel for the Plant Shop page.
/// </summary>
public partial class PlantShopViewModel : ViewModelBase
{
    private readonly IPlantService _plantService;
    private readonly IAppSettingsProvider _settingsProvider;

    public PlantShopViewModel(
        IPlantService plantService,
        IAppSettingsProvider settingsProvider)
    {
        _plantService = plantService;
        _settingsProvider = settingsProvider;
    }

    [ObservableProperty]
    private ObservableCollection<PlantSpecies> _availableSpecies = new();

    [ObservableProperty]
    private int _userCoins;

    [ObservableProperty]
    private int _userLevel;

    [ObservableProperty]
    private string _newPlantName = "";

    [ObservableProperty]
    private PlantSpecies? _selectedSpecies;

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            // Load user stats
            UserCoins = await _settingsProvider.GetUserCoinsAsync();
            UserLevel = await _settingsProvider.GetUserLevelAsync();

            // Load available species
            var species = await _plantService.GetAvailableSpeciesAsync(UserLevel);
            AvailableSpecies = new ObservableCollection<PlantSpecies>(species);
        }, "Failed to load shop");
    }

    [RelayCommand]
    public async Task PurchasePlantAsync(Guid speciesId)
    {
        if (string.IsNullOrWhiteSpace(NewPlantName))
        {
            SetError("Please enter a name for your plant");
            return;
        }

        await ExecuteSafelyAsync(async () =>
        {
            // Check if user has enough coins
            var species = AvailableSpecies.FirstOrDefault(s => s.Id == speciesId);
            if (species == null)
            {
                SetError("Plant species not found");
                return;
            }

            if (UserCoins < species.BaseCost)
            {
                SetError($"Not enough coins. You need {species.BaseCost} coins but only have {UserCoins}.");
                return;
            }

            // Deduct coins
            await _settingsProvider.SpendCoinsAsync(species.BaseCost);

            // Purchase plant
            await _plantService.PurchasePlantAsync(speciesId, NewPlantName);

            // Reload shop
            NewPlantName = "";
            SelectedSpecies = null;
            await LoadAsync();
        }, "Failed to purchase plant");
    }

    [RelayCommand]
    public void SelectSpecies(PlantSpecies species)
    {
        SelectedSpecies = species;
    }

    [RelayCommand]
    public void ClearSelection()
    {
        SelectedSpecies = null;
        NewPlantName = "";
        ClearError();
    }
}
