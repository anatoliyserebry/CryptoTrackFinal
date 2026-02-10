using CryptoTrackClient.Models;
using CryptoTrackClient.Services.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CryptoTrackClient.Models;
using CryptoTrackClient.Services.Interfaces;

namespace CryptoTrackClient.Services.ApiClients
{

    public class CoinGeckoApiClient : BaseApiClient
    {
        public override string ApiName => "CoinGecko";
        public override int Priority => 1;
        public override bool SupportsFiatCurrencies => true;
        public override int RequestLimitPerMinute => 50;

        public CoinGeckoApiClient()
            : base("https://api.coingecko.com/api/v3/", TimeSpan.FromSeconds(15))
        {
        }

        public override async Task<List<CryptoCurrency>> GetTopCryptocurrenciesAsync(int limit = 100)
        {
            try
            {
                var json = await GetStringWithRetryAsync(
                    $"coins/markets?vs_currency=usd&order=market_cap_desc&per_page={limit}&page=1&sparkline=false");

                var data = JsonConvert.DeserializeObject<List<CoinGeckoMarketData>>(json);

                return data?.Select(c => new CryptoCurrency
                {
                    Id = c.id,
                    Name = c.name,
                    Symbol = c.symbol.ToUpper(),
                    CurrentPrice = c.current_price,
                    MarketCap = c.market_cap,
                    PriceChange24h = c.price_change_24h,
                    PriceChangePercentage24h = c.price_change_percentage_24h,
                    Volume24h = c.total_volume,
                    CirculatingSupply = c.circulating_supply,
                    LastUpdated = DateTime.Parse(c.last_updated),
                    Rank = c.market_cap_rank
                }).ToList() ?? new List<CryptoCurrency>();
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
                var data = JsonConvert.DeserializeObject<CoinGeckoCoinDetail>(json);

                return new CryptoCurrency
                {
                    Id = data.id,
                    Name = data.name,
                    Symbol = data.symbol.ToUpper(),
                    CurrentPrice = data.market_data.current_price.usd,
                    MarketCap = data.market_data.market_cap.usd,
                    PriceChange24h = data.market_data.price_change_24h,
                    PriceChangePercentage24h = data.market_data.price_change_percentage_24h,
                    Volume24h = data.market_data.total_volume.usd,
                    CirculatingSupply = data.market_data.circulating_supply,
                    TotalSupply = data.market_data.total_supply,
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
                var json = await GetStringWithRetryAsync(
                    $"coins/{cryptoId}/market_chart?vs_currency=usd&days={days}");

                var data = JsonConvert.DeserializeObject<CoinGeckoMarketChart>(json);

                return data.prices.Select(p => new PriceHistory(
                    DateTimeOffset.FromUnixTimeMilliseconds((long)p[0]).DateTime,
                    (decimal)p[1],
                    data.total_volumes?.Count > 0 ? (decimal)data.total_volumes[0][1] : 0
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
                var json = await GetStringWithRetryAsync("simple/supported_vs_currencies");
                var currencies = JsonConvert.DeserializeObject<List<string>>(json);

                var fiatCurrencies = new List<string> { "usd", "eur", "gbp", "jpy", "rub", "cny", "inr", "aud", "cad", "chf" };
                var result = new List<FiatCurrency>();

                foreach (var currency in currencies.Where(c => fiatCurrencies.Contains(c)))
                {
                    var rate = await GetExchangeRateAsync("usd", currency);
                    result.Add(new FiatCurrency
                    {
                        Code = currency.ToUpper(),
                        Name = GetCurrencyName(currency),
                        Symbol = GetCurrencySymbol(currency),
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
                if (fromCurrency.ToLower() == toCurrency.ToLower())
                    return 1m;

                var json = await GetStringWithRetryAsync(
                    $"simple/price?ids=bitcoin&vs_currencies={toCurrency}");

                var data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, decimal>>>(json);

                if (data.TryGetValue("bitcoin", out var rates) && rates.TryGetValue(toCurrency.ToLower(), out var rate))
                {
                    // Convert via BTC
                    var btcToFrom = await GetBtcRate(fromCurrency);
                    var btcToTo = await GetBtcRate(toCurrency);

                    return btcToTo / btcToFrom;
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
                var response = await _httpClient.GetAsync("ping");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<decimal> GetBtcRate(string currency)
        {
            try
            {
                var json = await GetStringWithRetryAsync($"simple/price?ids=bitcoin&vs_currencies={currency}");
                var data = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, decimal>>>(json);

                return data?["bitcoin"]?[currency.ToLower()] ?? 1m;
            }
            catch
            {
                return 1m;
            }
        }

        private string GetCurrencyName(string code)
        {
            return code.ToLower() switch
            {
                "usd" => "US Dollar",
                "eur" => "Euro",
                "gbp" => "British Pound",
                "jpy" => "Japanese Yen",
                "rub" => "Russian Ruble",
                "cny" => "Chinese Yuan",
                "inr" => "Indian Rupee",
                "aud" => "Australian Dollar",
                "cad" => "Canadian Dollar",
                "chf" => "Swiss Franc",
                _ => code.ToUpper()
            };
        }

        private string GetCurrencySymbol(string code)
        {
            return code.ToUpper() switch
            {
                "USD" => "$",
                "EUR" => "€",
                "GBP" => "£",
                "JPY" => "¥",
                "RUB" => "₽",
                "CNY" => "¥",
                "INR" => "₹",
                "AUD" => "A$",
                "CAD" => "C$",
                "CHF" => "CHF",
                _ => "$"
            };
        }

        #region JSON Classes
        private class CoinGeckoMarketData
        {
            public string id { get; set; }
            public string symbol { get; set; }
            public string name { get; set; }
            public decimal current_price { get; set; }
            public decimal market_cap { get; set; }
            public int market_cap_rank { get; set; }
            public decimal price_change_24h { get; set; }
            public decimal price_change_percentage_24h { get; set; }
            public decimal total_volume { get; set; }
            public decimal? circulating_supply { get; set; }
            public string last_updated { get; set; }
        }

        private class CoinGeckoCoinDetail
        {
            public string id { get; set; }
            public string symbol { get; set; }
            public string name { get; set; }
            public CoinGeckoMarketDataDetail market_data { get; set; }
        }

        private class CoinGeckoMarketDataDetail
        {
            public Dictionary<string, decimal> current_price { get; set; }
            public Dictionary<string, decimal> market_cap { get; set; }
            public decimal price_change_24h { get; set; }
            public decimal price_change_percentage_24h { get; set; }
            public Dictionary<string, decimal> total_volume { get; set; }
            public decimal? circulating_supply { get; set; }
            public decimal? total_supply { get; set; }
        }

        private class CoinGeckoMarketChart
        {
            public List<List<decimal>> prices { get; set; }
            public List<List<decimal>> total_volumes { get; set; }
        }
        #endregion
    }

    public class ApiException : Exception
    {
        public string ApiName { get; }

        public ApiException(string apiName, string message, Exception innerException)
            : base($"{apiName}: {message}", innerException)
        {
            ApiName = apiName;
        }
    }
}