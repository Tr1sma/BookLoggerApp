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

    // Dictionary to store dynamic prices for each species
    private Dictionary<Guid, int> _dynamicPrices = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async () =>
        {
            // Load user stats
            UserCoins = await _settingsProvider.GetUserCoinsAsync();
            UserLevel = await _settingsProvider.GetUserLevelAsync();

            // Load ALL species (including locked ones for display)
            var species = await _plantService.GetAllSpeciesAsync();
            AvailableSpecies = new ObservableCollection<PlantSpecies>(species.Where(s => s.IsAvailable).OrderBy(s => s.UnlockLevel).ThenBy(s => s.BaseCost));

            // Load dynamic prices for each species
            _dynamicPrices.Clear();
            foreach (var sp in AvailableSpecies)
            {
                var price = await _plantService.GetPlantCostAsync(sp.Id);
                _dynamicPrices[sp.Id] = price;
            }
        }, "Failed to load shop");
    }

    /// <summary>
    /// Get the dynamic price for a plant species.
    /// </summary>
    public int GetDynamicPrice(Guid speciesId)
    {
        return _dynamicPrices.TryGetValue(speciesId, out var price) ? price : 0;
    }

    [RelayCommand]
    public async Task PurchasePlantAsync(Guid speciesId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            // Check if user has enough coins
            var species = AvailableSpecies.FirstOrDefault(s => s.Id == speciesId);
            if (species == null)
            {
                SetError("Plant species not found");
                return;
            }

            // Check if plant is unlocked
            if (UserLevel < species.UnlockLevel)
            {
                SetError($"This plant requires level {species.UnlockLevel}. You are currently level {UserLevel}.");
                return;
            }

            // Get dynamic price
            int dynamicPrice = GetDynamicPrice(speciesId);

            if (UserCoins < dynamicPrice)
            {
                SetError($"Not enough coins. You need {dynamicPrice} coins but only have {UserCoins}.");
                return;
            }

            // Use species name as default if no custom name provided
            var plantName = string.IsNullOrWhiteSpace(NewPlantName)
                ? species.Name
                : NewPlantName;

            // Purchase plant (PlantService handles coin deduction and counter increment)
            await _plantService.PurchasePlantAsync(speciesId, plantName);

            // Reload shop (prices will be recalculated after PlantsPurchased increment)
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
