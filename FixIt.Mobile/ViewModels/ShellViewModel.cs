using CommunityToolkit.Mvvm.ComponentModel;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

public partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly IConnectivityService _connectivity;
    private bool _disposed;

    public ShellViewModel(IConnectivityService connectivity)
    {
        _connectivity = connectivity;
        IsOffline = !_connectivity.IsOnline;
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    [ObservableProperty]
    private bool _isOffline;

    private void OnConnectivityChanged(object? sender, bool isOnline)
    {
        MainThread.BeginInvokeOnMainThread(() => IsOffline = !isOnline);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connectivity.ConnectivityChanged -= OnConnectivityChanged;
    }
}
