using CryptoTrackClient.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTrackClient.Services.ApiClients
{
    public class MexcApiClient : BaseApiClient
    {
        private readonly Dictionary<string, string> _symbolMapping;

        public override string ApiName => "MEXC";
        public override int Priority => 2;
        public override bool SupportsFiatCurrencies => false;
        public override int RequestLimitPerMinute => 1200;

        public MexcApiClient()
            : base("https://api.mexc.com/api/v3/", TimeSpan.FromSeconds(10))
        {
            _symbolMapping = new Dictionary<string, string>
            {
                { "bitcoin", "BTCUSDT" },
                { "ethereum", "ETHUSDT" },
                { "binancecoin", "BNBUSDT" },
                { "ripple", "XRPUSDT" },
                { "cardano", "ADAUSDT" },
                { "solana", "SOLUSDT" },
                { "dogecoin", "DOGEUSDT" }
            };
        }

        public override async Task<List<CryptoCurrency>> GetTopCryptocurrenciesAsync(int limit = 100)
        {
            var json = await GetStringWithRetryAsync("ticker/24hr");
            var tickers = JsonConvert.DeserializeObject<List<MexcTicker>>(json) ?? new List<MexcTicker>();

            return tickers
                .Where(t => t.symbol != null && t.symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => ParseDecimal(t.quoteVolume))
                .Take(limit)
                .Select(t =>
                {
                    var symbol = t.symbol.Replace("USDT", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
                    var lastPrice = ParseDecimal(t.lastPrice);
                    var changePct = ParseDecimal(t.priceChangePercent);
                    var quoteVol = ParseDecimal(t.quoteVolume);

                    return new CryptoCurrency
                    {
                        Id = GetCryptoId(symbol),
                        Name = GetCryptoName(symbol),
                        Symbol = symbol.ToUpperInvariant(),
                        CurrentPrice = lastPrice,
                        PriceChange24h = lastPrice * changePct / 100m,
                        PriceChangePercentage24h = changePct,
                        Volume24h = quoteVol,
                        LastUpdated = DateTime.UtcNow
                    };
                })
                .ToList();
        }

        public override async Task<CryptoCurrency> GetCryptocurrencyByIdAsync(string id)
        {
            var symbol = GetSymbol(id);
            var json = await GetStringWithRetryAsync($"ticker/24hr?symbol={symbol}");
            var ticker = JsonConvert.DeserializeObject<MexcTicker>(json) ?? throw new InvalidOperationException($"{ApiName}: No data for {id}");

            var lastPrice = ParseDecimal(ticker.lastPrice);
            var changePct = ParseDecimal(ticker.priceChangePercent);

            return new CryptoCurrency
            {
                Id = id,
                Name = GetCryptoName(id),
                Symbol = symbol.Replace("USDT", "", StringComparison.OrdinalIgnoreCase),
                CurrentPrice = lastPrice,
                PriceChange24h = lastPrice * changePct / 100m,
                PriceChangePercentage24h = changePct,
                Volume24h = ParseDecimal(ticker.quoteVolume),
                LastUpdated = DateTime.UtcNow
            };
        }

        public override async Task<List<PriceHistory>> GetPriceHistoryAsync(string cryptoId, int days = 7)
        {
            var symbol = GetSymbol(cryptoId);
            var interval = days <= 1 ? "1h" : "1d";
            var limit = days <= 1 ? 24 : days;

            var json = await GetStringWithRetryAsync($"klines?symbol={symbol}&interval={interval}&limit={limit}");
            var klines = JsonConvert.DeserializeObject<List<List<object>>>(json) ?? new List<List<object>>();

            return klines
                .Where(k => k.Count >= 6)
                .Select(k => new PriceHistory(
                    DateTimeOffset.FromUnixTimeMilliseconds(ParseLong(k[0])).UtcDateTime,
                    ParseDecimal(k[4]?.ToString()),
                    ParseDecimal(k[5]?.ToString())))
                .ToList();
        }

        public override Task<List<FiatCurrency>> GetFiatCurrenciesAsync() => Task.FromResult(new List<FiatCurrency>());

        public override Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency) => Task.FromResult(1m);

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
            return _symbolMapping.TryGetValue(cryptoId.ToLowerInvariant(), out var symbol)
                ? symbol
                : cryptoId.ToUpperInvariant() + "USDT";
        }

        private static decimal ParseDecimal(string? value)
            => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0m;

        private static long ParseLong(object value)
            => long.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0L;

        private string GetCryptoId(string symbol) => symbol switch
        {
            "btc" => "bitcoin",
            "eth" => "ethereum",
            "bnb" => "binancecoin",
            "xrp" => "ripple",
            "ada" => "cardano",
            "sol" => "solana",
            "doge" => "dogecoin",
            _ => symbol
        };

        private string GetCryptoName(string symbolOrId)
        {
            var key = symbolOrId.ToLowerInvariant();
            return key switch
            {
                "btc" or "bitcoin" => "Bitcoin",
                "eth" or "ethereum" => "Ethereum",
                "bnb" or "binancecoin" => "Binance Coin",
                "xrp" or "ripple" => "Ripple",
                "ada" or "cardano" => "Cardano",
                "sol" or "solana" => "Solana",
                "doge" or "dogecoin" => "Dogecoin",
                _ => symbolOrId.ToUpperInvariant()
            };
        }

        private class MexcTicker
        {
            public string symbol { get; set; } = string.Empty;
            public string lastPrice { get; set; } = "0";
            public string priceChangePercent { get; set; } = "0";
            public string quoteVolume { get; set; } = "0";
        }
    }
}
