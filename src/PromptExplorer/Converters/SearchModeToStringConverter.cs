using System;
using System.Globalization;
using System.Windows.Data;
using PromptExplorer.Models;

namespace PromptExplorer.Converters
{
    public class SearchModeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SearchMode mode)
            {
                return mode switch
                {
                    SearchMode.AndTags => "AND検索モード",
                    _ => "完全一致モード"
                };
            }

            return "完全一致モード";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
