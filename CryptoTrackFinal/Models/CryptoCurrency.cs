using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTrackClient.Models
{
    public class CryptoCurrency : INotifyPropertyChanged
    {
        private bool _isFavorite;

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public decimal MarketCap { get; set; }
        public decimal PriceChange24h { get; set; }
        public decimal PriceChangePercentage24h { get; set; }

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged(nameof(IsFavorite));
                }
            }
        }

        public DateTime LastUpdated { get; set; }
        public decimal? Volume24h { get; set; }
        public decimal? CirculatingSupply { get; set; }
        public decimal? TotalSupply { get; set; }
        public int? Rank { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PriceHistory
    {
        public DateTime Date { get; set; }
        public decimal Price { get; set; }
        public decimal Volume { get; set; }

        public PriceHistory(DateTime date, decimal price, decimal volume = 0)
        {
            Date = date;
            Price = price;
            Volume = volume;
        }
    }

    public class FiatCurrency : INotifyPropertyChanged
    {
        private string _code = string.Empty;
        private string _name = string.Empty;
        private string _symbol = string.Empty;
        private decimal _rateToUSD;

        public string Code
        {
            get => _code;
            set
            {
                if (_code != value)
                {
                    _code = value;
                    OnPropertyChanged(nameof(Code));
                }
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string Symbol
        {
            get => _symbol;
            set
            {
                if (_symbol != value)
                {
                    _symbol = value;
                    OnPropertyChanged(nameof(Symbol));
                }
            }
        }

        public decimal RateToUSD
        {
            get => _rateToUSD;
            set
            {
                if (_rateToUSD != value)
                {
                    _rateToUSD = value;
                    OnPropertyChanged(nameof(RateToUSD));
                }
            }
        }

        public DateTime LastUpdated { get; set; }

        public decimal ConvertFromUSD(decimal usdAmount) => usdAmount / RateToUSD;
        public decimal ConvertToUSD(decimal amount) => amount * RateToUSD;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum TransactionType
    {
        Buy,
        Sell,
        Transfer
    }

    public class Transaction : INotifyPropertyChanged
    {
        private decimal _currentPrice;
        private Guid _id = Guid.NewGuid();

        public Guid Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

        public string CryptoId { get; set; } = string.Empty;
        public string CryptoSymbol { get; set; } = string.Empty;
        public string CryptoName { get; set; } = string.Empty;
        public TransactionType Type { get; set; }
        public decimal Amount { get; set; }
        public decimal PricePerUnit { get; set; }
        public decimal Fee { get; set; }
        public decimal TotalCost => Amount * PricePerUnit + Fee;
        public string Exchange { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; }
        public string Notes { get; set; } = string.Empty;

        public decimal CurrentPrice
        {
            get => _currentPrice;
            set
            {
                if (_currentPrice != value)
                {
                    _currentPrice = value;
                    OnPropertyChanged(nameof(CurrentPrice));
                    OnPropertyChanged(nameof(CurrentValue));
                    OnPropertyChanged(nameof(ProfitLoss));
                    OnPropertyChanged(nameof(ProfitLossPercentage));
                }
            }
        }

        public decimal CurrentValue => Amount * CurrentPrice;
        public decimal ProfitLoss => CurrentValue - TotalCost;
        public decimal ProfitLossPercentage => TotalCost > 0 ? (ProfitLoss / TotalCost) * 100 : 0;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PortfolioAsset : INotifyPropertyChanged
    {
        private decimal _currentPrice;
        private decimal _amount;
        private string _cryptoId = string.Empty;
        private string _symbol = string.Empty;
        private string _name = string.Empty;

        public string CryptoId
        {
            get => _cryptoId;
            set
            {
                if (_cryptoId != value)
                {
                    _cryptoId = value;
                    OnPropertyChanged(nameof(CryptoId));
                }
            }
        }

        public string Symbol
        {
            get => _symbol;
            set
            {
                if (_symbol != value)
                {
                    _symbol = value;
                    OnPropertyChanged(nameof(Symbol));
                }
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public decimal Amount
        {
            get => _amount;
            set
            {
                if (_amount != value)
                {
                    _amount = value;
                    OnPropertyChanged(nameof(Amount));
                    OnPropertyChanged(nameof(CurrentValue));
                    OnPropertyChanged(nameof(ProfitLoss));
                    OnPropertyChanged(nameof(ProfitLossPercentage));
                }
            }
        }

        public decimal AverageBuyPrice { get; set; }
        public decimal TotalInvested { get; set; }

        public decimal CurrentPrice
        {
            get => _currentPrice;
            set
            {
                if (_currentPrice != value)
                {
                    _currentPrice = value;
                    OnPropertyChanged(nameof(CurrentPrice));
                    OnPropertyChanged(nameof(CurrentValue));
                    OnPropertyChanged(nameof(ProfitLoss));
                    OnPropertyChanged(nameof(ProfitLossPercentage));
                }
            }
        }

        public decimal CurrentValue => Amount * CurrentPrice;
        public decimal ProfitLoss => CurrentValue - TotalInvested;
        public decimal ProfitLossPercentage => TotalInvested > 0 ? (ProfitLoss / TotalInvested) * 100 : 0;

        public List<Transaction> Transactions { get; set; } = new();
        public decimal PercentageOfPortfolio { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PortfolioSummary : INotifyPropertyChanged
    {
        private decimal _totalInvested;
        private decimal _currentValue;

        public decimal TotalInvested
        {
            get => _totalInvested;
            set
            {
                if (_totalInvested != value)
                {
                    _totalInvested = value;
                    OnPropertyChanged(nameof(TotalInvested));
                    OnPropertyChanged(nameof(TotalProfitLoss));
                    OnPropertyChanged(nameof(TotalProfitLossPercentage));
                }
            }
        }

        public decimal CurrentValue
        {
            get => _currentValue;
            set
            {
                if (_currentValue != value)
                {
                    _currentValue = value;
                    OnPropertyChanged(nameof(CurrentValue));
                    OnPropertyChanged(nameof(TotalProfitLoss));
                    OnPropertyChanged(nameof(TotalProfitLossPercentage));
                }
            }
        }

        public decimal TotalProfitLoss => CurrentValue - TotalInvested;
        public decimal TotalProfitLossPercentage => TotalInvested > 0 ? (TotalProfitLoss / TotalInvested) * 100 : 0;

        public DateTime LastUpdated { get; set; }
        public List<PortfolioAsset> Assets { get; set; } = new();

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}