using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community;

/// <summary>
/// Siembra datos de demostración de la comunidad ERP solo en desarrollo (si la tabla
/// de perfiles está vacía). Genera ~40 perfiles con región/diagnóstico/semana y señales
/// de actividad variadas (para que el riesgo varíe), entradas de XP, publicaciones en
/// todos los tipos/destinos y eventos de feed coherentes.
/// </summary>
public static class CommunitySeeder
{
    private static readonly string[] Names =
    [
        "Carolina Mendoza", "Andrés Cárdenas", "Valentina Ríos", "Jorge Herrera",
        "María González", "Luis Pérez", "Camila Torres", "Sebastián Díaz",
        "Isabella Ramírez", "Mateo López", "Sofía Castro", "Diego Morales",
        "Lucía Fernández", "Tomás Romero", "Daniela Vargas", "Samuel Ortiz",
        "Antonella Núñez", "Emilio Prieto", "Martina Aguirre", "Benjamín Reyes",
        "Emma Soto", "Thiago Méndez", "Renata Cordero", "Leonardo Pacheco",
        "Aitana Rivas", "Matías Gil", "Olivia Sepúlveda", "Nicolás Bravo",
        "Jimena Córdoba", "Bruno Lara", "Alba Navarro", "Gael Herrera",
        "Noa Villalobos", "Iker Salazar", "Vega Montoya", "Dario Céspedes",
        "Catalina Quintero", "Adrián Beltrán", "Esperanza Cano", "Facundo Ríos",
    ];

    private static readonly ProfileRegion[] Regions =
        [ProfileRegion.Miami, ProfileRegion.NY, ProfileRegion.Barranquilla,
         ProfileRegion.Bogota, ProfileRegion.Orlando, ProfileRegion.CDMX];

    private static readonly ProfileDiagnosis[] Diagnoses =
        [ProfileDiagnosis.DM2, ProfileDiagnosis.Obesidad,
         ProfileDiagnosis.DM2HTA, ProfileDiagnosis.Prediabetes];

    private static readonly PostType[] AllPostTypes =
        [PostType.Texto, PostType.Imagen, PostType.Video, PostType.Encuesta, PostType.Logro];

    private static readonly PostDestination[] AllDestinations =
    [
        PostDestination.TodasLasComunidades, PostDestination.ComunidadADRED,
        PostDestination.RetoCaminata, PostDestination.ApoyoEmocional,
        PostDestination.CocinaSaludable, PostDestination.SoloInactivos,
    ];

    public static async Task SeedAsync(CommunityDbContext db, CancellationToken ct = default)
    {
        if (await db.Profiles.AnyAsync(ct))
            return;

        var now = DateTime.UtcNow;
        var rnd = new Random(20260826);
        var profiles = new List<Profile>();

        for (var i = 0; i < Names.Length; i++)
        {
            // Señal de actividad variable (0..29 días) para que el riesgo varíe.
            var offsetDays = (i * 3) % 30;
            var lastActivity = now.AddDays(-offsetDays).AddHours(-(i % 12));
            var profile = new Profile
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                DisplayName = Names[i],
                Status = ProfileStatus.Active,
                Region = Regions[i % Regions.Length],
                Diagnosis = Diagnoses[i % Diagnoses.Length],
                Week = (i % 8) + 1,
                LastActiveAt = DateTimeOffset.UtcNow.AddDays(-offsetDays),
                LastPostAt = i % 4 == 0 ? null : DateTimeOffset.UtcNow.AddDays(-offsetDays),
                CreatedAt = now.AddDays(-60 + i),
            };

            // XP inicial variado (algunos perfiles con niveles distintos).
            var xpAmount = (i + 1) * 137;
            db.XpEntries.Add(new XpEntry
            {
                Id = Guid.NewGuid(),
                Profile = profile,
                Amount = xpAmount,
                Reason = "Bienvenida a la comunidad",
                CreatedAt = profile.CreatedAt,
            });
            profile.XpTotal = xpAmount;

            // Publicaciones para ~el primer tercio, para dar racha y contenido.
            if (i < 14)
            {
                var postCount = 1 + (i % 3);
                for (var k = 0; k < postCount; k++)
                {
                    var type = AllPostTypes[(i + k) % AllPostTypes.Length];
                    var destination = AllDestinations[(i + k) % AllDestinations.Length];
                    var created = now.AddDays(-(k * 1)).AddHours(-(k * 2));
                    var post = new Post
                    {
                        Id = Guid.NewGuid(),
                        Profile = profile,
                        Body = $"{Names[i]} comparte en la comunidad ({type}).",
                        Type = type,
                        Destination = destination,
                        CreatedAt = created,
                    };
                    db.Posts.Add(post);

                    db.FeedEvents.Add(new FeedEvent
                    {
                        Id = Guid.NewGuid(),
                        Profile = profile,
                        Kind = MapPostTypeToFeedEvent(type),
                        Body = post.Body.Length > 500 ? post.Body[..500] : post.Body,
                        CreatedAt = created,
                    });
                }

                // Recalcula racha a partir de las fechas de las publicaciones.
                var postDates = Enumerable.Range(0, postCount)
                    .Select(k => now.AddDays(-(k * 1)).AddHours(-(k * 2)))
                    .ToList();
                profile.CurrentStreak = CommunityStats.CurrentStreak(postDates);
                profile.BestStreak = CommunityStats.BestStreak(postDates);
            }

            db.Profiles.Add(profile);
            profiles.Add(profile);
        }

        await db.SaveChangesAsync(ct);
    }

    private static FeedEventKind MapPostTypeToFeedEvent(PostType type) => type switch
    {
        PostType.Imagen => FeedEventKind.Foto,
        PostType.Video => FeedEventKind.Video,
        PostType.Encuesta => FeedEventKind.Publicacion,
        PostType.Logro => FeedEventKind.Logro,
        _ => FeedEventKind.Publicacion,
    };
}
