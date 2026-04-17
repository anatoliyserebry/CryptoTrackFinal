using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTrackClient.Converters
{
    public class ProfitLossColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush PositiveBrush = CreateBrush("#12926B");
        private static readonly SolidColorBrush NegativeBrush = CreateBrush("#D44F4F");
        private static readonly SolidColorBrush NeutralBrush = CreateBrush("#112033");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal profitLoss)
            {
                return profitLoss >= 0 ? PositiveBrush : NegativeBrush;
            }
            return NeutralBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static SolidColorBrush CreateBrush(string color)
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(color)!;
            brush.Freeze();
            return brush;
        }
    }
}
