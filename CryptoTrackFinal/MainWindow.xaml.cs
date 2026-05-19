using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CryptoTrackClient.ViewModels;

namespace CryptoTrackClient
{
    public partial class MainWindow : Window
    {
        private INotifyPropertyChanged? _viewModelNotifications;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += (_, _) => ApplyResponsiveLayout();
            SizeChanged += (_, _) => ApplyResponsiveLayout();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModelNotifications != null)
            {
                _viewModelNotifications.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModelNotifications = e.NewValue as INotifyPropertyChanged;

            if (_viewModelNotifications != null)
            {
                _viewModelNotifications.PropertyChanged += OnViewModelPropertyChanged;
            }

            ApplyResponsiveLayout();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MainViewModel.IsMarketDataExpanded)
                or nameof(MainViewModel.IsChartExpanded)
                or nameof(MainViewModel.IsPortfolioDataExpanded))
            {
                if (Dispatcher.CheckAccess())
                {
                    ApplyResponsiveLayout();
                    return;
                }

                Dispatcher.Invoke(ApplyResponsiveLayout);
            }
        }

        private void ApplyResponsiveLayout()
        {
            var width = ActualWidth > 0 ? ActualWidth : Width;
            var height = ActualHeight > 0 ? ActualHeight : Height;
            var compact = width < 1180 || height < 760;
            var narrow = width < 1020;
            var viewModel = DataContext as MainViewModel;
            var marketExpanded = viewModel?.IsMarketDataExpanded == true;
            var chartExpanded = viewModel?.IsChartExpanded == true;
            var portfolioExpanded = viewModel?.IsPortfolioDataExpanded == true;
            var expanded = marketExpanded || chartExpanded || portfolioExpanded;

            ShellFrame.Margin = expanded
                ? new Thickness(10)
                : compact
                    ? new Thickness(12)
                    : new Thickness(20);
            ShellFrame.Padding = expanded
                ? new Thickness(14)
                : compact
                    ? new Thickness(16)
                    : new Thickness(24);

            HeroPanel.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
            FooterBar.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;

            if (!expanded)
            {
                HeroPanel.Padding = compact ? new Thickness(18) : new Thickness(28);
                HeroPanel.Margin = compact ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 0, 18);
                HeroSubtitle.Width = compact ? double.NaN : 560;
                HeroSubtitle.MaxWidth = compact ? 520 : double.PositiveInfinity;
                HeroStatus.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
                HeroMetricsPanel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            }

            ApplyMarketLayout(narrow, marketExpanded);
            ApplyChartLayout(narrow, chartExpanded);
            ApplyPortfolioLayout(narrow, portfolioExpanded);
        }

        private void ApplyMarketLayout(bool narrow, bool marketExpanded)
        {
            MarketSummaryGrid.Visibility = marketExpanded ? Visibility.Collapsed : Visibility.Visible;

            if (marketExpanded)
            {
                return;
            }

            if (narrow)
            {
                Grid.SetRow(MarketFiltersCard, 0);
                Grid.SetColumn(MarketFiltersCard, 0);
                Grid.SetColumnSpan(MarketFiltersCard, 3);
                MarketFiltersCard.Margin = new Thickness(0, 0, 0, 12);

                Grid.SetRow(MarketDisplayedCard, 1);
                Grid.SetColumn(MarketDisplayedCard, 0);
                Grid.SetColumnSpan(MarketDisplayedCard, 2);
                MarketDisplayedCard.Margin = new Thickness(0, 0, 12, 0);

                Grid.SetRow(MarketFavoritesCard, 1);
                Grid.SetColumn(MarketFavoritesCard, 2);
                Grid.SetColumnSpan(MarketFavoritesCard, 1);
                MarketFavoritesCard.Margin = new Thickness(0);

                Grid.SetRow(MarketSearchPanel, 0);
                Grid.SetColumn(MarketSearchPanel, 0);
                Grid.SetColumnSpan(MarketSearchPanel, 3);
                MarketSearchPanel.Margin = new Thickness(0, 0, 0, 12);

                Grid.SetRow(MarketSourcePanel, 1);
                Grid.SetColumn(MarketSourcePanel, 0);
                Grid.SetColumnSpan(MarketSourcePanel, 2);
                MarketSourcePanel.Margin = new Thickness(0, 0, 12, 0);

                Grid.SetRow(MarketApplyPanel, 1);
                Grid.SetColumn(MarketApplyPanel, 2);
                Grid.SetColumnSpan(MarketApplyPanel, 1);
                MarketApplyPanel.Margin = new Thickness(0);
                return;
            }

            Grid.SetRow(MarketFiltersCard, 0);
            Grid.SetColumn(MarketFiltersCard, 0);
            Grid.SetColumnSpan(MarketFiltersCard, 1);
            MarketFiltersCard.Margin = new Thickness(0, 0, 18, 0);

            Grid.SetRow(MarketDisplayedCard, 0);
            Grid.SetColumn(MarketDisplayedCard, 1);
            Grid.SetColumnSpan(MarketDisplayedCard, 1);
            MarketDisplayedCard.Margin = new Thickness(0, 0, 18, 0);

            Grid.SetRow(MarketFavoritesCard, 0);
            Grid.SetColumn(MarketFavoritesCard, 2);
            Grid.SetColumnSpan(MarketFavoritesCard, 1);
            MarketFavoritesCard.Margin = new Thickness(0);

            Grid.SetRow(MarketSearchPanel, 0);
            Grid.SetColumn(MarketSearchPanel, 0);
            Grid.SetColumnSpan(MarketSearchPanel, 1);
            MarketSearchPanel.Margin = new Thickness(0, 0, 18, 0);

            Grid.SetRow(MarketSourcePanel, 0);
            Grid.SetColumn(MarketSourcePanel, 1);
            Grid.SetColumnSpan(MarketSourcePanel, 1);
            MarketSourcePanel.Margin = new Thickness(0, 0, 18, 0);

            Grid.SetRow(MarketApplyPanel, 0);
            Grid.SetColumn(MarketApplyPanel, 2);
            Grid.SetColumnSpan(MarketApplyPanel, 1);
            MarketApplyPanel.Margin = new Thickness(0);
        }

        private void ApplyChartLayout(bool narrow, bool chartExpanded)
        {
            Grid.SetRow(ChartPlotCard, 0);
            Grid.SetColumn(ChartPlotCard, 0);
            ChartHistoryCard.Visibility = chartExpanded ? Visibility.Collapsed : Visibility.Visible;

            if (chartExpanded)
            {
                Grid.SetColumnSpan(ChartPlotCard, 2);
                ChartPlotCard.Margin = new Thickness(0);
                ChartContentGrid.RowDefinitions[1].Height = GridLength.Auto;
                return;
            }

            if (narrow)
            {
                Grid.SetColumnSpan(ChartPlotCard, 2);
                ChartPlotCard.Margin = new Thickness(0, 0, 0, 12);

                Grid.SetRow(ChartHistoryCard, 1);
                Grid.SetColumn(ChartHistoryCard, 0);
                Grid.SetColumnSpan(ChartHistoryCard, 2);
                ChartHistoryCard.Margin = new Thickness(0);
                ChartHistoryCard.MaxHeight = 220;
                ChartContentGrid.RowDefinitions[1].Height = new GridLength(220);
                return;
            }

            Grid.SetColumnSpan(ChartPlotCard, 1);
            ChartPlotCard.Margin = new Thickness(0, 0, 18, 0);

            Grid.SetRow(ChartHistoryCard, 0);
            Grid.SetColumn(ChartHistoryCard, 1);
            Grid.SetColumnSpan(ChartHistoryCard, 1);
            ChartHistoryCard.Margin = new Thickness(0);
            ChartHistoryCard.MaxHeight = double.PositiveInfinity;
            ChartContentGrid.RowDefinitions[1].Height = GridLength.Auto;
        }

        private void ApplyPortfolioLayout(bool narrow, bool portfolioExpanded)
        {
            PortfolioSummaryGrid.Visibility = portfolioExpanded ? Visibility.Collapsed : Visibility.Visible;
            PortfolioTransactionCard.Visibility = portfolioExpanded ? Visibility.Collapsed : Visibility.Visible;
            PortfolioActivityCard.Visibility = portfolioExpanded ? Visibility.Collapsed : Visibility.Visible;

            if (portfolioExpanded)
            {
                Grid.SetRow(PortfolioHoldingsCard, 0);
                Grid.SetColumn(PortfolioHoldingsCard, 0);
                Grid.SetColumnSpan(PortfolioHoldingsCard, 2);
                PortfolioHoldingsCard.Margin = new Thickness(0);
                PortfolioContentGrid.RowDefinitions[1].Height = GridLength.Auto;
                return;
            }

            PortfolioSummaryGrid.Columns = narrow ? 1 : 3;
            PortfolioSummaryGrid.Rows = narrow ? 3 : 1;

            for (var i = 0; i < PortfolioSummaryGrid.Children.Count; i++)
            {
                if (PortfolioSummaryGrid.Children[i] is FrameworkElement child)
                {
                    child.Margin = narrow
                        ? new Thickness(0, 0, 0, i == PortfolioSummaryGrid.Children.Count - 1 ? 0 : 12)
                        : new Thickness(0, 0, i == PortfolioSummaryGrid.Children.Count - 1 ? 0 : 18, 0);
                }
            }

            ApplyPortfolioFormLayout(narrow);

            if (narrow)
            {
                Grid.SetRow(PortfolioHoldingsCard, 0);
                Grid.SetColumn(PortfolioHoldingsCard, 0);
                Grid.SetColumnSpan(PortfolioHoldingsCard, 2);
                PortfolioHoldingsCard.Margin = new Thickness(0, 0, 0, 12);

                Grid.SetRow(PortfolioActivityCard, 1);
                Grid.SetColumn(PortfolioActivityCard, 0);
                Grid.SetColumnSpan(PortfolioActivityCard, 2);
                PortfolioActivityCard.Margin = new Thickness(0);
                PortfolioActivityCard.MaxHeight = 240;
                PortfolioContentGrid.RowDefinitions[1].Height = new GridLength(240);
                return;
            }

            Grid.SetRow(PortfolioHoldingsCard, 0);
            Grid.SetColumn(PortfolioHoldingsCard, 0);
            Grid.SetColumnSpan(PortfolioHoldingsCard, 1);
            PortfolioHoldingsCard.Margin = new Thickness(0, 0, 18, 0);

            Grid.SetRow(PortfolioActivityCard, 0);
            Grid.SetColumn(PortfolioActivityCard, 1);
            Grid.SetColumnSpan(PortfolioActivityCard, 1);
            PortfolioActivityCard.Margin = new Thickness(0);
            PortfolioActivityCard.MaxHeight = double.PositiveInfinity;
            PortfolioContentGrid.RowDefinitions[1].Height = GridLength.Auto;
        }

        private void ApplyPortfolioFormLayout(bool narrow)
        {
            if (narrow)
            {
                PortfolioTransactionFormGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                PortfolioTransactionFormGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                PortfolioTransactionFormGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
                PortfolioTransactionFormGrid.ColumnDefinitions[3].Width = new GridLength(0);
                PortfolioTransactionFormGrid.ColumnDefinitions[4].Width = new GridLength(0);
                PortfolioTransactionFormGrid.ColumnDefinitions[5].Width = new GridLength(0);

                Grid.SetRow(PortfolioAssetInput, 0);
                Grid.SetColumn(PortfolioAssetInput, 0);
                Grid.SetColumnSpan(PortfolioAssetInput, 2);
                PortfolioAssetInput.Margin = new Thickness(0, 0, 12, 12);

                Grid.SetRow(PortfolioExchangeInput, 0);
                Grid.SetColumn(PortfolioExchangeInput, 2);
                Grid.SetColumnSpan(PortfolioExchangeInput, 1);
                PortfolioExchangeInput.Margin = new Thickness(0, 0, 0, 12);

                Grid.SetRow(PortfolioAmountInput, 1);
                Grid.SetColumn(PortfolioAmountInput, 0);
                Grid.SetColumnSpan(PortfolioAmountInput, 1);
                PortfolioAmountInput.Margin = new Thickness(0, 0, 12, 12);

                Grid.SetRow(PortfolioPriceInput, 1);
                Grid.SetColumn(PortfolioPriceInput, 1);
                Grid.SetColumnSpan(PortfolioPriceInput, 1);
                PortfolioPriceInput.Margin = new Thickness(0, 0, 12, 12);

                Grid.SetRow(PortfolioFeeInput, 1);
                Grid.SetColumn(PortfolioFeeInput, 2);
                Grid.SetColumnSpan(PortfolioFeeInput, 1);
                PortfolioFeeInput.Margin = new Thickness(0, 0, 0, 12);

                Grid.SetRow(PortfolioAddAction, 2);
                Grid.SetColumn(PortfolioAddAction, 0);
                Grid.SetColumnSpan(PortfolioAddAction, 3);
                PortfolioAddAction.Margin = new Thickness(0);
                return;
            }

            PortfolioTransactionFormGrid.ColumnDefinitions[0].Width = new GridLength(2, GridUnitType.Star);
            PortfolioTransactionFormGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            PortfolioTransactionFormGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            PortfolioTransactionFormGrid.ColumnDefinitions[3].Width = new GridLength(1, GridUnitType.Star);
            PortfolioTransactionFormGrid.ColumnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);
            PortfolioTransactionFormGrid.ColumnDefinitions[5].Width = GridLength.Auto;

            var inputs = new[]
            {
                PortfolioAssetInput,
                PortfolioAmountInput,
                PortfolioPriceInput,
                PortfolioFeeInput,
                PortfolioExchangeInput,
                PortfolioAddAction
            };

            for (var i = 0; i < inputs.Length; i++)
            {
                Grid.SetRow(inputs[i], 0);
                Grid.SetColumn(inputs[i], i);
                Grid.SetColumnSpan(inputs[i], 1);
                inputs[i].Margin = new Thickness(0, 0, i == inputs.Length - 1 ? 0 : 14, 0);
            }
        }
    }
}
