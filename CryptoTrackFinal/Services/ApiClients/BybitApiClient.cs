using CryptoTrackClient.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoTrackClient.Services.ApiClients
{
    public class BybitApiClient : BaseApiClient
    {
        private readonly Dictionary<string, string> _symbolMapping;

        public override string ApiName => "Bybit";
        public override int Priority => 1;
        public override bool SupportsFiatCurrencies => false;
        public override int RequestLimitPerMinute => 600;

        public BybitApiClient()
            : base("https://api.bybit.com/v5/", TimeSpan.FromSeconds(10))
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
            var json = await GetStringWithRetryAsync("market/tickers?category=spot");
            var response = JsonConvert.DeserializeObject<BybitTickerResponse>(json);
            var tickers = response?.result?.list ?? new List<BybitTicker>();

            return tickers
                .Where(t => t.symbol != null && t.symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => ParseDecimal(t.turnover24h))
                .Take(limit)
                .Select(t =>
                {
                    var symbol = t.symbol.Replace("USDT", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
                    var lastPrice = ParseDecimal(t.lastPrice);
                    var changePct = ParseDecimal(t.price24hPcnt) * 100m;

                    return new CryptoCurrency
                    {
                        Id = GetCryptoId(symbol),
                        Name = GetCryptoName(symbol),
                        Symbol = symbol.ToUpperInvariant(),
                        CurrentPrice = lastPrice,
                        PriceChange24h = ParseDecimal(t.price24hPcnt) * lastPrice,
                        PriceChangePercentage24h = changePct,
                        Volume24h = ParseDecimal(t.turnover24h),
                        LastUpdated = DateTime.UtcNow
                    };
                })
                .ToList();
        }

        public override async Task<CryptoCurrency> GetCryptocurrencyByIdAsync(string id)
        {
            var symbol = GetSymbol(id);
            var json = await GetStringWithRetryAsync($"market/tickers?category=spot&symbol={symbol}");
            var response = JsonConvert.DeserializeObject<BybitTickerResponse>(json);
            var ticker = response?.result?.list?.FirstOrDefault() ?? throw new InvalidOperationException($"{ApiName}: No data for {id}");

            var lastPrice = ParseDecimal(ticker.lastPrice);
            var changeRatio = ParseDecimal(ticker.price24hPcnt);

            return new CryptoCurrency
            {
                Id = id,
                Name = GetCryptoName(id),
                Symbol = symbol.Replace("USDT", "", StringComparison.OrdinalIgnoreCase),
                CurrentPrice = lastPrice,
                PriceChange24h = changeRatio * lastPrice,
                PriceChangePercentage24h = changeRatio * 100m,
                Volume24h = ParseDecimal(ticker.turnover24h),
                LastUpdated = DateTime.UtcNow
            };
        }

        public override async Task<List<PriceHistory>> GetPriceHistoryAsync(string cryptoId, int days = 7)
        {
            var symbol = GetSymbol(cryptoId);
            var interval = days <= 1 ? "60" : "D";
            var limit = days <= 1 ? 24 : days;

            var json = await GetStringWithRetryAsync($"market/kline?category=spot&symbol={symbol}&interval={interval}&limit={limit}");
            var response = JsonConvert.DeserializeObject<BybitKlineResponse>(json);
            var candles = response?.result?.list ?? new List<List<string>>();

            return candles
                .Where(c => c.Count >= 6)
                .Select(c => new PriceHistory(
                    DateTimeOffset.FromUnixTimeMilliseconds(ParseLong(c[0])).UtcDateTime,
                    ParseDecimal(c[4]),
                    ParseDecimal(c[5])))
                .OrderBy(p => p.Date)
                .ToList();
        }

        public override Task<List<FiatCurrency>> GetFiatCurrenciesAsync() => Task.FromResult(new List<FiatCurrency>());

        public override Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency) => Task.FromResult(1m);

        public override async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("market/time");
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

        private static long ParseLong(string? value)
            => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0L;

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

        private class BybitTickerResponse
        {
            public BybitTickerResult? result { get; set; }
        }

        private class BybitTickerResult
        {
            public List<BybitTicker> list { get; set; } = new();
        }

        private class BybitTicker
        {
            public string symbol { get; set; } = string.Empty;
            public string lastPrice { get; set; } = "0";
            public string price24hPcnt { get; set; } = "0";
            public string turnover24h { get; set; } = "0";
        }

        private class BybitKlineResponse
        {
            public BybitKlineResult? result { get; set; }
        }

        private class BybitKlineResult
        {
            public List<List<string>> list { get; set; } = new();
        }
    }
}
