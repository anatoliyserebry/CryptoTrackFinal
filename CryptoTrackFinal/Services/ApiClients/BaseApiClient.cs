using CryptoTrackClient.Models;
using CryptoTrackClient.Services.Interfaces;
using System;
using System.Threading;
using CryptoTrackClient.Services.Interfaces
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTrackClient.Services.ApiClients
{
    public abstract class BaseApiClient : IApiClient, IDisposable
    {
        protected readonly HttpClient _httpClient;
        protected readonly AsyncRetryPolicy _retryPolicy;
        protected readonly SemaphoreSlim _rateLimiter;

        public abstract string ApiName { get; }
        public abstract int Priority { get; }
        public abstract bool SupportsFiatCurrencies { get; }
        public abstract int RequestLimitPerMinute { get; }

        protected BaseApiClient(string baseUrl, TimeSpan timeout)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = timeout
            };

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "CryptoTrackClient/2.0");

            
            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        Console.WriteLine($"Retry {retryCount} for {ApiName}: {exception.Message}");
                    });

            
            _rateLimiter = new SemaphoreSlim(RequestLimitPerMinute / 60, RequestLimitPerMinute / 60);
        }

        protected async Task<T> ExecuteWithRetryAndRateLimitAsync<T>(Func<Task<T>> action)
        {
            await _rateLimiter.WaitAsync();
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    return await action();
                });
            }
            finally
            {
                // Release after delay to respect rate limit
                _ = Task.Delay(60000 / RequestLimitPerMinute).ContinueWith(_ => _rateLimiter.Release());
            }
        }

        protected async Task<string> GetStringWithRetryAsync(string requestUri)
        {
            return await ExecuteWithRetryAndRateLimitAsync(async () =>
            {
                var response = await _httpClient.GetAsync(requestUri);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            });
        }

        public abstract Task<List<CryptoCurrency>> GetTopCryptocurrenciesAsync(int limit = 100);
        public abstract Task<CryptoCurrency> GetCryptocurrencyByIdAsync(string id);
        public abstract Task<List<PriceHistory>> GetPriceHistoryAsync(string cryptoId, int days = 7);
        public abstract Task<List<FiatCurrency>> GetFiatCurrenciesAsync();
        public abstract Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency);
        public abstract Task<bool> TestConnectionAsync();

        public virtual void Dispose()
        {
            _httpClient?.Dispose();
            _rateLimiter?.Dispose();
        }
    }
}
