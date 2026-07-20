using System.Globalization;
using System.Windows;
using Microsoft.Win32;
using Yuexin.Radar.Bridge.Wpf.Controls;
using Yuexin.Radar.Bridge.Wpf.Services;
using Yuexin.Radar.Bridge.Wpf.ViewModels;

namespace Yuexin.Radar.Bridge.Wpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IRadarBridgeRuntime _runtime;

    public MainWindow(MainViewModel viewModel, IRadarBridgeRuntime runtime)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnRegionVertexMoved(object sender, RegionVertexMovedEventArgs eventArgs)
    {
        _viewModel.UpdateRegionVertex(eventArgs.Index, eventArgs.WorldPosition);
    }

    private async void OnStartRecordingClick(object sender, RoutedEventArgs eventArgs)
    {
        var dialog = new SaveFileDialog
        {
            Title = "保存雷达原始数据录制",
            Filter = "Radar recording (*.radarrec)|*.radarrec",
            DefaultExt = ".radarrec",
            AddExtension = true,
            FileName = $"radar-{DateTime.Now:yyyyMMdd-HHmmss}.radarrec"
        };
        if (dialog.ShowDialog(this) == true)
        {
            await ExecuteUiActionAsync(() => _runtime.StartRecordingAsync(dialog.FileName));
        }
    }

    private async void OnStopRecordingClick(object sender, RoutedEventArgs eventArgs)
    {
        await ExecuteUiActionAsync(_runtime.StopRecordingAsync);
    }

    private async void OnReplayClick(object sender, RoutedEventArgs eventArgs)
    {
        var dialog = new OpenFileDialog
        {
            Title = "打开雷达原始数据录制",
            Filter = "Radar recording (*.radarrec)|*.radarrec",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var speedText = (ReplaySpeedCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() ?? "1";
        var speed = double.Parse(speedText, CultureInfo.InvariantCulture);
        await ExecuteUiActionAsync(() => _runtime.ReplayAsync(dialog.FileName, speed, ReplayLoopCheckBox.IsChecked == true));
    }

    private void OnPauseReplayClick(object sender, RoutedEventArgs eventArgs) => _runtime.PauseReplay();
    private void OnResumeReplayClick(object sender, RoutedEventArgs eventArgs) => _runtime.ResumeReplay();
    private void OnStepReplayClick(object sender, RoutedEventArgs eventArgs) => _runtime.StepReplay();

    private async void OnStopReplayClick(object sender, RoutedEventArgs eventArgs)
    {
        await ExecuteUiActionAsync(_runtime.StopReplayAsync);
    }

    private static async Task ExecuteUiActionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "RadarBridge 操作失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
