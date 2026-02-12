using System;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CryptoTrackClient.Services;
using CryptoTrackClient.Services.Interfaces;
using CryptoTrackClient.Services.ApiClients;
using CryptoTrackClient.ViewModels;

namespace CryptoTrackClient
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }
        public static IConfiguration Configuration { get; private set; }

        public App()
        {
            // Gestion globale des exceptions
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);

                // Configuration
                var builder = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                Configuration = builder.Build();

                // Services
                var services = new ServiceCollection();
                ConfigureServices(services);
                ServiceProvider = services.BuildServiceProvider();

                // Fenêtre principale
                var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
                mainWindow.DataContext = ServiceProvider.GetRequiredService<MainViewModel>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur au démarrage : {ex.Message}\n{ex.StackTrace}",
                                "Erreur fatale", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
            });

            // Configuration
            services.Configure<CryptoServiceOptions>(Configuration.GetSection("CryptoService"));

            // API Clients
            services.AddSingleton<IApiClient, CoinGeckoApiClient>();
            services.AddSingleton<IApiClient, CoinCapApiClient>();
            services.AddSingleton<IApiClient, BinanceApiClient>();
            services.AddSingleton<IApiClient, CryptoCompareApiClient>();
            services.AddSingleton<IApiClient, CoinMarketCapApiClient>();
            services.AddSingleton<IApiClient, KucoinApiClient>();
            services.AddSingleton<IApiClient, CoinStatsApiClient>();

            // Services métier
            services.AddSingleton<ICryptoService, CryptoService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<PortfolioViewModel>();
            services.AddSingleton<CurrencyViewModel>();

            // Fenêtre principale
            services.AddSingleton<MainWindow>();
        }

        // Gestionnaires d'exceptions
        private void App_DispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Erreur UI : {e.Exception.Message}", "Erreur",
                            MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show($"Erreur non gérée : {ex?.Message}", "Erreur",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void TaskScheduler_UnobservedTaskException(object sender,
            System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            MessageBox.Show($"Erreur tâche : {e.Exception.Message}", "Erreur",
                            MessageBoxButton.OK, MessageBoxImage.Error);
            e.SetObserved();
        }
    }
}