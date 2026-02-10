using CryptoTrackClient.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTrackClient.Services.ApiClients
{
    public class CoinStatsApiClient : BaseApiClient
    {
        public override string ApiName => "CoinStats";
        public override int Priority => 7;
        public override bool SupportsFiatCurrencies => true;
        public override int RequestLimitPerMinute => 100;

        public CoinStatsApiClient()
            : base("https://api.coinstats.app/public/v1/", TimeSpan.FromSeconds(10))
        {
        }

        public override async Task<List<CryptoCurrency>> GetTopCryptocurrenciesAsync(int limit = 100)
        {
            try
            {
                var json = await GetStringWithRetryAsync($"coins?limit={limit}");
                var data = JsonConvert.DeserializeObject<CoinStatsResponse>(json);

                return data.coins.Select(c => new CryptoCurrency
                {
                    Id = c.id,
                    Name = c.name,
                    Symbol = c.symbol.ToUpper(),
                    CurrentPrice = c.price,
                    MarketCap = c.marketCap,
                    PriceChange24h = c.priceChange,
                    PriceChangePercentage24h = c.priceChange1d,
                    Volume24h = c.volume,
                    Rank = c.rank,
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
                var json = await GetStringWithRetryAsync($"coins/{id}");
                var data = JsonConvert.DeserializeObject<CoinStatsCoinResponse>(json);

                var coin = data.coin;
                return new CryptoCurrency
                {
                    Id = coin.id,
                    Name = coin.name,
                    Symbol = coin.symbol.ToUpper(),
                    CurrentPrice = coin.price,
                    MarketCap = coin.marketCap,
                    PriceChange24h = coin.priceChange,
                    PriceChangePercentage24h = coin.priceChange1d,
                    Volume24h = coin.volume,
                    Rank = coin.rank,
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
                var json = await GetStringWithRetryAsync($"charts?period={days}d&coinId={cryptoId}");
                var data = JsonConvert.DeserializeObject<CoinStatsChartResponse>(json);

                return data.chart.Select(c => new PriceHistory(
                    DateTimeOffset.FromUnixTimeMilliseconds((long)c[0]).DateTime,
                    (decimal)c[1]
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
                var json = await GetStringWithRetryAsync("fiats");
                var data = JsonConvert.DeserializeObject<CoinStatsFiatsResponse>(json);

                return data.fiats.Select(f => new FiatCurrency
                {
                    Code = f.symbol.ToUpper(),
                    Name = f.name,
                    Symbol = f.symbol,
                    RateToUSD = 1m / f.rate, // Convert to USD
                    LastUpdated = DateTime.Now
                }).Take(10).ToList();
            }
            catch (Exception ex)
            {
                throw new ApiException(ApiName, "Failed to get fiat currencies", ex);
            }
        }

        public override async Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency)
        {
            try
            {
                // Get all fiat rates
                var fiats = await GetFiatCurrenciesAsync();

                var fromFiat = fiats.FirstOrDefault(f => f.Code == fromCurrency.ToUpper());
                var toFiat = fiats.FirstOrDefault(f => f.Code == toCurrency.ToUpper());

                if (fromFiat != null && toFiat != null)
                {
                    // Convert via USD
                    return toFiat.RateToUSD / fromFiat.RateToUSD;
                }

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
                var response = await _httpClient.GetAsync("coins?limit=1");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        #region JSON Classes
        private class CoinStatsResponse
        {
            public List<CoinStatsCoin> coins { get; set; }
        }

        private class CoinStatsCoinResponse
        {
            public CoinStatsCoin coin { get; set; }
        }

        private class CoinStatsCoin
        {
            public string id { get; set; }
            public string name { get; set; }
            public string symbol { get; set; }
            public decimal price { get; set; }
            public decimal marketCap { get; set; }
            public decimal priceChange { get; set; }
            public decimal priceChange1d { get; set; }
            public decimal volume { get; set; }
            public int rank { get; set; }
        }

        private class CoinStatsChartResponse
        {
            public List<List<decimal>> chart { get; set; }
        }

        private class CoinStatsFiatsResponse
        {
            public List<CoinStatsFiat> fiats { get; set; }
        }

        private class CoinStatsFiat
        {
            public string symbol { get; set; }
            public string name { get; set; }
            public decimal rate { get; set; }
        }
        #endregion
    }
}
