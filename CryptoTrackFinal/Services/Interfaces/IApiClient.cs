using CryptoTrackClient.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTrackClient.Services.Interfaces
{
    public interface IApiClient
    {
        string ApiName { get; }
        int Priority { get; }
        bool SupportsFiatCurrencies { get; }
        int RequestLimitPerMinute { get; }

        Task<List<CryptoCurrency>> GetTopCryptocurrenciesAsync(int limit = 100);
        Task<CryptoCurrency> GetCryptocurrencyByIdAsync(string id);
        Task<List<PriceHistory>> GetPriceHistoryAsync(string cryptoId, int days = 7);
        Task<List<FiatCurrency>> GetFiatCurrenciesAsync();
        Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency);
        Task<bool> TestConnectionAsync();
    }
}
