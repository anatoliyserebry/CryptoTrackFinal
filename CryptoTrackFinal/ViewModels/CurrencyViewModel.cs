using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoTrackClient.Models;
using CryptoTrackClient.Services.Interfaces;

namespace CryptoTrackClient.ViewModels
{

    public partial class CurrencyViewModel : ObservableObject
    {
        private readonly ICryptoService _cryptoService;

        [ObservableProperty]
        private ObservableCollection<FiatCurrency> _fiatCurrencies = new();

        [ObservableProperty]
        private FiatCurrency? _selectedFromCurrency;

        [ObservableProperty]
        private FiatCurrency? _selectedToCurrency;

        [ObservableProperty]
        private decimal _amountToConvert = 100;

        [ObservableProperty]
        private decimal _convertedAmount;

        [ObservableProperty]
        private decimal _exchangeRate = 1;

        [ObservableProperty]
        private bool _isConverting;

        [ObservableProperty]
        private ObservableCollection<ExchangeRateHistory> _rateHistory = new();

        public ICommand LoadCurrenciesCommand { get; }
        public ICommand ConvertCurrencyCommand { get; }
        public ICommand SwapCurrenciesCommand { get; }
        public ICommand UpdateRateCommand { get; }

        public CurrencyViewModel(ICryptoService cryptoService)
        {
            _cryptoService = cryptoService;

            LoadCurrenciesCommand = new AsyncRelayCommand(LoadCurrenciesAsync);
            ConvertCurrencyCommand = new AsyncRelayCommand(ConvertCurrencyAsync);
            SwapCurrenciesCommand = new AsyncRelayCommand(SwapCurrenciesAsync);
            UpdateRateCommand = new AsyncRelayCommand(UpdateExchangeRateAsync);

            _ = LoadCurrenciesAsync();
        }

        private async Task LoadCurrenciesAsync()
        {
            var currencies = await _cryptoService.GetFiatCurrenciesAsync();
            FiatCurrencies = new ObservableCollection<FiatCurrency>(currencies);

            SelectedFromCurrency = FiatCurrencies.FirstOrDefault(c => c.Code == "USD");
            SelectedToCurrency = FiatCurrencies.FirstOrDefault(c => c.Code == "EUR");

            if (SelectedFromCurrency != null && SelectedToCurrency != null)
            {
                await UpdateExchangeRateAsync();
            }
        }

        private async Task ConvertCurrencyAsync()
        {
            if (SelectedFromCurrency == null || SelectedToCurrency == null || AmountToConvert <= 0)
                return;

            IsConverting = true;
            try
            {
                await UpdateExchangeRateAsync();
                ConvertedAmount = AmountToConvert * ExchangeRate;
            }
            finally
            {
                IsConverting = false;
            }
        }

        private async Task SwapCurrenciesAsync()
        {
            if (SelectedFromCurrency == null || SelectedToCurrency == null)
                return;

            var temp = SelectedFromCurrency;
            SelectedFromCurrency = SelectedToCurrency;
            SelectedToCurrency = temp;

            await ConvertCurrencyAsync();
        }

        private async Task UpdateExchangeRateAsync()
        {
            if (SelectedFromCurrency == null || SelectedToCurrency == null)
                return;

            try
            {
                ExchangeRate = await _cryptoService.ConvertCurrencyAsync(
                    1,
                    SelectedFromCurrency.Code,
                    SelectedToCurrency.Code);
            }
            catch
            {
                ExchangeRate = 1;
            }
        }
    }

    public class ExchangeRateHistory
    {
        public DateTime Date { get; set; }
        public decimal Rate { get; set; }
    }
}
