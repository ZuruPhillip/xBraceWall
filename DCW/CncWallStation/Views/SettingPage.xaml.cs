using System.Windows.Controls;
using System.Windows.Input;
using CncWallStation.ViewModels;

namespace CncWallStation.Views;

public partial class SettingPage : Page
{
    private readonly SettingPageViewModel _viewModel;

    public SettingPage(SettingPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel.LoadConfig();
    }

    private void OnChineseClicked(object sender, MouseButtonEventArgs e)
    {
        _viewModel.SwitchToChineseCommand.Execute(null);
    }

    private void OnEnglishClicked(object sender, MouseButtonEventArgs e)
    {
        _viewModel.SwitchToEnglishCommand.Execute(null);
    }
}
