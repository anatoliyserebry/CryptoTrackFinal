using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CryptoTrackClient.Models;
using CryptoTrackClient.Services.Interfaces;

namespace CryptoTrackClient.ViewModels
{
    public partial class PortfolioViewModel : ObservableObject
    {
        private readonly ICryptoService _cryptoService;

        [ObservableProperty]
        private ObservableCollection<Transaction> _transactions = new();

        [ObservableProperty]
        private ObservableCollection<PortfolioAsset> _assets = new();

        [ObservableProperty]
        private PortfolioSummary _summary = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private Transaction? _selectedTransaction;

        [ObservableProperty]
        private PortfolioAsset? _selectedAsset;

        [ObservableProperty]
        private string _newTransactionCryptoId = string.Empty;

        [ObservableProperty]
        private TransactionType _newTransactionType = TransactionType.Buy;

        [ObservableProperty]
        private decimal _newTransactionAmount;

        [ObservableProperty]
        private decimal _newTransactionPrice;

        [ObservableProperty]
        private decimal _newTransactionFee;

        [ObservableProperty]
        private string _newTransactionExchange = "Binance";

        [ObservableProperty]
        private string _newTransactionNotes = string.Empty;

        [ObservableProperty]
        private DateTime _newTransactionDate = DateTime.Now;

        public ObservableCollection<string> Exchanges { get; } = new()
        {
            "Binance",
            "Coinbase",
            "Kraken",
            "KuCoin",
            "Huobi",
            "OKX",
            "Bybit",
            "Bitfinex",
            "Gate.io",
            "Other"
        };

        public ICommand LoadPortfolioCommand { get; }
        public ICommand AddTransactionCommand { get; }
        public ICommand DeleteTransactionCommand { get; }
        public ICommand RefreshPortfolioCommand { get; }
        public ICommand ShowAssetDetailsCommand { get; }
        public ICommand ClearFormCommand { get; }

        public PortfolioViewModel(ICryptoService cryptoService)
        {
            _cryptoService = cryptoService;

            LoadPortfolioCommand = new AsyncRelayCommand(LoadPortfolioAsync);
            AddTransactionCommand = new AsyncRelayCommand(AddTransactionAsync);
            DeleteTransactionCommand = new RelayCommand<Guid>(DeleteTransactionAsync);
            RefreshPortfolioCommand = new AsyncRelayCommand(RefreshPortfolioAsync);
            ShowAssetDetailsCommand = new RelayCommand<PortfolioAsset>(ShowAssetDetails);
            ClearFormCommand = new RelayCommand(ClearForm);

            _cryptoService.PortfolioUpdated += async () => await OnPortfolioUpdated();

            _ = LoadPortfolioAsync();
        }

        private async Task LoadPortfolioAsync()
        {
            IsLoading = true;
            try
            {
                var transactions = await _cryptoService.GetTransactionsAsync();
                Transactions = new ObservableCollection<Transaction>(transactions);

                var assets = await _cryptoService.GetPortfolioAssetsAsync();
                Assets = new ObservableCollection<PortfolioAsset>(assets);

                Summary = await _cryptoService.GetPortfolioSummaryAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AddTransactionAsync()
        {
            if (string.IsNullOrWhiteSpace(NewTransactionCryptoId) ||
                NewTransactionAmount <= 0 ||
                NewTransactionPrice <= 0)
                return;

            try
            {
                // Get cryptocurrency info
                var crypto = await _cryptoService.GetCryptoCurrencyByIdAsync(NewTransactionCryptoId.ToLower());

                var transaction = new Transaction
                {
                    CryptoId = crypto.Id,
                    CryptoSymbol = crypto.Symbol,
                    CryptoName = crypto.Name,
                    Type = NewTransactionType,
                    Amount = NewTransactionAmount,
                    PricePerUnit = NewTransactionPrice,
                    Fee = NewTransactionFee,
                    Exchange = NewTransactionExchange,
                    TransactionDate = NewTransactionDate,
                    Notes = NewTransactionNotes,
                    CurrentPrice = crypto.CurrentPrice
                };

                await _cryptoService.AddTransactionAsync(transaction);

                // Clear form
                ClearForm();
            }
            catch (Exception ex)
            {
                // TODO: Show error message
                Console.WriteLine($"Error adding transaction: {ex.Message}");
            }
        }

        private async void DeleteTransactionAsync(Guid transactionId)
        {
            if (transactionId == Guid.Empty) return;

            await _cryptoService.DeleteTransactionAsync(transactionId);
        }

        private async Task RefreshPortfolioAsync()
        {
            await LoadPortfolioAsync();
        }

        private void ShowAssetDetails(PortfolioAsset? asset)
        {
            SelectedAsset = asset;
            // TODO: Show asset details dialog
        }

        private void ClearForm()
        {
            NewTransactionCryptoId = string.Empty;
            NewTransactionAmount = 0;
            NewTransactionPrice = 0;
            NewTransactionFee = 0;
            NewTransactionExchange = "Binance";
            NewTransactionNotes = string.Empty;
            NewTransactionDate = DateTime.Now;
        }

        private async Task OnPortfolioUpdated()
        {
            await LoadPortfolioAsync();
        }
    }
}
