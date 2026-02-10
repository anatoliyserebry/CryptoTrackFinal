using CryptoTrackClient.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTrackClient.Services.ApiClients
{
    public class CryptoCompareApiClient : BaseApiClient
    {
        private const string API_KEY = "YOUR_API_KEY"; // Get from cryptocompare.com
        public override string ApiName => "CryptoCompare";
        public override int Priority => 4;
        public override bool SupportsFiatCurrencies => true;
        public override int RequestLimitPerMinute => string.IsNullOrEmpty(API_KEY) ? 100 : 1000;

        public CryptoCompareApiClient()
            : base("https://min-api.cryptocompare.com/data/", TimeSpan.FromSeconds(10))
        {
            if (!string.IsNullOrEmpty(API_KEY))
            {
                _httpClient.DefaultRequestHeaders.Add("authorization", $"Apikey {API_KEY}");
            }
        }

        public override async Task<List<CryptoCurrency>> GetTopCryptocurrenciesAsync(int limit = 100)
        {
            try
            {
                var json = await GetStringWithRetryAsync($"top/mktcapfull?limit={limit}&tsym=USD");
                var data = JsonConvert.DeserializeObject<CryptoCompareResponse>(json);

                return data.Data.Select(d => new CryptoCurrency
                {
                    Id = d.CoinInfo.Name.ToLower(),
                    Name = d.CoinInfo.FullName,
                    Symbol = d.CoinInfo.Name.ToUpper(),
                    CurrentPrice = d.RAW?.USD?.PRICE ?? 0,
                    MarketCap = d.RAW?.USD?.MKTCAP ?? 0,
                    PriceChange24h = d.RAW?.USD?.CHANGE24HOUR ?? 0,
                    PriceChangePercentage24h = d.RAW?.USD?.CHANGEPCT24HOUR ?? 0,
                    Volume24h = d.RAW?.USD?.VOLUME24HOURTO ?? 0,
                    LastUpdated = DateTimeOffset.FromUnixTimeSeconds(d.RAW?.USD?.LASTUPDATE ?? 0).DateTime
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
                var json = await GetStringWithRetryAsync($"pricemultifull?fsyms={id.ToUpper()}&tsyms=USD");
                var data = JsonConvert.DeserializeObject<CryptoComparePriceResponse>(json);

                if (data.RAW.TryGetValue(id.ToUpper(), out var cryptoData) &&
                    cryptoData.TryGetValue("USD", out var priceData))
                {
                    return new CryptoCurrency
                    {
                        Id = id,
                        Name = id.ToUpper(),
                        Symbol = id.ToUpper(),
                        CurrentPrice = priceData.PRICE,
                        MarketCap = priceData.MKTCAP,
                        PriceChange24h = priceData.CHANGE24HOUR,
                        PriceChangePercentage24h = priceData.CHANGEPCT24HOUR,
                        Volume24h = priceData.VOLUME24HOURTO,
                        LastUpdated = DateTimeOffset.FromUnixTimeSeconds(priceData.LASTUPDATE).DateTime
                    };
                }

                throw new ApiException(ApiName, $"Cryptocurrency {id} not found", null);
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
                var limit = days <= 1 ? 24 : days;
                var aggregate = days <= 1 ? 1 : 24;

                var json = await GetStringWithRetryAsync(
                    $"v2/histoday?fsym={cryptoId.ToUpper()}&tsym=USD&limit={limit}&aggregate={aggregate}");

                var data = JsonConvert.DeserializeObject<CryptoCompareHistoryResponse>(json);

                return data.Data.Data.Select(d => new PriceHistory(
                    DateTimeOffset.FromUnixTimeSeconds(d.time).DateTime,
                    d.close,
                    d.volumeto
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
                var json = await GetStringWithRetryAsync("v3/fiat/map");
                var data = JsonConvert.DeserializeObject<CryptoCompareFiatResponse>(json);

                var result = new List<FiatCurrency>();
                foreach (var f in data.Data.Take(20))
                {
                    var rate = await GetExchangeRateAsync("USD", f.Symbol);
                    result.Add(new FiatCurrency
                    {
                        Code = f.Symbol,
                        Name = f.Name,
                        Symbol = f.Sign ?? f.Symbol,
                        RateToUSD = rate,
                        LastUpdated = DateTime.Now
                    });
                }

                return result;
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
                var json = await GetStringWithRetryAsync($"price?fsym={fromCurrency}&tsyms={toCurrency}");
                var data = JsonConvert.DeserializeObject<Dictionary<string, decimal>>(json);

                return data.TryGetValue(toCurrency, out var rate) ? rate : 1m;
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

        #region JSON Classes
        private class CryptoCompareResponse
        {
            public List<CryptoCompareData> Data { get; set; }
        }

        private class CryptoCompareData
        {
            public CoinInfo CoinInfo { get; set; }
            public RAW RAW { get; set; }
        }

        private class CoinInfo
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string FullName { get; set; }
        }

        private class RAW
        {
            public USD USD { get; set; }
        }

        private class USD
        {
            public decimal PRICE { get; set; }
            public decimal MKTCAP { get; set; }
            public decimal CHANGE24HOUR { get; set; }
            public decimal CHANGEPCT24HOUR { get; set; }
            public decimal VOLUME24HOURTO { get; set; }
            public long LASTUPDATE { get; set; }
        }

        private class CryptoComparePriceResponse
        {
            public Dictionary<string, Dictionary<string, USD>> RAW { get; set; }
        }

        private class CryptoCompareHistoryResponse
        {
            public HistoryData Data { get; set; }
        }

        private class HistoryData
        {
            public List<HistoryPoint> Data { get; set; }
        }

        private class HistoryPoint
        {
            public long time { get; set; }
            public decimal close { get; set; }
            public decimal volumeto { get; set; }
        }

        private class CryptoCompareFiatResponse
        {
            public List<FiatData> Data { get; set; }
        }

        private class FiatData
        {
            public string Symbol { get; set; }
            public string Name { get; set; }
            public string Sign { get; set; }
        }
        #endregion
    }
}