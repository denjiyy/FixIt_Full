using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FixIt.Mobile.Models;

public class SafetyHazard : INotifyPropertyChanged
{
    private bool _canConfirm;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int Confirmations { get; set; }
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool HasCoordinates => Latitude is not null and not 0 && Longitude is not null and not 0;

    public bool CanConfirm
    {
        get => _canConfirm;
        set
        {
            if (_canConfirm == value)
            {
                return;
            }

            _canConfirm = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
