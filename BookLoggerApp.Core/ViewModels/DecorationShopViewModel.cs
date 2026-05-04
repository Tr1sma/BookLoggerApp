using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

/// <summary>
/// ViewModel for the Decoration Shop tab.
/// </summary>
public partial class DecorationShopViewModel : ViewModelBase
{
    private readonly IDecorationService _decorationService;
    private readonly IAppSettingsProvider _settingsProvider;

    public DecorationShopViewModel(
        IDecorationService decorationService,
        IAppSettingsProvider settingsProvider)
    {
        _decorationService = decorationService;
        _settingsProvider = settingsProvider;
    }

    [ObservableProperty]
    private ObservableCollection<ShopItem> _allDecorations = new();

    [ObservableProperty]
    private int _userCoins;

    [ObservableProperty]
    private int _userLevel;

    [ObservableProperty]
    private ShopItem? _selectedDecoration;

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyWithDbAsync(async () =>
        {
            UserCoins = await _settingsProvider.GetUserCoinsAsync();
            UserLevel = await _settingsProvider.GetUserLevelAsync();

            // Load ALL decorations (including locked) — card handles lock overlay
            var items = await _decorationService.GetAllDecorationShopItemsAsync();
            AllDecorations = new ObservableCollection<ShopItem>(
                items.OrderBy(d => d.UnlockLevel).ThenBy(d => d.Cost).ThenBy(d => d.Name));
        }, Tr("Error_FailedTo_LoadDecorationShop"));
    }

    [RelayCommand]
    public async Task PurchaseDecorationAsync(Guid shopItemId)
    {
        await ExecuteSafelyAsync(async () =>
        {
            var item = AllDecorations.FirstOrDefault(d => d.Id == shopItemId);
            if (item == null)
            {
                SetError(Tr("Error_DecorationNotFound"));
                return;
            }

            if (UserLevel < item.UnlockLevel)
            {
                SetError(Tr("Error_DecorationRequiresLevel", item.UnlockLevel));
                return;
            }

            if (UserCoins < item.Cost)
            {
                SetError(Tr("Error_NotEnoughCoinsShort", item.Cost, UserCoins));
                return;
            }

            await _decorationService.PurchaseDecorationAsync(shopItemId);
            SelectedDecoration = null;
            await LoadAsync();
        }, Tr("Error_PurchaseDecorationFailed"));
    }

    [RelayCommand]
    public void SelectDecoration(ShopItem item)
    {
        SelectedDecoration = item;
        ClearError();
    }

    [RelayCommand]
    public void ClearSelection()
    {
        SelectedDecoration = null;
        ClearError();
    }
}
