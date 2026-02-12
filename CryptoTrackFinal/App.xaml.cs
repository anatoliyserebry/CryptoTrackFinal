using CryptoTrackClient.Services.ApiClients;
using CryptoTrackClient.Services.Interfaces;
using CryptoTrackClient.Services;
using CryptoTrackClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using System.Configuration;
using System.Data;
using System.Windows;

namespace CryptoTrackClient
{

    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }
        public static IConfiguration Configuration { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Build configuration
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            Configuration = builder.Build();

            // Configure services
            ServiceProvider = ConfigureServices();

            // Create main window
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = ServiceProvider.GetRequiredService<MainViewModel>();
            mainWindow.Show();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
            });

            // Add configuration
            services.Configure<CryptoServiceOptions>(Configuration.GetSection("CryptoService"));

            // Register API clients
            services.AddSingleton<IApiClient, CoinGeckoApiClient>();
            services.AddSingleton<IApiClient, CoinCapApiClient>();
            services.AddSingleton<IApiClient, BinanceApiClient>();
            services.AddSingleton<IApiClient, CryptoCompareApiClient>();
            services.AddSingleton<IApiClient, CoinMarketCapApiClient>();
            services.AddSingleton<IApiClient, KucoinApiClient>();
            services.AddSingleton<IApiClient, CoinStatsApiClient>();

            // Register services
            services.AddSingleton<ICryptoService, CryptoService>();

            // Register view models
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<PortfolioViewModel>();
            services.AddSingleton<CurrencyViewModel>();

            // Register views
            services.AddSingleton<MainWindow>();

            return services.BuildServiceProvider();
        }

        private void Application_DispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"An error occurred: {e.Exception.Message}\n\n" +
                "The application will continue, but some features may be unavailable.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
        }
    }
}


