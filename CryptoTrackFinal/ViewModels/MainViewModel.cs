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
        private readonly List<CryptoCurrency> _allCurrencies = new();
        private bool _showOnlyFavorites;

        [ObservableProperty]
        private ObservableCollection<CryptoCurrency> _cryptocurrencies = new();

        [ObservableProperty]
        private ObservableCollection<CryptoCurrency> _favorites = new();

        [ObservableProperty]
        private CryptoCurrency? _selectedCrypto;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "Ready";

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

        [ObservableProperty]
        private ObservableCollection<FiatCurrency> _fiatCurrencies = new();

        [ObservableProperty]
        private FiatCurrency? _selectedFromCurrency;

        [ObservableProperty]
        private FiatCurrency? _selectedToCurrency;

        [ObservableProperty]
        private decimal _amountToConvert = 100m;

        [ObservableProperty]
        private decimal _convertedAmount;

        [ObservableProperty]
        private decimal _exchangeRate = 1m;

        [ObservableProperty]
        private ObservableCollection<PortfolioAsset> _portfolioAssets = new();

        [ObservableProperty]
        private ObservableCollection<Transaction> _portfolioTransactions = new();

        [ObservableProperty]
        private CryptoCurrency? _selectedPortfolioCurrency;

        [ObservableProperty]
        private decimal _portfolioTransactionAmount;

        [ObservableProperty]
        private decimal _portfolioTransactionPrice;

        [ObservableProperty]
        private decimal _portfolioTransactionFee;

        [ObservableProperty]
        private string _portfolioTransactionExchange = "Manual";

        public ObservableCollection<CryptoCurrency> FilteredCurrencies =>
            new ObservableCollection<CryptoCurrency>((_showOnlyFavorites ? Favorites : Cryptocurrencies)
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
        public ICommand ConvertCurrencyCommand { get; }
        public ICommand SwapCurrencyCommand { get; }
        public ICommand AddPortfolioTransactionCommand { get; }

        public MainViewModel(ICryptoService cryptoService)
        {
            _cryptoService = cryptoService;

            RefreshCommand = new AsyncRelayCommand(RefreshDataAsync);
            ToggleFavoriteCommand = new AsyncRelayCommand<string>(ToggleFavoriteAsync);
            ShowChartCommand = new AsyncRelayCommand<string>(ShowChartAsync);
            ShowFavoritesCommand = new RelayCommand(ShowFavorites);
            ShowAllCommand = new RelayCommand(ShowAll);
            ToggleAutoRefreshCommand = new RelayCommand(ToggleAutoRefresh);
            ShowDayChartCommand = new AsyncRelayCommand(() => LoadChartDataAsync(1));
            ShowWeekChartCommand = new AsyncRelayCommand(() => LoadChartDataAsync(7));
            ShowMonthChartCommand = new AsyncRelayCommand(() => LoadChartDataAsync(30));
            BackCommand = new RelayCommand(Back);
            SwitchApiCommand = new AsyncRelayCommand<string>(SwitchApiAsync);
            ShowPortfolioCommand = new RelayCommand(() => ShowPortfolioSection());
            ShowConverterCommand = new RelayCommand(() => ShowConverterSection());
            ShowMarketCommand = new RelayCommand(() => ShowMarketSection());
            ConvertCurrencyCommand = new AsyncRelayCommand(ConvertCurrencyAsync);
            SwapCurrencyCommand = new AsyncRelayCommand(SwapCurrenciesAsync);
            AddPortfolioTransactionCommand = new AsyncRelayCommand(AddPortfolioTransactionAsync);

            SetupAutoRefresh();
            _ = LoadDataAsync();
            _ = LoadFiatCurrenciesAsync();

            // Subscribe to events
            _cryptoService.DataUpdated += async () => await OnDataUpdated();
            _cryptoService.PortfolioUpdated += async () => await UpdatePortfolioInfo();

            _ = UpdateApiInfo();
            _ = UpdatePortfolioInfo();
            _ = LoadPortfolioDetailsAsync();
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var data = await _cryptoService.GetCryptoCurrenciesAsync();
                _allCurrencies.Clear();
                _allCurrencies.AddRange(data);
                Cryptocurrencies = new ObservableCollection<CryptoCurrency>(data);

                var favorites = await _cryptoService.GetFavoriteCurrenciesAsync();
                Favorites = new ObservableCollection<CryptoCurrency>(favorites);
                SelectedPortfolioCurrency ??= Cryptocurrencies.FirstOrDefault();
                StatusMessage = $"Loaded {Cryptocurrencies.Count} assets ({DateTime.Now:T})";
                OnPropertyChanged(nameof(FilteredCurrencies));
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load market data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshDataAsync()
        {
            await _cryptoService.RefreshDataAsync();
            StatusMessage = $"Data refreshed at {DateTime.Now:T}";
        }

        private async Task ToggleFavoriteAsync(string cryptoId)
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
            _showOnlyFavorites = true;
            OnPropertyChanged(nameof(FilteredCurrencies));
        }

        private void ShowAll()
        {
            SearchText = string.Empty;
            _showOnlyFavorites = false;
            Cryptocurrencies = new ObservableCollection<CryptoCurrency>(_allCurrencies);
            OnPropertyChanged(nameof(FilteredCurrencies));
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

        private async Task SwitchApiAsync(string apiName)
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
            _ = LoadPortfolioDetailsAsync();
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
            await UpdateApiInfo();

            if (FiatCurrencies.Count == 0)
            {
                await LoadFiatCurrenciesAsync();
            }
        }

        private async Task UpdatePortfolioInfo()
        {
            try
            {
                PortfolioValue = await _cryptoService.GetPortfolioValueAsync();
                var summary = await _cryptoService.GetPortfolioSummaryAsync();
                PortfolioProfitLoss = summary.TotalProfitLoss;
                PortfolioProfitLossPercentage = summary.TotalProfitLossPercentage;
                await LoadPortfolioDetailsAsync();
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

        private async Task LoadPortfolioDetailsAsync()
        {
            try
            {
                var assets = await _cryptoService.GetPortfolioAssetsAsync();
                PortfolioAssets = new ObservableCollection<PortfolioAsset>(assets);

                var transactions = await _cryptoService.GetTransactionsAsync();
                PortfolioTransactions = new ObservableCollection<Transaction>(transactions.Take(20));
            }
            catch
            {
                // Ignore errors, summary header still provides status
            }
        }

        private async Task AddPortfolioTransactionAsync()
        {
            if (SelectedPortfolioCurrency == null || PortfolioTransactionAmount <= 0)
            {
                StatusMessage = "Select a coin and enter an amount greater than 0.";
                return;
            }

            var unitPrice = PortfolioTransactionPrice > 0 ? PortfolioTransactionPrice : SelectedPortfolioCurrency.CurrentPrice;
            if (unitPrice <= 0)
            {
                StatusMessage = "Enter a valid purchase price.";
                return;
            }

            var transaction = new Transaction
            {
                CryptoId = SelectedPortfolioCurrency.Id,
                CryptoName = SelectedPortfolioCurrency.Name,
                CryptoSymbol = SelectedPortfolioCurrency.Symbol,
                Type = TransactionType.Buy,
                Amount = PortfolioTransactionAmount,
                PricePerUnit = unitPrice,
                Fee = PortfolioTransactionFee,
                Exchange = string.IsNullOrWhiteSpace(PortfolioTransactionExchange) ? "Manual" : PortfolioTransactionExchange,
                TransactionDate = DateTime.Now,
                CurrentPrice = SelectedPortfolioCurrency.CurrentPrice
            };

            await _cryptoService.AddTransactionAsync(transaction);

            PortfolioTransactionAmount = 0;
            PortfolioTransactionFee = 0;
            PortfolioTransactionPrice = SelectedPortfolioCurrency.CurrentPrice;

            await UpdatePortfolioInfo();
            StatusMessage = $"Added {transaction.Amount} {transaction.CryptoSymbol} to portfolio.";
        }

        partial void OnSearchTextChanged(string value) => OnPropertyChanged(nameof(FilteredCurrencies));
        partial void OnCryptocurrenciesChanged(ObservableCollection<CryptoCurrency> value) => OnPropertyChanged(nameof(FilteredCurrencies));
        partial void OnFavoritesChanged(ObservableCollection<CryptoCurrency> value) => OnPropertyChanged(nameof(FilteredCurrencies));
        partial void OnActiveApiChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value) && value != _cryptoService.ActiveApiName)
            {
                _ = SwitchApiAsync(value);
            }
        }

        private async Task LoadFiatCurrenciesAsync()
        {
            try
            {
                var currencies = await _cryptoService.GetFiatCurrenciesAsync();
                FiatCurrencies = new ObservableCollection<FiatCurrency>(currencies.OrderBy(c => c.Code));

                SelectedFromCurrency = FiatCurrencies.FirstOrDefault(f => f.Code == "USD") ?? FiatCurrencies.FirstOrDefault();
                SelectedToCurrency = FiatCurrencies.FirstOrDefault(f => f.Code == "EUR") ?? FiatCurrencies.Skip(1).FirstOrDefault();

                await ConvertCurrencyAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load fiat currencies: {ex.Message}";
            }
        }

        private async Task ConvertCurrencyAsync()
        {
            if (SelectedFromCurrency == null || SelectedToCurrency == null || AmountToConvert <= 0)
            {
                ConvertedAmount = 0;
                return;
            }

            ExchangeRate = await _cryptoService.ConvertCurrencyAsync(1m, SelectedFromCurrency.Code, SelectedToCurrency.Code);
            ConvertedAmount = await _cryptoService.ConvertCurrencyAsync(AmountToConvert, SelectedFromCurrency.Code, SelectedToCurrency.Code);
        }

        private async Task SwapCurrenciesAsync()
        {
            if (SelectedFromCurrency == null || SelectedToCurrency == null) return;

            var temp = SelectedFromCurrency;
            SelectedFromCurrency = SelectedToCurrency;
            SelectedToCurrency = temp;
            await ConvertCurrencyAsync();
        }

        partial void OnSelectedFromCurrencyChanged(FiatCurrency? value) => _ = ConvertCurrencyAsync();
        partial void OnSelectedToCurrencyChanged(FiatCurrency? value) => _ = ConvertCurrencyAsync();
        partial void OnAmountToConvertChanged(decimal value) => _ = ConvertCurrencyAsync();
        partial void OnSelectedPortfolioCurrencyChanged(CryptoCurrency? value)
        {
            if (value != null)
            {
                PortfolioTransactionPrice = value.CurrentPrice;
            }
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
