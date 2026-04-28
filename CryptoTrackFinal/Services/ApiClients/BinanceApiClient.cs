using CryptoTrackClient.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace CryptoTrackClient.Services.ApiClients
{
    public class BinanceApiClient : BaseApiClient
    {
        private readonly Dictionary<string, string> _symbolMapping;

        public override string ApiName => "Binance";
        public override int Priority => 3;
        public override bool SupportsFiatCurrencies => true;
        public override int RequestLimitPerMinute => 1200;

        public BinanceApiClient()
            : base("https://api.binance.com/api/v3/", TimeSpan.FromSeconds(10))
        {
            _symbolMapping = new Dictionary<string, string>
            {
                { "bitcoin", "BTCUSDT" },
                { "ethereum", "ETHUSDT" },
                { "binancecoin", "BNBUSDT" },
                { "ripple", "XRPUSDT" },
                { "cardano", "ADAUSDT" },
                { "solana", "SOLUSDT" },
                { "polkadot", "DOTUSDT" },
                { "dogecoin", "DOGEUSDT" },
                { "litecoin", "LTCUSDT" },
                { "chainlink", "LINKUSDT" },
                { "stellar", "XLMUSDT" },
                { "vechain", "VETUSDT" },
                { "monero", "XMRUSDT" },
                { "eos", "EOSUSDT" },
                { "tezos", "XTZUSDT" },
                { "cosmos", "ATOMUSDT" }
            };
        }

        public override async Task<List<CryptoCurrency>> GetTopCryptocurrenciesAsync(int limit = 100)
        {
            try
            {
                var json = await GetStringWithRetryAsync("ticker/24hr");
                var tickers = JsonConvert.DeserializeObject<List<BinanceTicker>>(json);

                // Filter only USDT pairs and take top by volume
                var usdtTickers = tickers
                    .Where(t => t.symbol.EndsWith("USDT"))
                    .OrderByDescending(t => decimal.Parse(t.volume))
                    .Take(limit)
                    .ToList();

                var result = new List<CryptoCurrency>();

                foreach (var ticker in usdtTickers)
                {
                    var symbol = ticker.symbol.Replace("USDT", "").ToLower();
                    result.Add(new CryptoCurrency
                    {
                        Id = symbol,
                        Name = GetCryptoName(symbol),
                        Symbol = symbol.ToUpper(),
                        CurrentPrice = decimal.Parse(ticker.lastPrice),
                        PriceChange24h = decimal.Parse(ticker.priceChange),
                        PriceChangePercentage24h = decimal.Parse(ticker.priceChangePercent),
                        Volume24h = decimal.Parse(ticker.volume) * decimal.Parse(ticker.lastPrice),
                        LastUpdated = DateTime.Now
                    });
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new ApiException(ApiName, "Failed to get top cryptocurrencies", ex);
            }
        }

        public override async Task<CryptoCurrency> GetCryptocurrencyByIdAsync(string id)
        {
            try
            {
                var symbol = GetSymbol(id);
                var json = await GetStringWithRetryAsync($"ticker/24hr?symbol={symbol}");
                var ticker = JsonConvert.DeserializeObject<BinanceTicker>(json);

                return new CryptoCurrency
                {
                    Id = id,
                    Name = GetCryptoName(id),
                    Symbol = id.ToUpper(),
                    CurrentPrice = decimal.Parse(ticker.lastPrice),
                    PriceChange24h = decimal.Parse(ticker.priceChange),
                    PriceChangePercentage24h = decimal.Parse(ticker.priceChangePercent),
                    Volume24h = decimal.Parse(ticker.volume) * decimal.Parse(ticker.lastPrice),
                    LastUpdated = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                throw new ApiException(ApiName, $"Failed to get cryptocurrency {id}", ex);
            }
        }

        public override async Task<List<PriceHistory>> GetPriceHistoryAsync(string cryptoId, int days = 7)
        {
            try
            {
                var symbol = GetSymbol(cryptoId);
                var interval = days <= 1 ? "1h" : "1d";
                var limit = days <= 1 ? 24 * days : days;

                var json = await GetStringWithRetryAsync(
                    $"klines?symbol={symbol}&interval={interval}&limit={limit}");

                var klines = JsonConvert.DeserializeObject<List<List<object>>>(json);

                return klines.Select(k => new PriceHistory(
                    DateTimeOffset.FromUnixTimeMilliseconds((long)k[0]).DateTime,
                    decimal.Parse(k[4].ToString()),
                    decimal.Parse(k[5].ToString())
                )).ToList();
            }
            catch (Exception ex)
            {
                throw new ApiException(ApiName, $"Failed to get price history for {cryptoId}", ex);
            }
        }

        public override async Task<List<FiatCurrency>> GetFiatCurrenciesAsync()
        {
            try
            {
                // Binance doesn't provide direct fiat API
                // Using fixed list
                return new List<FiatCurrency>
                {
                    new() { Code = "USD", Name = "US Dollar", Symbol = "$", RateToUSD = 1m, LastUpdated = DateTime.Now },
                    new() { Code = "EUR", Name = "Euro", Symbol = "€", RateToUSD = 0.85m, LastUpdated = DateTime.Now },
                    new() { Code = "GBP", Name = "British Pound", Symbol = "£", RateToUSD = 0.73m, LastUpdated = DateTime.Now },
                    new() { Code = "RUB", Name = "Russian Ruble", Symbol = "₽", RateToUSD = 0.011m, LastUpdated = DateTime.Now }
                };
            }
            catch
            {
                return new List<FiatCurrency>();
            }
        }

        public override async Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency)
        {
            try
            {
                if (fromCurrency == toCurrency) return 1m;

                // For cryptocurrencies
                if (fromCurrency != "USD" && toCurrency != "USD")
                {
                    var fromToUsd = await GetExchangeRateAsync(fromCurrency, "USD");
                    var usdToTo = await GetExchangeRateAsync("USD", toCurrency);
                    return fromToUsd * usdToTo;
                }

                // Return fixed rates for now
                var rates = new Dictionary<string, decimal>
                {
                    { "USDEUR", 0.85m },
                    { "USDGBP", 0.73m },
                    { "USDRUB", 91.5m },
                    { "USDCNY", 7.2m },
                    { "EURUSD", 1.18m },
                    { "GBPUSD", 1.37m },
                    { "RUBUSD", 0.011m },
                    { "CNYUSD", 0.14m }
                };

                var key = $"{fromCurrency}{toCurrency}";
                if (rates.ContainsKey(key))
                    return rates[key];

                return 1m;
            }
            catch
            {
                return 1m;
            }
        }

        public override async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("ping");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private string GetSymbol(string cryptoId)
        {
            return _symbolMapping.TryGetValue(cryptoId.ToLower(), out var symbol)
                ? symbol
                : cryptoId.ToUpper() + "USDT";
        }

        private string GetCryptoName(string symbol)
        {
            var names = new Dictionary<string, string>
            {
                { "btc", "Bitcoin" },
                { "eth", "Ethereum" },
                { "bnb", "Binance Coin" },
                { "xrp", "Ripple" },
                { "ada", "Cardano" },
                { "sol", "Solana" },
                { "dot", "Polkadot" },
                { "doge", "Dogecoin" },
                { "ltc", "Litecoin" },
                { "link", "Chainlink" },
                { "xlm", "Stellar" },
                { "vet", "VeChain" },
                { "xmr", "Monero" },
                { "eos", "EOS" },
                { "xtz", "Tezos" },
                { "atom", "Cosmos" }
            };

            return names.TryGetValue(symbol.ToLower(), out var name) ? name : symbol.ToUpper();
        }

        #region JSON Classes
        private class BinanceTicker
        {
            public string symbol { get; set; }
            public string priceChange { get; set; }
            public string priceChangePercent { get; set; }
            public string lastPrice { get; set; }
            public string volume { get; set; }
        }
        #endregion
    }
}