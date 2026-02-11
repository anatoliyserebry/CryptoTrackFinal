using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoTrackClient.Models;
using CryptoTrackClient.Services.Interfaces;

namespace CryptoTrackClient.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly ICryptoService _cryptoService;
        private System.Timers.Timer? _autoRefreshTimer;

        [ObservableProperty]
        private ObservableCollection<CryptoCurrency> _cryptocurrencies = new();

        [ObservableProperty]
        private ObservableCollection<CryptoCurrency> _favorites = new();

        [ObservableProperty]
        private CryptoCurrency? _selectedCrypto;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _autoRefreshEnabled = true;

        [ObservableProperty]
        private bool _showChart = false;

        [ObservableProperty]
        private string _activeApi = "Loading...";

        [ObservableProperty]
        private ObservableCollection<string> _availableApis = new();

        [ObservableProperty]
        private bool _isApiSwitching;

        [ObservableProperty]
        private decimal _portfolioValue;

        [ObservableProperty]
        private decimal _portfolioProfitLoss;

        [ObservableProperty]
        private decimal _portfolioProfitLossPercentage;

        [ObservableProperty]
        private bool _showPortfolio = false;

        [ObservableProperty]
        private bool _showConverter = false;

        [ObservableProperty]
        private bool _showMarket = true;

        [ObservableProperty]
        private ObservableCollection<PriceHistory> _priceHistory = new();

        [ObservableProperty]
        private int _selectedChartDays = 7;

        public ObservableCollection<CryptoCurrency> FilteredCurrencies =>
            new ObservableCollection<CryptoCurrency>(Cryptocurrencies
                .Where(c => string.IsNullOrEmpty(SearchText) ||
                           c.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                           c.Symbol.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));

        public ICommand RefreshCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand ShowChartCommand { get; }
        public ICommand ShowFavoritesCommand { get; }
        public ICommand ShowAllCommand { get; }
        public ICommand ToggleAutoRefreshCommand { get; }
        public ICommand ShowDayChartCommand { get; }
        public ICommand ShowWeekChartCommand { get; }
        public ICommand ShowMonthChartCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand SwitchApiCommand { get; }
        public ICommand ShowPortfolioCommand { get; }
        public ICommand ShowConverterCommand { get; }
        public ICommand ShowMarketCommand { get; }

        public MainViewModel(ICryptoService cryptoService)
        {
            _cryptoService = cryptoService;

            RefreshCommand = new AsyncRelayCommand(RefreshDataAsync);
            ToggleFavoriteCommand = new RelayCommand<string>(ToggleFavorite);
            ShowChartCommand = new AsyncRelayCommand<string>(ShowChartAsync);
            ShowFavoritesCommand = new RelayCommand(ShowFavorites);
            ShowAllCommand = new RelayCommand(ShowAll);
            ToggleAutoRefreshCommand = new RelayCommand(ToggleAutoRefresh);
            ShowDayChartCommand = new RelayCommand(() => LoadChartDataAsync(1));
            ShowWeekChartCommand = new RelayCommand(() => LoadChartDataAsync(7));
            ShowMonthChartCommand = new RelayCommand(() => LoadChartDataAsync(30));
            BackCommand = new RelayCommand(Back);
            SwitchApiCommand = new RelayCommand<string>(SwitchApi);
            ShowPortfolioCommand = new RelayCommand(() => ShowPortfolioSection());
            ShowConverterCommand = new RelayCommand(() => ShowConverterSection());
            ShowMarketCommand = new RelayCommand(() => ShowMarketSection());

            SetupAutoRefresh();
            _ = LoadDataAsync();

            // Subscribe to events
            _cryptoService.DataUpdated += async () => await OnDataUpdated();
            _cryptoService.PortfolioUpdated += async () => await UpdatePortfolioInfo();

            _ = UpdateApiInfo();
            _ = UpdatePortfolioInfo();
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var data = await _cryptoService.GetCryptoCurrenciesAsync();
                Cryptocurrencies = new ObservableCollection<CryptoCurrency>(data);

                var favorites = await _cryptoService.GetFavoriteCurrenciesAsync();
                Favorites = new ObservableCollection<CryptoCurrency>(favorites);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshDataAsync()
        {
            await _cryptoService.RefreshDataAsync();
        }

        private async void ToggleFavorite(string cryptoId)
        {
            if (string.IsNullOrEmpty(cryptoId)) return;

            await _cryptoService.ToggleFavoriteAsync(cryptoId);
            await LoadDataAsync();
        }

        private async Task ShowChartAsync(string cryptoId)
        {
            if (string.IsNullOrEmpty(cryptoId)) return;

            SelectedCrypto = Cryptocurrencies.FirstOrDefault(c => c.Id == cryptoId);
            if (SelectedCrypto != null)
            {
                ShowChart = true;
                await LoadChartDataAsync(SelectedChartDays);
            }
        }

        private void ShowFavorites()
        {
            SearchText = string.Empty;
            Cryptocurrencies = new ObservableCollection<CryptoCurrency>(Favorites);
        }

        private void ShowAll()
        {
            SearchText = string.Empty;
            _ = LoadDataAsync();
        }

        private void ToggleAutoRefresh()
        {
            AutoRefreshEnabled = !AutoRefreshEnabled;
            if (_autoRefreshTimer != null)
            {
                _autoRefreshTimer.Enabled = AutoRefreshEnabled;
            }
        }

        private async Task LoadChartDataAsync(int days)
        {
            if (SelectedCrypto == null) return;

            IsLoading = true;
            try
            {
                var history = await _cryptoService.GetPriceHistoryAsync(SelectedCrypto.Id, days);
                PriceHistory = new ObservableCollection<PriceHistory>(history);
                SelectedChartDays = days;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void Back()
        {
            ShowChart = false;
            SelectedCrypto = null;
            PriceHistory.Clear();
        }

        private async void SwitchApi(string apiName)
        {
            if (IsApiSwitching || string.IsNullOrEmpty(apiName)) return;

            IsApiSwitching = true;
            try
            {
                await _cryptoService.SwitchToApiAsync(apiName);
                ActiveApi = apiName;
                await LoadDataAsync();
            }
            finally
            {
                IsApiSwitching = false;
            }
        }

        private void ShowPortfolioSection()
        {
            ShowMarket = false;
            ShowConverter = false;
            ShowPortfolio = true;
        }

        private void ShowConverterSection()
        {
            ShowMarket = false;
            ShowPortfolio = false;
            ShowConverter = true;
        }

        private void ShowMarketSection()
        {
            ShowPortfolio = false;
            ShowConverter = false;
            ShowMarket = true;
        }

        private async Task OnDataUpdated()
        {
            await LoadDataAsync();
        }

        private async Task UpdatePortfolioInfo()
        {
            try
            {
                PortfolioValue = await _cryptoService.GetPortfolioValueAsync();
                var summary = await _cryptoService.GetPortfolioSummaryAsync();
                PortfolioProfitLoss = summary.TotalProfitLoss;
                PortfolioProfitLossPercentage = summary.TotalProfitLossPercentage;
            }
            catch
            {
                // Ignore errors
            }
        }

        private async Task UpdateApiInfo()
        {
            ActiveApi = _cryptoService.ActiveApiName;
            AvailableApis = new ObservableCollection<string>(_cryptoService.AvailableApis);
        }

        private void SetupAutoRefresh()
        {
            _autoRefreshTimer = new System.Timers.Timer(30000); // 30 seconds
            _autoRefreshTimer.Elapsed += async (s, e) => await RefreshDataAsync();
            _autoRefreshTimer.AutoReset = true;
            _autoRefreshTimer.Enabled = AutoRefreshEnabled;
        }

        public void Dispose()
        {
            _autoRefreshTimer?.Dispose();
        }
    }
}
