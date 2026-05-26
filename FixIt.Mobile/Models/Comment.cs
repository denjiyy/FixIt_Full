using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FixIt.Mobile.Models;

public class Comment : INotifyPropertyChanged
{
    private int _likeCount;
    private int _dislikeCount;
    private bool _userHasLiked;
    private bool _userHasDisliked;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; set; } = string.Empty;
    public string IssueId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorInitials => string.IsNullOrWhiteSpace(AuthorName) ? "F" : AuthorName[0].ToString().ToUpperInvariant();
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public int LikeCount
    {
        get => _likeCount;
        set { if (_likeCount != value) { _likeCount = value; OnPropertyChanged(); } }
    }

    public int DislikeCount
    {
        get => _dislikeCount;
        set { if (_dislikeCount != value) { _dislikeCount = value; OnPropertyChanged(); } }
    }

    public bool UserHasLiked
    {
        get => _userHasLiked;
        set { if (_userHasLiked != value) { _userHasLiked = value; OnPropertyChanged(); } }
    }

    public bool UserHasDisliked
    {
        get => _userHasDisliked;
        set { if (_userHasDisliked != value) { _userHasDisliked = value; OnPropertyChanged(); } }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
