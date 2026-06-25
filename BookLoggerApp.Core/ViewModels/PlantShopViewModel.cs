using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

/// <summary>ViewModel for the Plant Shop page.</summary>
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

    private Dictionary<Guid, int> _dynamicPrices = new();

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async ct =>
        {
            UserCoins = await _settingsProvider.GetUserCoinsAsync(ct);
            UserLevel = await _settingsProvider.GetUserLevelAsync(ct);

            // Includes locked species for display.
            var species = await _plantService.GetAllSpeciesAsync(ct);
            AvailableSpecies = new ObservableCollection<PlantSpecies>(species.Where(s => s.IsAvailable).OrderBy(s => s.UnlockLevel).ThenBy(s => s.BaseCost).ThenBy(s => s.Name));

            _dynamicPrices.Clear();
            foreach (var sp in AvailableSpecies)
            {
                var price = await _plantService.GetPlantCostAsync(sp.Id, ct);
                _dynamicPrices[sp.Id] = price;
            }
        }, Tr("Error_FailedTo_LoadShop"));
    }

    /// <summary>Gets the dynamic price for a plant species.</summary>
    public int GetDynamicPrice(Guid speciesId)
    {
        return _dynamicPrices.TryGetValue(speciesId, out var price) ? price : 0;
    }

    [RelayCommand]
    public async Task PurchasePlantAsync(Guid speciesId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            var species = AvailableSpecies.FirstOrDefault(s => s.Id == speciesId);
            if (species == null)
            {
                SetError(Tr("Error_PlantSpeciesNotFound"));
                return;
            }

            if (UserLevel < species.UnlockLevel)
            {
                SetError(Tr("Error_PlantRequiresLevel", species.UnlockLevel, UserLevel));
                return;
            }

            int dynamicPrice = GetDynamicPrice(speciesId);

            if (UserCoins < dynamicPrice)
            {
                SetError(Tr("Error_NotEnoughCoins", dynamicPrice, UserCoins));
                return;
            }

            var plantName = string.IsNullOrWhiteSpace(NewPlantName)
                ? species.Name
                : NewPlantName;

            // PlantService handles coin deduction and counter increment.
            await _plantService.PurchasePlantAsync(speciesId, plantName);

            // Reload so prices recalc after the PlantsPurchased increment.
            NewPlantName = "";
            SelectedSpecies = null;
            await LoadAsync();
        }, Tr("Error_FailedTo_PurchasePlant"));
    }

    [RelayCommand]
    public void SelectSpecies(PlantSpecies species)
    {
        SelectedSpecies = species;
        ClearError();
    }

    [RelayCommand]
    public void ClearSelection()
    {
        SelectedSpecies = null;
        NewPlantName = "";
        ClearError();
    }
}
