using CommunityToolkit.Mvvm.ComponentModel;

namespace CameraViewer.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels.
    /// Provides INotifyPropertyChanged implementation via ObservableObject.
    /// </summary>
    public abstract class ViewModelBase : ObservableObject
    {
        // Base functionality provided by ObservableObject:
        // - INotifyPropertyChanged
        // - SetProperty<T> method
        // - OnPropertyChanged method
    }
}
