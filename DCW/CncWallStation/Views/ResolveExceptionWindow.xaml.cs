using CncWallStation.ViewModels;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace CncWallStation.Views
{
    public partial class ResolveExceptionWindow : Window
    {
        public ResolveExceptionWindow(ResolveExceptionViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        /// <summary>仅允许输入数字和小数点</summary>
        private void DecimalOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^[0-9.]+$");
        }

        /// <summary>粘贴时仅允许数字和小数点</summary>
        private void DecimalOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                var text = (string)e.DataObject.GetData(typeof(string));
                if (!Regex.IsMatch(text, @"^[0-9.]+$"))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
    }
}
