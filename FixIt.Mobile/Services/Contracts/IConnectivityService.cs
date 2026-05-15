namespace FixIt.Mobile.Services.Contracts;

public interface IConnectivityService
{
    event EventHandler<bool>? ConnectivityChanged;
    bool IsOnline { get; }
}
