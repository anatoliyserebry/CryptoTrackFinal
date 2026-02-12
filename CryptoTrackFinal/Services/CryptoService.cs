using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CryptoTrackClient.Models;
using CryptoTrackClient.Services.Interfaces;
using Timer = System.Timers.Timer;

namespace CryptoTrackClient.Services
{
    public class CryptoService : ICryptoService, IDisposable
    {
        private readonly ILogger<CryptoService> _logger;
        private readonly CryptoServiceOptions _options;
        private readonly List<IApiClient> _apiClients;
        private readonly Timer _refreshTimer;
        private readonly Timer _portfolioTimer;
        private readonly string _portfolioFilePath;
        private readonly ConcurrentDictionary<string, CryptoCurrency> _cachedCurrencies;
        private readonly ConcurrentDictionary<string, PriceHistoryCache> _priceHistoryCache;
        private readonly ConcurrentDictionary<string, FiatCurrency> _fiatCurrencyCache;
        private readonly Random _random = new();

        private IApiClient _activeApiClient;
        private List<string> _favorites = new();
        private List<Transaction> _transactions = new();

        public event Action DataUpdated;
        public event Action PortfolioUpdated;

        public string ActiveApiName => _activeApiClient?.ApiName ?? "None";
        public List<string> AvailableApis => _apiClients.Select(c => c.ApiName).ToList();

        public CryptoService(
            ILogger<CryptoService> logger,
            IOptions<CryptoServiceOptions> options,
            IEnumerable<IApiClient> apiClients)
        {
            _logger = logger;
            _options = options.Value;
            _apiClients = apiClients.OrderBy(c => c.Priority).ToList();

            _cachedCurrencies = new ConcurrentDictionary<string, CryptoCurrency>();
            _priceHistoryCache = new ConcurrentDictionary<string, PriceHistoryCache>();
            _fiatCurrencyCache = new ConcurrentDictionary<string, FiatCurrency>();

            _portfolioFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CryptoTrackClient",
                "portfolio.json");

            LoadPortfolio();

            // Initialize API
            _ = InitializeApiAsync();

            // Setup timers
            _refreshTimer = new Timer(_options.RefreshInterval.TotalMilliseconds)
            {
                AutoReset = true,
                Enabled = _options.AutoRefreshEnabled
            };
            _refreshTimer.Elapsed += async (s, e) => await RefreshDataAsync();

            _portfolioTimer = new Timer(_options.PortfolioUpdateInterval.TotalMilliseconds)
            {
                AutoReset = true,
                Enabled = true
            };
            _portfolioTimer.Elapsed += async (s, e) => await UpdatePortfolioValuesAsync();
        }

