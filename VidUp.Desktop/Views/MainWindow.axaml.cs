using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using VidUp.Desktop.ViewModels;

namespace VidUp.Desktop.Views;

public partial class MainWindow : Window
{
    private bool choosingFiles;
    public MainWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateLayoutMode();
    }

    private ShellViewModel? Model => DataContext as ShellViewModel;

    private void UpdateLayoutMode()
    {
        // Stack details below the queue on smaller windows, keeping both reachable.
        var compact = Bounds.Width < 1100;
        QueueLayout.ColumnDefinitions = new ColumnDefinitions(compact ? "*" : "*,320");
        QueueLayout.RowDefinitions = new RowDefinitions(compact ? "3*,2*" : "*");
        QueueLayout.RowSpacing = compact ? 12 : 0;
        Grid.SetColumn(DetailsPane, compact ? 0 : 1);
        Grid.SetRow(DetailsPane, compact ? 1 : 0);
    }

    private async void AddVideos(object? sender, RoutedEventArgs e)
    {
        if (choosingFiles) return;
        if (!StorageProvider.CanOpen)
        {
            Model?.ReportFilePickerError();
            return;
        }
        choosingFiles = true;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "プレビューに追加する動画を選択",
                AllowMultiple = true,
                FileTypeFilter = [new FilePickerFileType("動画")
                {
                    Patterns = ["*.mp4", "*.mkv", "*.mov", "*.avi", "*.webm", "*.m4v", "*.wmv", "*.mpeg", "*.mpg"]
                }]
            });
            if (files.Count > 0)
                Model?.AddFiles(files.Select(x => x.TryGetLocalPath()).OfType<string>());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            Model?.ReportFilePickerError();
        }
        finally { choosingFiles = false; }
    }

    private void ClearPreview(object? sender, RoutedEventArgs e) => Model?.ClearPreview();

    private void ThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Application.Current is not { } app || sender is not ComboBox picker) return;
        app.RequestedThemeVariant = picker.SelectedIndex switch
        {
            1 => ThemeVariant.Light,
            2 => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    private void QueueDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void QueueDrop(object? sender, DragEventArgs e)
    {
        var files = e.Data.GetFiles();
        if (files is not null)
            Model?.AddFiles(files.OfType<IStorageFile>().Select(x => x.TryGetLocalPath()).OfType<string>());
        e.Handled = true;
    }
}
