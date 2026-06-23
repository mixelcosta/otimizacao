using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HardwareOptimizer.App.ViewModels;

namespace HardwareOptimizer.App.Views;

public partial class BiosGuideView : UserControl
{
    public BiosGuideView() => InitializeComponent();

    private async void OnCarregarFotoClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecionar foto do BIOS",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Imagens") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" } },
            },
        });

        if (files.Count == 0) return;
        var path = files[0].TryGetLocalPath();
        if (path is null) return;

        if (DataContext is BiosGuideViewModel vm)
            await vm.AnalisarFotoAsync(path);
    }
}
