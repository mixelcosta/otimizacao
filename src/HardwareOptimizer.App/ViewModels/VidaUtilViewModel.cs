using CommunityToolkit.Mvvm.ComponentModel;

namespace HardwareOptimizer.App.ViewModels;

public partial class VidaUtilViewModel : ObservableObject
{
    [ObservableProperty] private string _status = "Em desenvolvimento";
}
