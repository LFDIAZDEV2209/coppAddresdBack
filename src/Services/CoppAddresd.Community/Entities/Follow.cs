namespace CoppAddresd.Community.Entities;

public sealed class Follow
{
    public Guid Id { get; set; }
    public Guid FollowerProfileId { get; set; }
    public Guid FollowingProfileId { get; set; }
    public DateTime CreatedAt { get; set; }
}
