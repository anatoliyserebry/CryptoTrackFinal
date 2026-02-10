using CryptoTrackClient.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTrackClient.Services.Interfaces
{
    public interface ICryptoService
    {
        event Action DataUpdated;
        event Action PortfolioUpdated;

        string ActiveApiName { get; }
        List<string> AvailableApis { get; }

        // Cryptocurrency methods
        Task<List<CryptoCurrency>> GetCryptoCurrenciesAsync();
        Task<CryptoCurrency> GetCryptoCurrencyByIdAsync(string id);
        Task<List<CryptoCurrency>> GetFavoriteCurrenciesAsync();
        Task ToggleFavoriteAsync(string cryptoId);
        Task<List<PriceHistory>> GetPriceHistoryAsync(string cryptoId, int days = 7);

        // Fiat currency methods
        Task<List<FiatCurrency>> GetFiatCurrenciesAsync();
        Task<decimal> ConvertCurrencyAsync(decimal amount, string fromCurrency, string toCurrency);

        // Portfolio methods
        Task AddTransactionAsync(Transaction transaction);
        Task UpdateTransactionAsync(Transaction transaction);
        Task DeleteTransactionAsync(Guid transactionId);
        Task<List<Transaction>> GetTransactionsAsync();
        Task<PortfolioSummary> GetPortfolioSummaryAsync();
        Task<List<PortfolioAsset>> GetPortfolioAssetsAsync();
        Task<decimal> GetPortfolioValueAsync();

        // Utility methods
        Task<bool> TestApiConnectionAsync();
        Task SwitchToApiAsync(string apiName);
        Task RefreshDataAsync();
    }
}