        private async Task InitializeApiAsync()
        {
            _logger.LogInformation("Testing API connections...");

            foreach (var client in _apiClients)
            {
                try
                {
                    if (await client.TestConnectionAsync())
                    {
                        _activeApiClient = client;
                        _logger.LogInformation("Using {ApiName} API", client.ApiName);
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to connect to {ApiName}", client.ApiName);
                }
            }

            if (_activeApiClient == null)
            {
                _logger.LogError("All APIs failed to connect");
                LoadFallbackData();
                return;
            }

            await LoadDataAsync();
            await LoadFiatCurrenciesAsync();
        }

        private async Task LoadDataAsync()
        {
            if (_activeApiClient == null)
            {
                _logger.LogWarning("No active API client available. Loading fallback data.");
                LoadFallbackData();
                return;
            }

            try
            {
                var data = await _activeApiClient.GetTopCryptocurrenciesAsync(100);

                foreach (var currency in data)
                {
                    _cachedCurrencies.AddOrUpdate(
                        currency.Id,
                        currency,
                        (key, oldValue) => currency);
                }

                DataUpdated?.Invoke();
                _logger.LogInformation("Loaded {Count} currencies", data.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load data from {ApiName}", _activeApiClient.ApiName);
                await TryNextApiAsync();
            }
        }

        private async Task TryNextApiAsync()
        {
            var nextClient = _apiClients
                .Where(c => c != _activeApiClient)
                .OrderBy(c => c.Priority)
                .FirstOrDefault();

            if (nextClient != null)
            {
                _activeApiClient = nextClient;
                _logger.LogInformation("Switched to {ApiName} API", _activeApiClient.ApiName);
                await LoadDataAsync();
            }
            else
            {
                _logger.LogWarning("All APIs failed, using fallback data");
                LoadFallbackData();
            }
        }

        private void LoadFallbackData()
        {
            var fallbackData = new List<CryptoCurrency>
            {
                new() {
                    Id = "bitcoin",
                    Name = "Bitcoin",
                    Symbol = "BTC",
                    CurrentPrice = 45000,
                    MarketCap = 880000000000,
                    PriceChange24h = 500,
                    PriceChangePercentage24h = 1.12m,
                    LastUpdated = DateTime.Now,
                    Volume24h = 25000000000,
                    Rank = 1
                },
                new() {
                    Id = "ethereum",
                    Name = "Ethereum",
                    Symbol = "ETH",
                    CurrentPrice = 2500,
                    MarketCap = 300000000000,
                    PriceChange24h = -25,
                    PriceChangePercentage24h = -0.99m,
                    LastUpdated = DateTime.Now,
                    Volume24h = 12000000000,
                    Rank = 2
                },
                new() {
                    Id = "binancecoin",
                    Name = "Binance Coin",
                    Symbol = "BNB",
                    CurrentPrice = 320,
                    MarketCap = 50000000000,
                    PriceChange24h = 5,
                    PriceChangePercentage24h = 1.56m,
                    LastUpdated = DateTime.Now,
                    Volume24h = 2000000000,
                    Rank = 3
                },
                new() {
                    Id = "ripple",
                    Name = "Ripple",
                    Symbol = "XRP",
                    CurrentPrice = 0.75m,
                    MarketCap = 40000000000,
                    PriceChange24h = 0.01m,
                    PriceChangePercentage24h = 1.35m,
                    LastUpdated = DateTime.Now,
                    Volume24h = 1500000000,
                    Rank = 4
                },
                new() {
                    Id = "cardano",
                    Name = "Cardano",
                    Symbol = "ADA",
                    CurrentPrice = 0.45m,
                    MarketCap = 16000000000,
                    PriceChange24h = 0.02m,
                    PriceChangePercentage24h = 4.65m,
                    LastUpdated = DateTime.Now,
                    Volume24h = 800000000,
                    Rank = 5
                },
            };

            foreach (var currency in fallbackData)
            {
                _cachedCurrencies[currency.Id] = currency;
            }

            DataUpdated?.Invoke();
        }

        private async Task LoadFiatCurrenciesAsync()
        {
            try
            {
                if (_activeApiClient.SupportsFiatCurrencies)
                {
                    var currencies = await _activeApiClient.GetFiatCurrenciesAsync();
                    foreach (var currency in currencies)
                    {
                        _fiatCurrencyCache[currency.Code] = currency;
                    }
                }
                else
                {
                    // Use fixed list
                    var fixedCurrencies = new List<FiatCurrency>
                    {
                        new() { Code = "USD", Name = "US Dollar", Symbol = "$", RateToUSD = 1m },
                        new() { Code = "EUR", Name = "Euro", Symbol = "€", RateToUSD = 0.85m },
                        new() { Code = "GBP", Name = "British Pound", Symbol = "£", RateToUSD = 0.73m },
                        new() { Code = "RUB", Name = "Russian Ruble", Symbol = "₽", RateToUSD = 0.011m },
                        new() { Code = "CNY", Name = "Chinese Yuan", Symbol = "¥", RateToUSD = 0.14m }
                    };

                    foreach (var currency in fixedCurrencies)
                    {
                        _fiatCurrencyCache[currency.Code] = currency;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load fiat currencies");
            }
        }

        // ========== CRYPTOCURRENCY METHODS ==========

        public async Task<List<CryptoCurrency>> GetCryptoCurrenciesAsync()
        {
            if (_cachedCurrencies.IsEmpty)
            {
                await LoadDataAsync();
            }

            return _cachedCurrencies.Values
                .Select(c => new CryptoCurrency
                {
                    Id = c.Id,
                    Name = c.Name,
                    Symbol = c.Symbol,
                    CurrentPrice = c.CurrentPrice,
                    MarketCap = c.MarketCap,
                    PriceChange24h = c.PriceChange24h,
                    PriceChangePercentage24h = c.PriceChangePercentage24h,
                    IsFavorite = _favorites.Contains(c.Id),
                    LastUpdated = c.LastUpdated,
                    Volume24h = c.Volume24h,
                    CirculatingSupply = c.CirculatingSupply,
                    Rank = c.Rank
                })
                .OrderBy(c => c.Rank ?? int.MaxValue)
                .ToList();
        }

        public async Task<CryptoCurrency> GetCryptoCurrencyByIdAsync(string id)
        {
            if (_cachedCurrencies.TryGetValue(id, out var currency))
            {
                currency.IsFavorite = _favorites.Contains(id);
                return currency;
            }

            // Not in cache, request from API
            foreach (var client in _apiClients)
            {
                try
                {
                    var crypto = await client.GetCryptocurrencyByIdAsync(id);
                    if (crypto != null)
                    {
                        crypto.IsFavorite = _favorites.Contains(id);
                        _cachedCurrencies[id] = crypto;
                        return crypto;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get {CryptoId} from {ApiName}", id, client.ApiName);
                }
            }

            return new CryptoCurrency
            {
                Id = id,
                Name = id.ToUpper(),
                Symbol = id.ToUpper(),
                LastUpdated = DateTime.Now
            };
        }

        public async Task<List<CryptoCurrency>> GetFavoriteCurrenciesAsync()
        {
            var allCurrencies = await GetCryptoCurrenciesAsync();
            return allCurrencies.Where(c => _favorites.Contains(c.Id)).ToList();
        }

        public async Task ToggleFavoriteAsync(string cryptoId)
        {
            if (_favorites.Contains(cryptoId))
                _favorites.Remove(cryptoId);
            else
                _favorites.Add(cryptoId);

            await SavePortfolioAsync();
            DataUpdated?.Invoke();
        }

        public async Task<List<PriceHistory>> GetPriceHistoryAsync(string cryptoId, int days = 7)
        {
            var cacheKey = $"{cryptoId}_{days}";

            // Check cache
            if (_priceHistoryCache.TryGetValue(cacheKey, out var cache) &&
                cache.ExpiryTime > DateTime.Now)
            {
                return cache.History;
            }

            // Request from API
            foreach (var client in _apiClients)
            {
                try
                {
                    var history = await client.GetPriceHistoryAsync(cryptoId, days);

                    // Save to cache for 5 minutes
                    _priceHistoryCache[cacheKey] = new PriceHistoryCache
                    {
                        History = history,
                        ExpiryTime = DateTime.Now.AddMinutes(5)
                    };

                    return history;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get price history for {CryptoId}", cryptoId);
                }
            }

            return await GenerateMockHistoryAsync(cryptoId, days);
        }

        private async Task<List<PriceHistory>> GenerateMockHistoryAsync(string cryptoId, int days)
        {
            await Task.Delay(100);

            var result = new List<PriceHistory>();
            var basePrice = _cachedCurrencies.TryGetValue(cryptoId, out var currency)
                ? currency.CurrentPrice
                : 100;

            var startDate = DateTime.Now.AddDays(-days);
            var points = days <= 1 ? 24 * days : days;

            for (int i = 0; i < points; i++)
            {
                var date = days <= 1 ? startDate.AddHours(i) : startDate.AddDays(i);
                var noise = (decimal)(_random.NextDouble() * 0.02 - 0.01);
                var trend = (decimal)(Math.Sin(i * 0.1) * 0.05);
                var price = basePrice * (1 + trend + noise);

                result.Add(new PriceHistory(
                    date,
                    Math.Round(price, 2),
                    Math.Round(basePrice * 1000 * (1 + (decimal)_random.NextDouble() * 0.5), 2)
                ));
            }

            return result;
        }

        // ========== FIAT CURRENCY METHODS ==========

        public async Task<List<FiatCurrency>> GetFiatCurrenciesAsync()
        {
            if (_fiatCurrencyCache.IsEmpty)
            {
                await LoadFiatCurrenciesAsync();
            }

            return _fiatCurrencyCache.Values
                .OrderBy(f => f.Code)
                .ToList();
        }

        public async Task<decimal> ConvertCurrencyAsync(decimal amount, string fromCurrency, string toCurrency)
        {
            try
            {
                if (fromCurrency == toCurrency) return amount;

                // Try active API
                if (_activeApiClient != null && _activeApiClient.SupportsFiatCurrencies)
                {
                    var rate = await _activeApiClient.GetExchangeRateAsync(fromCurrency, toCurrency);
                    return amount * rate;
                }

                // Use cached rates
                var fromFiat = _fiatCurrencyCache.Values.FirstOrDefault(f => f.Code == fromCurrency.ToUpper());
                var toFiat = _fiatCurrencyCache.Values.FirstOrDefault(f => f.Code == toCurrency.ToUpper());

                if (fromFiat != null && toFiat != null)
                {
                    // Convert via USD
                    var amountInUsd = fromFiat.ConvertToUSD(amount);
                    return toFiat.ConvertFromUSD(amountInUsd);
                }

                return amount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert currency");
                return amount;
            }
        }

        // ========== PORTFOLIO METHODS ==========

        public async Task AddTransactionAsync(Transaction transaction)
        {
            _transactions.Add(transaction);
            await SavePortfolioAsync();
            await UpdatePortfolioValuesAsync();
            PortfolioUpdated?.Invoke();
        }

        public async Task UpdateTransactionAsync(Transaction transaction)
        {
            var existing = _transactions.FirstOrDefault(t => t.Id == transaction.Id);
            if (existing != null)
            {
                _transactions.Remove(existing);
                _transactions.Add(transaction);
                await SavePortfolioAsync();
                await UpdatePortfolioValuesAsync();
                PortfolioUpdated?.Invoke();
            }
        }

        public async Task DeleteTransactionAsync(Guid transactionId)
        {
            var transaction = _transactions.FirstOrDefault(t => t.Id == transactionId);
            if (transaction != null)
            {
                _transactions.Remove(transaction);
                await SavePortfolioAsync();
                await UpdatePortfolioValuesAsync();
                PortfolioUpdated?.Invoke();
            }
        }

        public async Task<List<Transaction>> GetTransactionsAsync()
        {
            return _transactions.OrderByDescending(t => t.TransactionDate).ToList();
        }

        public async Task<PortfolioSummary> GetPortfolioSummaryAsync()
        {
            var assets = await GetPortfolioAssetsAsync();
            var totalInvested = assets.Sum(a => a.TotalInvested);
            var currentValue = assets.Sum(a => a.CurrentValue);
            var totalValue = assets.Sum(a => a.CurrentValue);

            // Calculate percentages
            foreach (var asset in assets)
            {
                asset.PercentageOfPortfolio = totalValue > 0 ? (asset.CurrentValue / totalValue) * 100 : 0;
            }

            return new PortfolioSummary
            {
                TotalInvested = totalInvested,
                CurrentValue = currentValue,
                Assets = assets,
                LastUpdated = DateTime.Now
            };
        }

        public async Task<List<PortfolioAsset>> GetPortfolioAssetsAsync()
        {
            var assets = new Dictionary<string, PortfolioAsset>();

            // Group transactions by cryptocurrency
            foreach (var transaction in _transactions)
            {
                if (!assets.TryGetValue(transaction.CryptoId, out var asset))
                {
                    asset = new PortfolioAsset
                    {
                        CryptoId = transaction.CryptoId,
                        Symbol = transaction.CryptoSymbol,
                        Name = transaction.CryptoName
                    };
                    assets[transaction.CryptoId] = asset;
                }

                asset.Transactions.Add(transaction);
            }

            // Update current prices and recalculate
            foreach (var asset in assets.Values)
            {
                try
                {
                    var crypto = await GetCryptoCurrencyByIdAsync(asset.CryptoId);
                    asset.CurrentPrice = crypto.CurrentPrice;

                    // Recalculate metrics
                    var buyTransactions = asset.Transactions.Where(t => t.Type == TransactionType.Buy).ToList();
                    var sellTransactions = asset.Transactions.Where(t => t.Type == TransactionType.Sell).ToList();

                    // Total amount
                    asset.Amount = buyTransactions.Sum(t => t.Amount) - sellTransactions.Sum(t => t.Amount);

                    // Average buy price
                    asset.TotalInvested = buyTransactions.Sum(t => t.TotalCost);
                    asset.AverageBuyPrice = buyTransactions.Sum(t => t.Amount) > 0
                        ? asset.TotalInvested / buyTransactions.Sum(t => t.Amount)
                        : 0;

                    // Update current prices in transactions
                    foreach (var transaction in asset.Transactions)
                    {
                        transaction.CurrentPrice = crypto.CurrentPrice;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update price for {CryptoId}", asset.CryptoId);
                }
            }

            return assets.Values.Where(a => a.Amount > 0).ToList();
        }

        public async Task<decimal> GetPortfolioValueAsync()
        {
            var summary = await GetPortfolioSummaryAsync();
            return summary.CurrentValue;
        }

        private async Task UpdatePortfolioValuesAsync()
        {
            // Update current prices for all assets
            var cryptoIds = _transactions.Select(t => t.CryptoId).Distinct().ToList();

            foreach (var cryptoId in cryptoIds)
            {
                try
                {
                    var crypto = await GetCryptoCurrencyByIdAsync(cryptoId);
                    var transactions = _transactions.Where(t => t.CryptoId == cryptoId).ToList();

                    foreach (var transaction in transactions)
                    {
                        transaction.CurrentPrice = crypto.CurrentPrice;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update price for {CryptoId}", cryptoId);
                }
            }

            PortfolioUpdated?.Invoke();
        }

        public async Task RefreshDataAsync()
        {
            await LoadDataAsync();
            await UpdatePortfolioValuesAsync();
        }

        // ========== SAVE/LOAD ==========

        private void LoadPortfolio()
        {
            try
            {
                if (File.Exists(_portfolioFilePath))
                {
                    var json = File.ReadAllText(_portfolioFilePath);
                    var options = new JsonSerializerOptions
                    {
                        Converters = { new JsonStringEnumConverter() },
                        PropertyNameCaseInsensitive = true
                    };

                    var data = JsonSerializer.Deserialize<PortfolioData>(json, options);

                    _transactions = data?.Transactions ?? new List<Transaction>();
                    _favorites = data?.Favorites ?? new List<string>();

                    _logger.LogInformation("Loaded portfolio with {TransactionCount} transactions", _transactions.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load portfolio");
            }
        }

        private async Task SavePortfolioAsync()
        {
            try
            {
                var directory = Path.GetDirectoryName(_portfolioFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var data = new PortfolioData
                {
                    Transactions = _transactions,
                    Favorites = _favorites
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                };

                var json = JsonSerializer.Serialize(data, options);
                await File.WriteAllTextAsync(_portfolioFilePath, json);

                _logger.LogInformation("Saved portfolio with {TransactionCount} transactions", _transactions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save portfolio");
            }
        }

        // ========== UTILITIES ==========

        public async Task<bool> TestApiConnectionAsync()
        {
            if (_activeApiClient == null)
                return false;

            return await _activeApiClient.TestConnectionAsync();
        }

        public async Task SwitchToApiAsync(string apiName)
        {
            var newClient = _apiClients.FirstOrDefault(c => c.ApiName == apiName);
            if (newClient != null && newClient != _activeApiClient)
            {
                _activeApiClient = newClient;
                await LoadDataAsync();
                await LoadFiatCurrenciesAsync();
                DataUpdated?.Invoke();
            }
        }

        public void Dispose()
        {
            _refreshTimer?.Dispose();
            _portfolioTimer?.Dispose();

            foreach (var client in _apiClients.OfType<IDisposable>())
            {
                client.Dispose();
            }
        }

        #region Helper Classes
        private class PriceHistoryCache
        {
            public List<PriceHistory> History { get; set; }
            public DateTime ExpiryTime { get; set; }
        }

        private class PortfolioData
        {
            public List<Transaction> Transactions { get; set; } = new();
            public List<string> Favorites { get; set; } = new();
        }
        #endregion
    }

    public class CryptoServiceOptions
    {
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan PortfolioUpdateInterval { get; set; } = TimeSpan.FromMinutes(1);
        public bool AutoRefreshEnabled { get; set; } = true;
    }
}
