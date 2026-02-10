using CryptoTrackClient.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTrackClient.Services.ApiClients
{
    public class CoinCapApiClient : BaseApiClient
    {
        public override string ApiName => "CoinCap";
        public override int Priority => 2;
        public override bool SupportsFiatCurrencies => false;
        public override int RequestLimitPerMinute => 200;

        public CoinCapApiClient()
            : base("https://api.coincap.io/v2/", TimeSpan.FromSeconds(10))
        {
        }

        public override async Task<List<CryptoCurrency>> GetTopCryptocurrenciesAsync(int limit = 100)
        {
            try
            {
                var json = await GetStringWithRetryAsync($"assets?limit={limit}");
                var data = JsonConvert.DeserializeObject<CoinCapResponse>(json);

                return data.data.Select(a => new CryptoCurrency
                {
                    Id = a.id.ToLower(),
                    Name = a.name,
                    Symbol = a.symbol.ToUpper(),
                    CurrentPrice = decimal.Parse(a.priceUsd),
                    MarketCap = decimal.Parse(a.marketCapUsd),
                    PriceChange24h = decimal.Parse(a.changePercent24Hr) / 100 * decimal.Parse(a.priceUsd),
                    PriceChangePercentage24h = decimal.Parse(a.changePercent24Hr),
                    Volume24h = decimal.Parse(a.volumeUsd24Hr),
                    Rank = int.Parse(a.rank),
                    LastUpdated = DateTime.Parse(a.date)
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
                var json = await GetStringWithRetryAsync($"assets/{id}");
                var data = JsonConvert.DeserializeObject<CoinCapAssetResponse>(json);

                var asset = data.data;
                return new CryptoCurrency
                {
                    Id = asset.id.ToLower(),
                    Name = asset.name,
                    Symbol = asset.symbol.ToUpper(),
                    CurrentPrice = decimal.Parse(asset.priceUsd),
                    MarketCap = decimal.Parse(asset.marketCapUsd),
                    PriceChange24h = decimal.Parse(asset.changePercent24Hr) / 100 * decimal.Parse(asset.priceUsd),
                    PriceChangePercentage24h = decimal.Parse(asset.changePercent24Hr),
                    Volume24h = decimal.Parse(asset.volumeUsd24Hr),
                    Rank = int.Parse(asset.rank),
                    LastUpdated = DateTime.Parse(asset.date)
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
                var end = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var start = DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeMilliseconds();

                var json = await GetStringWithRetryAsync(
                    $"assets/{cryptoId}/history?interval=d1&start={start}&end={end}");

                var data = JsonConvert.DeserializeObject<CoinCapHistoryResponse>(json);

                return data.data.Select(h => new PriceHistory(
                    DateTime.Parse(h.date),
                    decimal.Parse(h.priceUsd),
                    decimal.Parse(h.volumeUsd ?? "0")
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
                var response = await _httpClient.GetAsync("assets?limit=1");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        #region JSON Classes
        private class CoinCapResponse
        {
            public List<CoinCapAsset> data { get; set; }
        }

        private class CoinCapAssetResponse
        {
            public CoinCapAsset data { get; set; }
        }

        private class CoinCapHistoryResponse
        {
            public List<CoinCapHistory> data { get; set; }
        }

        private class CoinCapAsset
        {
            public string id { get; set; }
            public string rank { get; set; }
            public string symbol { get; set; }
            public string name { get; set; }
            public string priceUsd { get; set; }
            public string marketCapUsd { get; set; }
            public string volumeUsd24Hr { get; set; }
            public string changePercent24Hr { get; set; }
            public string date { get; set; }
        }

        private class CoinCapHistory
        {
            public string priceUsd { get; set; }
            public string volumeUsd { get; set; }
            public string date { get; set; }
        }
        #endregion
    }
}