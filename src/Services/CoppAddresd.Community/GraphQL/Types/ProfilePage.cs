using CoppAddresd.Community.Entities;

namespace CoppAddresd.Community.GraphQL.Types;

/// <summary>Página del directorio de miembros con total filtrado calculado en PostgreSQL.</summary>
public sealed record ProfilePage(IReadOnlyList<Profile> Items, int TotalCount);
