using CryptoTrackClient.Models;
using CryptoTrackClient.Services.Interfaces;
using Polly;
using Polly.Retry;
using System;
using System.Net.Http;
using System.Threading;
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

            // 🔧 CORRECTION : garantir que maxCount est au moins 1
            int permitsPerSecond = Math.Max(1, RequestLimitPerMinute / 60);
            _rateLimiter = new SemaphoreSlim(permitsPerSecond, permitsPerSecond);

            // Politique de réessai (identique)
            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"Retry {retryCount} for {ApiName}: {exception.Message}");
                    });
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
                // 🔧 CORRECTION : délai minimum de 1 ms
                int delayMs = Math.Max(1, 60000 / RequestLimitPerMinute);
                _ = Task.Delay(delayMs).ContinueWith(_ => _rateLimiter.Release());
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