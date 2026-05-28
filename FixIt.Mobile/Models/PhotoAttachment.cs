using FixIt.Mobile.ViewModels;

namespace FixIt.Mobile.Models;

public sealed class PhotoAttachment
{
    public required byte[] Bytes { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public ImageSource? Thumbnail { get; init; }
}
