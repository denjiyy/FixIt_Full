namespace FixIt.Data.Repository;

public sealed class InvalidEntityIdException : ArgumentException
{
    public InvalidEntityIdException(string id)
        : base($"Invalid entity identifier format: '{id}'.", nameof(id))
    {
    }
}
