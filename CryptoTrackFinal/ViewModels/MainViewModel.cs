using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoTrackClient.Models;
using CryptoTrackClient.Services.Interfaces;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace CryptoTrackClient.ViewModels
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly ICryptoService _cryptoService;
        private readonly Dispatcher _dispatcher;
        private readonly ObservableCollection<CryptoCurrency> _filteredCurrenciesInternal = new();
        private CancellationTokenSource? _conversionCts;
        private readonly List<CryptoCurrency> _allCurrencies = new();

        public ReadOnlyObservableCollection<CryptoCurrency> FilteredCurrencies { get; }

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
        private bool _autoRefreshEnabled;

        [ObservableProperty]
        private bool _showChart;

        [ObservableProperty]
        private string _activeApi = "Loading...";

        [ObservableProperty]
        private string _selectedApi = string.Empty;

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
        private bool _showPortfolio;

        [ObservableProperty]
        private bool _showConverter;

        [ObservableProperty]
        private bool _showMarket = true;

        [ObservableProperty]
        private bool _showOnlyFavorites;

        [ObservableProperty]
        private ObservableCollection<PriceHistory> _priceHistory = new();

        [ObservableProperty]
        private int _selectedChartDays = 7;

        [ObservableProperty]
        private bool _isChartExpanded;

        [ObservableProperty]
        private PlotModel _priceChartModel = CreateEmptyChartModel();

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

        public int VisibleCurrencyCount => _filteredCurrenciesInternal.Count;
        public int FavoriteCount => Favorites.Count;
        public bool HasFilteredCurrencies => _filteredCurrenciesInternal.Count > 0;
        public bool HasActiveSearch => !string.IsNullOrWhiteSpace(SearchText);
        public string SelectedCryptoDisplayName => SelectedCrypto?.Name ?? "Selected asset";
        public decimal SelectedCryptoDisplayPrice => SelectedCrypto?.CurrentPrice ?? 0m;
        public decimal SelectedCryptoDisplayChange => SelectedCrypto?.PriceChangePercentage24h ?? 0m;
        public string SelectedFromCurrencyCode => SelectedFromCurrency?.Code ?? "---";
        public string SelectedFromCurrencyName => SelectedFromCurrency?.Name ?? "Select a source currency";
        public string SelectedToCurrencyCode => SelectedToCurrency?.Code ?? "---";
        public string SelectedToCurrencyName => SelectedToCurrency?.Name ?? "Select a target currency";

        public string MarketSummary
        {
            get
            {
                var total = ShowOnlyFavorites ? Favorites.Count : Cryptocurrencies.Count;

                if (HasActiveSearch)
                {
                    var trimmed = SearchText.Trim();
                    return ShowOnlyFavorites
                        ? $"{VisibleCurrencyCount} of {total} favorites match \"{trimmed}\""
                        : $"{VisibleCurrencyCount} of {total} assets match \"{trimmed}\"";
                }

                return ShowOnlyFavorites
                    ? $"{VisibleCurrencyCount} favorite assets tracked"
                    : $"{VisibleCurrencyCount} assets tracked in the current market feed";
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand ShowChartCommand { get; }
        public ICommand ShowFavoritesCommand { get; }
        public ICommand ShowAllCommand { get; }
        public ICommand ShowDayChartCommand { get; }
        public ICommand ShowWeekChartCommand { get; }
        public ICommand ShowMonthChartCommand { get; }
        public ICommand ToggleChartExpansionCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand SwitchApiCommand { get; }
        public ICommand ShowPortfolioCommand { get; }
        public ICommand ShowConverterCommand { get; }
        public ICommand ShowMarketCommand { get; }
        public ICommand ConvertCurrencyCommand { get; }
        public ICommand SwapCurrencyCommand { get; }
        public ICommand AddPortfolioTransactionCommand { get; }
        public ICommand ClearSearchCommand { get; }

        public MainViewModel(ICryptoService cryptoService)
        {
            _cryptoService = cryptoService;
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            FilteredCurrencies = new ReadOnlyObservableCollection<CryptoCurrency>(_filteredCurrenciesInternal);
            AutoRefreshEnabled = _cryptoService.AutoRefreshEnabled;

            RefreshCommand = new AsyncRelayCommand(RefreshDataAsync);
            ToggleFavoriteCommand = new AsyncRelayCommand<string>(ToggleFavoriteAsync);
            ShowChartCommand = new AsyncRelayCommand<string>(ShowChartAsync);
            ShowFavoritesCommand = new RelayCommand(ShowFavorites);
            ShowAllCommand = new RelayCommand(ShowAll);
            ShowDayChartCommand = new AsyncRelayCommand(() => LoadChartDataAsync(1));
            ShowWeekChartCommand = new AsyncRelayCommand(() => LoadChartDataAsync(7));
            ShowMonthChartCommand = new AsyncRelayCommand(() => LoadChartDataAsync(30));
            ToggleChartExpansionCommand = new RelayCommand(ToggleChartExpansion);
            BackCommand = new RelayCommand(Back);
            SwitchApiCommand = new AsyncRelayCommand(SwitchApiAsync);
            ShowPortfolioCommand = new RelayCommand(ShowPortfolioSection);
            ShowConverterCommand = new RelayCommand(ShowConverterSection);
            ShowMarketCommand = new RelayCommand(ShowMarketSection);
            ConvertCurrencyCommand = new AsyncRelayCommand(ConvertCurrencyAsync);
            SwapCurrencyCommand = new AsyncRelayCommand(SwapCurrenciesAsync);
            AddPortfolioTransactionCommand = new AsyncRelayCommand(AddPortfolioTransactionAsync);
            ClearSearchCommand = new RelayCommand(ClearSearch);

            _cryptoService.DataUpdated += HandleDataUpdated;
            _cryptoService.PortfolioUpdated += HandlePortfolioUpdated;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            UpdateApiInfo();
            await LoadDataAsync();
            await LoadFiatCurrenciesAsync();
            await UpdatePortfolioInfo();
            await LoadPortfolioDetailsAsync();
        }

        private async void HandleDataUpdated()
        {
            try
            {
                await RunOnUiThreadAsync(async () =>
                {
                    UpdateApiInfo();
                    await LoadDataAsync();

                    if (FiatCurrencies.Count == 0)
                    {
                        await LoadFiatCurrenciesAsync();
                    }
                });
            }
            catch (Exception ex)
            {
                _ = RunOnUiThreadAsync(() =>
                {
                    StatusMessage = $"Background refresh failed: {ex.Message}";
                    return Task.CompletedTask;
                });
            }
        }

        private async void HandlePortfolioUpdated()
        {
            try
            {
                await RunOnUiThreadAsync(UpdatePortfolioInfo);
            }
            catch (Exception ex)
            {
                _ = RunOnUiThreadAsync(() =>
                {
                    StatusMessage = $"Portfolio refresh failed: {ex.Message}";
                    return Task.CompletedTask;
                });
            }
        }

        private async Task RunOnUiThreadAsync(Func<Task> action)
        {
            if (_dispatcher.CheckAccess())
            {
                await action();
                return;
            }

            await _dispatcher.InvokeAsync(action).Task.Unwrap();
        }

        private async Task LoadDataAsync()
        {
            IsLoading = true;

            try
            {
                var data = await _cryptoService.GetCryptoCurrenciesAsync();
                var favorites = await _cryptoService.GetFavoriteCurrenciesAsync();

                _allCurrencies.Clear();
                _allCurrencies.AddRange(data);

                Cryptocurrencies = new ObservableCollection<CryptoCurrency>(data);
                Favorites = new ObservableCollection<CryptoCurrency>(favorites);

                if (SelectedCrypto != null)
                {
                    SelectedCrypto = FindMatchingCurrency(Cryptocurrencies, SelectedCrypto);
                }

                SelectedPortfolioCurrency = SelectedPortfolioCurrency != null
                    ? FindMatchingCurrency(Cryptocurrencies, SelectedPortfolioCurrency) ?? Cryptocurrencies.FirstOrDefault()
                    : Cryptocurrencies.FirstOrDefault();

                RefreshFilteredCurrencies();

                StatusMessage = data.Count > 0
                    ? $"Loaded {data.Count} assets at {DateTime.Now:T}"
                    : "No market data available from the current provider.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load market data: {ex.Message}";
                Cryptocurrencies = new ObservableCollection<CryptoCurrency>();
                Favorites = new ObservableCollection<CryptoCurrency>();
                RefreshFilteredCurrencies();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void RefreshFilteredCurrencies()
        {
            var source = ShowOnlyFavorites ? Favorites : Cryptocurrencies;
            var filtered = source
                .Where(currency => string.IsNullOrWhiteSpace(SearchText)
                    || currency.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || currency.Symbol.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .OrderBy(currency => currency.Rank ?? int.MaxValue)
                .ThenBy(currency => currency.Name)
                .ToList();

            _filteredCurrenciesInternal.Clear();
            foreach (var currency in filtered)
            {
                _filteredCurrenciesInternal.Add(currency);
            }

            OnPropertyChanged(nameof(VisibleCurrencyCount));
            OnPropertyChanged(nameof(FavoriteCount));
            OnPropertyChanged(nameof(HasFilteredCurrencies));
            OnPropertyChanged(nameof(HasActiveSearch));
            OnPropertyChanged(nameof(MarketSummary));
        }

        private static CryptoCurrency? FindMatchingCurrency(IEnumerable<CryptoCurrency> currencies, CryptoCurrency reference)
        {
            return currencies.FirstOrDefault(currency =>
                       string.Equals(currency.Id, reference.Id, StringComparison.OrdinalIgnoreCase))
                   ?? currencies.FirstOrDefault(currency =>
                       !string.IsNullOrWhiteSpace(currency.Symbol)
                       && string.Equals(currency.Symbol, reference.Symbol, StringComparison.OrdinalIgnoreCase))
                   ?? currencies.FirstOrDefault(currency =>
                       NormalizeAssetKey(currency.Name) == NormalizeAssetKey(reference.Name));
        }

        private static string NormalizeAssetKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var parts = value
                .Trim()
                .ToLowerInvariant()
                .Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

            return string.Join("-", parts);
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                IsLoading = true;
                await _cryptoService.RefreshDataAsync();
                StatusMessage = $"Data refreshed at {DateTime.Now:T}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Refresh failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ToggleFavoriteAsync(string? cryptoId)
        {
            if (string.IsNullOrWhiteSpace(cryptoId))
            {
                return;
            }

            await _cryptoService.ToggleFavoriteAsync(cryptoId);
            await LoadDataAsync();
        }

        private async Task ShowChartAsync(string? cryptoId)
        {
            if (string.IsNullOrWhiteSpace(cryptoId))
            {
                return;
            }

            SelectedCrypto = Cryptocurrencies.FirstOrDefault(currency => currency.Id == cryptoId);
            if (SelectedCrypto == null)
            {
                return;
            }

            ShowChart = true;
            IsChartExpanded = false;
            await LoadChartDataAsync(SelectedChartDays);
        }

        private void ShowFavorites()
        {
            ShowOnlyFavorites = true;
        }

        private void ShowAll()
        {
            ShowOnlyFavorites = false;
        }

        private void ClearSearch() => SearchText = string.Empty;

        private async Task LoadChartDataAsync(int days)
        {
            if (SelectedCrypto == null)
            {
                return;
            }

            IsLoading = true;

            try
            {
                var history = (await _cryptoService.GetPriceHistoryAsync(SelectedCrypto.Id, days))
                    .OrderBy(point => point.Date)
                    .ToList();

                PriceHistory = new ObservableCollection<PriceHistory>(history);
                SelectedChartDays = days;
                PriceChartModel = CreatePriceChartModel(history, SelectedCrypto.Name, days);
            }
            catch (Exception ex)
            {
                PriceHistory = new ObservableCollection<PriceHistory>();
                PriceChartModel = CreateEmptyChartModel();
                StatusMessage = $"Failed to load chart data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void Back()
        {
            ShowChart = false;
            IsChartExpanded = false;
            SelectedCrypto = null;
            PriceHistory.Clear();
            PriceChartModel = CreateEmptyChartModel();
        }

        private void ToggleChartExpansion()
        {
            IsChartExpanded = !IsChartExpanded;
        }

        private async Task SwitchApiAsync()
        {
            if (IsApiSwitching || string.IsNullOrWhiteSpace(SelectedApi))
            {
                return;
            }

            if (string.Equals(SelectedApi, _cryptoService.ActiveApiName, StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = $"{SelectedApi} is already active.";
                return;
            }

            IsApiSwitching = true;
            StatusMessage = $"Switching market feed to {SelectedApi}...";

            try
            {
                await _cryptoService.SwitchToApiAsync(SelectedApi);
                UpdateApiInfo();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                SelectedApi = ActiveApi;
                StatusMessage = $"Failed to switch API: {ex.Message}";
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
                // Keep the last known portfolio summary when background refreshes fail.
            }
        }

        private void UpdateApiInfo()
        {
            ActiveApi = _cryptoService.ActiveApiName;
            AvailableApis = new ObservableCollection<string>(_cryptoService.AvailableApis);

            if (AvailableApis.Count == 0)
            {
                SelectedApi = string.Empty;
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedApi) || !AvailableApis.Contains(SelectedApi))
            {
                SelectedApi = ActiveApi;
            }
        }

        private async Task LoadPortfolioDetailsAsync()
        {
            try
            {
                var assets = await _cryptoService.GetPortfolioAssetsAsync();
                PortfolioAssets = new ObservableCollection<PortfolioAsset>(assets.OrderByDescending(asset => asset.CurrentValue));

                var transactions = await _cryptoService.GetTransactionsAsync();
                PortfolioTransactions = new ObservableCollection<Transaction>(transactions.Take(12));
            }
            catch
            {
                // Ignore errors, summary cards still provide useful context.
            }
        }

        private async Task AddPortfolioTransactionAsync()
        {
            if (SelectedPortfolioCurrency == null || PortfolioTransactionAmount <= 0)
            {
                StatusMessage = "Select an asset and enter an amount greater than 0.";
                return;
            }

            var unitPrice = PortfolioTransactionPrice > 0
                ? PortfolioTransactionPrice
                : SelectedPortfolioCurrency.CurrentPrice;

            if (unitPrice <= 0)
            {
                StatusMessage = "Enter a valid buy price.";
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
            StatusMessage = $"Added {transaction.Amount} {transaction.CryptoSymbol} to the portfolio.";
        }

        private async Task LoadFiatCurrenciesAsync()
        {
            try
            {
                var currencies = await _cryptoService.GetFiatCurrenciesAsync();
                FiatCurrencies = new ObservableCollection<FiatCurrency>(currencies.OrderBy(currency => currency.Code));

                SelectedFromCurrency = FiatCurrencies.FirstOrDefault(currency => currency.Code == "USD")
                    ?? FiatCurrencies.FirstOrDefault();
                SelectedToCurrency = FiatCurrencies.FirstOrDefault(currency => currency.Code == "EUR")
                    ?? FiatCurrencies.Skip(1).FirstOrDefault()
                    ?? FiatCurrencies.FirstOrDefault();

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
                ExchangeRate = 0;
                return;
            }

            try
            {
                var rate = await _cryptoService.ConvertCurrencyAsync(1m, SelectedFromCurrency.Code, SelectedToCurrency.Code);
                ExchangeRate = rate;
                ConvertedAmount = AmountToConvert * rate;
            }
            catch (Exception ex)
            {
                ExchangeRate = 0;
                ConvertedAmount = 0;
                StatusMessage = $"Currency conversion failed: {ex.Message}";
            }
        }

        private async Task SwapCurrenciesAsync()
        {
            if (SelectedFromCurrency == null || SelectedToCurrency == null)
            {
                return;
            }

            (SelectedFromCurrency, SelectedToCurrency) = (SelectedToCurrency, SelectedFromCurrency);
            await ConvertCurrencyAsync();
        }

        partial void OnSearchTextChanged(string value) => RefreshFilteredCurrencies();
        partial void OnCryptocurrenciesChanged(ObservableCollection<CryptoCurrency> value) => RefreshFilteredCurrencies();
        partial void OnFavoritesChanged(ObservableCollection<CryptoCurrency> value) => RefreshFilteredCurrencies();
        partial void OnShowOnlyFavoritesChanged(bool value) => RefreshFilteredCurrencies();
        partial void OnSelectedCryptoChanged(CryptoCurrency? value)
        {
            OnPropertyChanged(nameof(SelectedCryptoDisplayName));
            OnPropertyChanged(nameof(SelectedCryptoDisplayPrice));
            OnPropertyChanged(nameof(SelectedCryptoDisplayChange));
        }

        partial void OnAutoRefreshEnabledChanged(bool value)
        {
            _cryptoService.AutoRefreshEnabled = value;
            StatusMessage = value ? "Auto-refresh enabled." : "Auto-refresh paused.";
        }

        partial void OnSelectedFromCurrencyChanged(FiatCurrency? value)
        {
            OnPropertyChanged(nameof(SelectedFromCurrencyCode));
            OnPropertyChanged(nameof(SelectedFromCurrencyName));
            QueueCurrencyConversion();
        }

        partial void OnSelectedToCurrencyChanged(FiatCurrency? value)
        {
            OnPropertyChanged(nameof(SelectedToCurrencyCode));
            OnPropertyChanged(nameof(SelectedToCurrencyName));
            QueueCurrencyConversion();
        }

        partial void OnAmountToConvertChanged(decimal value) => QueueCurrencyConversion();

        partial void OnSelectedPortfolioCurrencyChanged(CryptoCurrency? value)
        {
            if (value != null)
            {
                PortfolioTransactionPrice = value.CurrentPrice;
            }
        }

        public void Dispose()
        {
            _cryptoService.DataUpdated -= HandleDataUpdated;
            _cryptoService.PortfolioUpdated -= HandlePortfolioUpdated;
            _conversionCts?.Cancel();
            _conversionCts?.Dispose();
        }

        private async void QueueCurrencyConversion()
        {
            _conversionCts?.Cancel();
            _conversionCts?.Dispose();

            _conversionCts = new CancellationTokenSource();
            var token = _conversionCts.Token;

            try
            {
                await Task.Delay(250, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                await ConvertCurrencyAsync();
            }
            catch (TaskCanceledException)
            {
                // Ignore canceled conversion requests.
            }
            catch (Exception ex)
            {
                StatusMessage = $"Currency conversion failed: {ex.Message}";
            }
        }

        private static PlotModel CreateEmptyChartModel()
        {
            var model = CreateBaseChartModel();
            model.Title = "Market trend";
            model.Subtitle = "Select an asset to display its recent price movement.";
            return model;
        }

        private static PlotModel CreatePriceChartModel(IReadOnlyList<PriceHistory> history, string assetName, int days)
        {
            if (history.Count == 0)
            {
                return CreateEmptyChartModel();
            }

            var model = CreateBaseChartModel();
            model.Title = $"{assetName} price history";
            model.Subtitle = days == 1 ? "Intraday view" : $"{days}-day trend";

            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = days == 1 ? "HH:mm" : "dd MMM",
                TextColor = OxyColor.Parse("#5F728A"),
                AxislineColor = OxyColor.Parse("#C8D6E5"),
                TicklineColor = OxyColor.Parse("#C8D6E5"),
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.Parse("#E7EEF6"),
                MinorGridlineStyle = LineStyle.None
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                TextColor = OxyColor.Parse("#5F728A"),
                AxislineColor = OxyColor.Parse("#C8D6E5"),
                TicklineColor = OxyColor.Parse("#C8D6E5"),
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColor.Parse("#E7EEF6"),
                MinorGridlineStyle = LineStyle.None,
                LabelFormatter = value => value.ToString("C2")
            });

            var lineSeries = new LineSeries
            {
                Color = OxyColor.Parse("#0F9D8A"),
                StrokeThickness = 3,
                MarkerType = MarkerType.None,
                TrackerFormatString = "{2:dd MMM yyyy HH:mm}\nPrice: {4:C2}"
            };

            foreach (var point in history)
            {
                lineSeries.Points.Add(new DataPoint(DateTimeAxis.ToDouble(point.Date), (double)point.Price));
            }

            model.Series.Add(lineSeries);
            return model;
        }

        private static PlotModel CreateBaseChartModel() =>
            new()
            {
                Background = OxyColors.Transparent,
                PlotAreaBorderThickness = new OxyThickness(0),
                TextColor = OxyColor.Parse("#5F728A"),
                TitleColor = OxyColor.Parse("#112033"),
                SubtitleColor = OxyColor.Parse("#5F728A"),
                Padding = new OxyThickness(0)
            };
    }
}
