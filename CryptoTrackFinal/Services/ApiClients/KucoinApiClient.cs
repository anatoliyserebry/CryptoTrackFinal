using CryptoTrackClient.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTrackClient.Services.ApiClients
{
    public class KucoinApiClient : BaseApiClient
    {
        public override string ApiName => "KuCoin";
        public override int Priority => 6;
        public override bool SupportsFiatCurrencies => false;
        public override int RequestLimitPerMinute => 300;

        public KucoinApiClient()
            : base("https://api.kucoin.com/api/v1/", TimeSpan.FromSeconds(10))
        {
        }

        public override async Task<List<CryptoCurrency>> GetTopCryptocurrenciesAsync(int limit = 100)
        {
            try
            {
                var json = await GetStringWithRetryAsync("market/allTickers");
                var data = JsonConvert.DeserializeObject<KucoinTickersResponse>(json);

                // Filter USDT pairs and take top by volume
                var usdtPairs = data.data.ticker
                    .Where(t => t.symbol.EndsWith("-USDT"))
                    .OrderByDescending(t => decimal.Parse(t.volValue))
                    .Take(limit)
                    .ToList();

                return usdtPairs.Select(t => new CryptoCurrency
                {
                    Id = t.symbol.Replace("-USDT", "").ToLower(),
                    Name = t.symbol.Replace("-USDT", "").ToUpper(),
                    Symbol = t.symbol.Replace("-USDT", "").ToUpper(),
                    CurrentPrice = decimal.Parse(t.last),
                    PriceChange24h = decimal.Parse(t.changePrice),
                    PriceChangePercentage24h = decimal.Parse(t.changeRate) * 100,
                    Volume24h = decimal.Parse(t.volValue),
                    LastUpdated = DateTime.Now
                }).ToList();
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
                var symbol = $"{id.ToUpper()}-USDT";
                var json = await GetStringWithRetryAsync($"market/orderbook/level1?symbol={symbol}");
                var data = JsonConvert.DeserializeObject<KucoinTickerResponse>(json);

                // Get 24h stats
                var statsJson = await GetStringWithRetryAsync($"market/stats?symbol={symbol}");
                var stats = JsonConvert.DeserializeObject<KucoinStatsResponse>(statsJson);

                return new CryptoCurrency
                {
                    Id = id,
                    Name = id.ToUpper(),
                    Symbol = id.ToUpper(),
                    CurrentPrice = decimal.Parse(data.data.price),
                    PriceChange24h = decimal.Parse(stats.data.changePrice),
                    PriceChangePercentage24h = decimal.Parse(stats.data.changeRate) * 100,
                    Volume24h = decimal.Parse(stats.data.volValue),
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
                var symbol = $"{cryptoId.ToUpper()}-USDT";
                var type = days <= 1 ? "1hour" : "1day";
                var end = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var start = DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeSeconds();

                var json = await GetStringWithRetryAsync(
                    $"market/candles?type={type}&symbol={symbol}&startAt={start}&endAt={end}");

                var data = JsonConvert.DeserializeObject<KucoinCandlesResponse>(json);

                return data.data.Select(c => new PriceHistory(
                    DateTimeOffset.FromUnixTimeSeconds(long.Parse(c[0])).DateTime,
                    decimal.Parse(c[2]), // Close price
                    decimal.Parse(c[5])
                )).ToList();
            }
            catch (Exception ex)
            {
                throw new ApiException(ApiName, $"Failed to get price history for {cryptoId}", ex);
            }
        }

        public override Task<List<FiatCurrency>> GetFiatCurrenciesAsync()
        {
            return Task.FromResult(new List<FiatCurrency>());
        }

        public override Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency)
        {
            return Task.FromResult(1m);
        }

        public override async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("timestamp");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        #region JSON Classes
        private class KucoinTickersResponse
        {
            public KucoinTickersData data { get; set; }
        }

        private class KucoinTickersData
        {
            public List<KucoinTicker> ticker { get; set; }
        }

        private class KucoinTicker
        {
            public string symbol { get; set; }
            public string last { get; set; }
            public string changePrice { get; set; }
            public string changeRate { get; set; }
            public string volValue { get; set; }
        }

        private class KucoinTickerResponse
        {
            public KucoinTickerData data { get; set; }
        }

        private class KucoinTickerData
        {
            public string price { get; set; }
        }

        private class KucoinStatsResponse
        {
            public KucoinStatsData data { get; set; }
        }

        private class KucoinStatsData
        {
            public string changePrice { get; set; }
            public string changeRate { get; set; }
            public string volValue { get; set; }
        }

        private class KucoinCandlesResponse
        {
            public List<List<string>> data { get; set; }
        }
        #endregion
    }
}