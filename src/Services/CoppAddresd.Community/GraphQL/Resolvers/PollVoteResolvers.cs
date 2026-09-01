using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Persistence;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.GraphQL.Resolvers;

/// <summary>Resolves the Profile navigation on PollVote (not stored as FK navigation in entity).</summary>
[ExtendObjectType(typeof(PollVote))]
public sealed class PollVoteResolvers
{
    /// <summary>
    /// Returns the Profile that cast this vote. The ERP can display
    /// WHO voted per option (gated by Community.Moderate on frontend).
    /// Simple query per vote — acceptable for demo data (&lt;20 votes).
    /// </summary>
    public async Task<Profile?> GetProfileAsync(
        [Parent] PollVote vote,
        [Service] CommunityDbContext db,
        CancellationToken ct)
    {
        return await db.Profiles.FirstOrDefaultAsync(p => p.Id == vote.ProfileId, ct);
    }
}
