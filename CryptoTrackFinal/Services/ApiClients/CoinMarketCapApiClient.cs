using CryptoTrackClient.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTrackClient.Services.ApiClients
{

    public class CoinMarketCapApiClient : BaseApiClient
    {
        private const string API_KEY = "YOUR_API_KEY"; // Get from coinmarketcap.com
        public override string ApiName => "CoinMarketCap";
        public override int Priority => 5;
        public override bool SupportsFiatCurrencies => true;
        public override int RequestLimitPerMinute => string.IsNullOrEmpty(API_KEY) ? 10 : 333;

        public CoinMarketCapApiClient()
            : base("https://pro-api.coinmarketcap.com/v1/", TimeSpan.FromSeconds(10))
        {
            if (!string.IsNullOrEmpty(API_KEY))
            {
                _httpClient.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", API_KEY);
            }
        }

        public override async Task<List<CryptoCurrency>> GetTopCryptocurrenciesAsync(int limit = 100)
        {
            try
            {
                var json = await GetStringWithRetryAsync($"cryptocurrency/listings/latest?limit={limit}");
                var data = JsonConvert.DeserializeObject<CMCResponse>(json);

                return data.data.Select(c => new CryptoCurrency
                {
                    Id = c.symbol.ToLower(),
                    Name = c.name,
                    Symbol = c.symbol,
                    CurrentPrice = c.quote.USD.price,
                    MarketCap = c.quote.USD.market_cap,
                    PriceChange24h = c.quote.USD.percent_change_24h / 100 * c.quote.USD.price,
                    PriceChangePercentage24h = c.quote.USD.percent_change_24h,
                    Volume24h = c.quote.USD.volume_24h,
                    CirculatingSupply = c.circulating_supply,
                    TotalSupply = c.total_supply,
                    Rank = c.cmc_rank,
                    LastUpdated = c.quote.USD.last_updated
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
                var json = await GetStringWithRetryAsync($"cryptocurrency/quotes/latest?symbol={id}");
                var data = JsonConvert.DeserializeObject<CMCSingleResponse>(json);

                var crypto = data.data.First().Value;
                return new CryptoCurrency
                {
                    Id = crypto.symbol.ToLower(),
                    Name = crypto.name,
                    Symbol = crypto.symbol,
                    CurrentPrice = crypto.quote.USD.price,
                    MarketCap = crypto.quote.USD.market_cap,
                    PriceChange24h = crypto.quote.USD.percent_change_24h / 100 * crypto.quote.USD.price,
                    PriceChangePercentage24h = crypto.quote.USD.percent_change_24h,
                    Volume24h = crypto.quote.USD.volume_24h,
                    CirculatingSupply = crypto.circulating_supply,
                    TotalSupply = crypto.total_supply,
                    Rank = crypto.cmc_rank,
                    LastUpdated = crypto.quote.USD.last_updated
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
                var json = await GetStringWithRetryAsync(
                    $"cryptocurrency/quotes/historical?symbol={cryptoId}&count={days}&interval=daily");

                var data = JsonConvert.DeserializeObject<CMCHistoryResponse>(json);

                return data.data.quotes.Select(q => new PriceHistory(
                    q.timestamp,
                    q.quote.USD.price,
                    q.quote.USD.volume_24h
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
                var json = await GetStringWithRetryAsync("fiat/map");
                var data = JsonConvert.DeserializeObject<CMCFiatResponse>(json);

                var result = new List<FiatCurrency>();
                foreach (var f in data.data.Take(20))
                {
                    var rate = await GetExchangeRateAsync("USD", f.symbol);
                    result.Add(new FiatCurrency
                    {
                        Code = f.symbol,
                        Name = f.name,
                        Symbol = f.sign ?? f.symbol,
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
                var json = await GetStringWithRetryAsync(
                    $"tools/price-conversion?amount=1&symbol={fromCurrency}&convert={toCurrency}");

                var data = JsonConvert.DeserializeObject<CMCConversionResponse>(json);

                return data.data.quote[toCurrency].price;
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
        private class CMCResponse
        {
            public List<CMCData> data { get; set; }
        }

        private class CMCSingleResponse
        {
            public Dictionary<string, CMCData> data { get; set; }
        }

        private class CMCHistoryResponse
        {
            public CMCData data { get; set; }
        }

        private class CMCFiatResponse
        {
            public List<CMCFiat> data { get; set; }
        }

        private class CMCConversionResponse
        {
            public CMCConversionData data { get; set; }
        }

        private class CMCData
        {
            public int id { get; set; }
            public string name { get; set; }
            public string symbol { get; set; }
            public int cmc_rank { get; set; }
            public decimal? circulating_supply { get; set; }
            public decimal? total_supply { get; set; }
            public CMCQuote quote { get; set; }
            public List<CMCHistoryQuote> quotes { get; set; }
        }

        private class CMCQuote
        {
            public CMCUSD USD { get; set; }
        }

        private class CMCUSD
        {
            public decimal price { get; set; }
            public decimal market_cap { get; set; }
            public decimal percent_change_24h { get; set; }
            public decimal volume_24h { get; set; }
            public DateTime last_updated { get; set; }
        }

        private class CMCHistoryQuote
        {
            public DateTime timestamp { get; set; }
            public CMCQuote quote { get; set; }
        }

        private class CMCFiat
        {
            public string symbol { get; set; }
            public string name { get; set; }
            public string sign { get; set; }
        }

        private class CMCConversionData
        {
            public Dictionary<string, CMCConversionQuote> quote { get; set; }
        }

        private class CMCConversionQuote
        {
            public decimal price { get; set; }
        }
        #endregion
    }
}