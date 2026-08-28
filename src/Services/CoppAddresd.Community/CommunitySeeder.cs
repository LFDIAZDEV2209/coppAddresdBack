using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoppAddresd.Community;

/// <summary>
/// Siembra datos de demostración de la comunidad ERP solo en desarrollo (si la tabla
/// de perfiles está vacía). Genera ~40 perfiles con región/diagnóstico/semana y señales
/// de actividad variadas (para que el riesgo varíe), entradas de XP, publicaciones en
/// todos los tipos/destinos y eventos de feed coherentes. Las publicaciones se extienden
/// por los últimos 30 días con horas variadas, y se incluyen comentarios y likes.
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
         ProfileRegion.Orlando, ProfileRegion.Houston, ProfileRegion.Dallas,
         ProfileRegion.Atlanta, ProfileRegion.Seattle, ProfileRegion.Denver];

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

    /// <summary>Textos de ejemplo para comentarios del seeder.</summary>
    private static readonly string[] CommentBodies =
    [
        "¡Excelente publicación! Me encanta ver el progreso de la comunidad.",
        "Gracias por compartir, muy motivador para todos nosotros.",
        "¿Alguien más ha probado esta receta? Se ve deliciosa.",
        "Felicitaciones por la constancia, sigue así.",
        "Muy interesante, me gustaría saber más detalles.",
        "Genial aporte, lo voy a intentar esta semana.",
        "Esto es justo lo que necesitaba leer hoy.",
        "¡Gran reto! Yo también estoy participando.",
    ];

    public static async Task SeedAsync(CommunityDbContext db, IConfiguration? configuration = null, CancellationToken ct = default)
    {
        // ─── PERFIL DEL SISTEMA (siempre, independiente de los datos demo) ───
        var systemProfile = await db.Profiles.FirstOrDefaultAsync(p => p.IsSystem, ct);
        if (systemProfile is null)
        {
            var senderName = configuration?["Community:AnnouncementSenderName"] ?? "Equipo ANTARES";
            db.Profiles.Add(new Profile
            {
                Id = Guid.NewGuid(),
                UserId = null,
                IsSystem = true,
                DisplayName = senderName,
                Status = ProfileStatus.Active,
                Bio = null,
                Region = null,
                Diagnosis = null,
                Week = null,
                CurrentStreak = 0,
                BestStreak = 0,
                XpTotal = 0,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }

        if (await db.Profiles.AnyAsync(p => !p.IsSystem, ct))
        {
            // En desarrollo: si hay perfiles con regiones obsoletas (null tras cambio de enum),
            // limpiar todo y re-sembrar con las ciudades actuales.
            var hasStaleRegions = await db.Profiles.AnyAsync(p => !p.IsSystem && p.Region == null, ct);
            if (!hasStaleRegions)
                return;

            // Borrar TODOS los datos demo en orden FK seguro y re-sembrar
            await db.Database.ExecuteSqlRawAsync("DELETE FROM community.feed_events", ct);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM community.likes", ct);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM community.comments", ct);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM community.post_reports", ct);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM community.xp_entries", ct);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM community.recognitions", ct);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM community.messages", ct);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM community.follows", ct);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM community.chat_group_members", ct);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM community.chat_groups", ct);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM community.posts", ct);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM community.profiles WHERE NOT is_system", ct);
        }

        var now = DateTime.UtcNow;
        var rnd = new Random(20260826);
        var profiles = new List<Profile>();
        var allPosts = new List<Post>(); // Posts creados, para asignar comentarios/likes

        // ─── PERFILES ────────────────────────────────────────────────────
        for (var i = 0; i < Names.Length; i++)
        {
            // Señal de actividad variable (0..29 días) para que el riesgo varíe.
            var offsetDays = (i * 3) % 30;
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

            db.Profiles.Add(profile);
            profiles.Add(profile);
        }

        // ─── PUBLICACIONES (spread 30 días) ──────────────────────────────
        // ~27 posts distribuidos entre 27 perfiles, repartidos en
        // los últimos 30 días con horas variadas para que el dashboard muestre
        // datos interesantes.
        var postOwners = profiles.Take(27).ToList();
        var postDates = new DateTime[27];
        for (var k = 0; k < 27; k++)
        {
            // Distribuir en 30 días: día = k*30/27, hora variada (8..22)
            var dayOffset = (int)(k * 30L / 27);
            var hourOffset = 8 + (k * 7) % 15; // 8..22
            postDates[k] = now.AddDays(-dayOffset).AddHours(-hourOffset).AddMinutes(-(k * 13) % 60);
        }

        for (var k = 0; k < 27; k++)
        {
            var owner = postOwners[k];
            var type = AllPostTypes[k % AllPostTypes.Length];
            var destination = AllDestinations[k % AllDestinations.Length];
            var created = postDates[k];

            var post = new Post
            {
                Id = Guid.NewGuid(),
                ProfileId = owner.Id,
                Body = $"{owner.DisplayName} comparte en la comunidad ({type}).",
                Type = type,
                Destination = destination,
                CreatedAt = created,
            };
            db.Posts.Add(post);
            allPosts.Add(post);

            // Evento de feed coherente con el tipo de publicación.
            db.FeedEvents.Add(new FeedEvent
            {
                Id = Guid.NewGuid(),
                ProfileId = owner.Id,
                Kind = MapPostTypeToFeedEvent(type),
                Body = post.Body.Length > 500 ? post.Body[..500] : post.Body,
                CreatedAt = created,
            });
        }

        // Recalcula rachas de los perfiles que tienen posts.
        var postsByProfile = allPosts.GroupBy(p => p.ProfileId).ToList();
        foreach (var group in postsByProfile)
        {
            var profile = profiles.First(p => p.Id == group.Key);
            var dates = group.Select(p => p.CreatedAt).ToList();
            profile.CurrentStreak = CommunityStats.CurrentStreak(dates);
            profile.BestStreak = CommunityStats.BestStreak(dates);
        }

        // ─── COMENTARIOS (~2-3 por algunos posts) ────────────────────────
        // Los 10 primeros posts reciben 2-3 comentarios cada uno.
        var commentTargets = allPosts.Take(10).ToList();
        var comments = new List<Comment>();
        for (var ci = 0; ci < commentTargets.Count; ci++)
        {
            var targetPost = commentTargets[ci];
            var commentCount = 2 + (ci % 2); // 2 o 3 comentarios
            for (var cj = 0; cj < commentCount; cj++)
            {
                // Comentario creado entre 1 y 48 horas después del post.
                var commenterIdx = (ci * 3 + cj + 15) % profiles.Count;
                var commenter = profiles[commenterIdx];
                var createdAt = targetPost.CreatedAt.AddHours(1 + (cj * 7) % 48);

                var comment = new Comment
                {
                    Id = Guid.NewGuid(),
                    PostId = targetPost.Id,
                    ProfileId = commenter.Id,
                    Body = CommentBodies[(ci + cj) % CommentBodies.Length],
                    CreatedAt = createdAt,
                };
                db.Comments.Add(comment);
                comments.Add(comment);

                // Evento de feed para comentario.
                db.FeedEvents.Add(new FeedEvent
                {
                    Id = Guid.NewGuid(),
                    ProfileId = commenter.Id,
                    Kind = FeedEventKind.Comentario,
                    Body = comment.Body.Length > 500 ? comment.Body[..500] : comment.Body,
                    CreatedAt = createdAt,
                });
            }
        }

        // ─── LIKES (5-10 por posts variados) ─────────────────────────────
        // Distribuir ~30 likes entre los primeros 15 posts.
        var likeTargets = allPosts.Take(15).ToList();
        for (var li = 0; li < 30; li++)
        {
            var targetPost = likeTargets[li % likeTargets.Count];
            var likerIdx = (li * 5 + 7) % profiles.Count;
            var liker = profiles[likerIdx];

            // Like creado entre 30 min y 72 horas después del post.
            var createdAt = targetPost.CreatedAt.AddMinutes(30 + (li * 47) % 4320);

            db.Likes.Add(new Like
            {
                Id = Guid.NewGuid(),
                ProfileId = liker.Id,
                PostId = targetPost.Id,
                CreatedAt = createdAt,
            });
        }

        // ─── RECONOCIMIENTOS (si vacío) ────────────────────────────────
        if (!await db.Recognitions.AnyAsync(ct))
        {
            var recognitionData = new (string Name, string TypeLabel, int Xp, int OffsetDays)[]
            {
                ("Carolina Mendoza", "Miembro del mes", 200, 1),
                ("Andrés Cárdenas", "Racha destacada", 150, 2),
                ("Valentina Ríos", "Adherencia NB", 100, 3),
                ("Jorge Herrera", "Publicación top", 75, 4),
            };

            for (var i = 0; i < recognitionData.Length; i++)
            {
                var (name, typeLabel, xp, offsetDays) = recognitionData[i];
                var profile = profiles.FirstOrDefault(p => p.DisplayName == name);
                if (profile is null) continue;

                db.Recognitions.Add(new Recognition
                {
                    Id = Guid.NewGuid(),
                    ProfileId = profile.Id,
                    TypeLabel = typeLabel,
                    Xp = xp,
                    Status = RecognitionStatus.Sent,
                    CreatedAt = now.AddDays(-offsetDays),
                });
            }
        }

        // ─── CANALES DE RED SOCIAL (si vacío) ──────────────────────────
        if (!await db.NetworkChannels.AnyAsync(ct))
        {
            var channels = new (string Name, string Handle, string Color, int Followers,
                (string Month, int Value)[] Growth)[]
            {
                ("TikTok", "@antares.fya", "#000000", 48200,
                    [("2026-04", 31000), ("2026-05", 35400), ("2026-06", 39800), ("2026-07", 44000), ("2026-08", 48200)]),
                ("Instagram", "@antares.fya", "#E1306C", 23700,
                    [("2026-04", 18000), ("2026-05", 19200), ("2026-06", 20800), ("2026-07", 22100), ("2026-08", 23700)]),
                ("Facebook", "@antaresfya", "#1877F2", 15400,
                    [("2026-04", 11000), ("2026-05", 12100), ("2026-06", 13200), ("2026-07", 14300), ("2026-08", 15400)]),
                ("YouTube", "@antaresfya", "#FF0000", 8100,
                    [("2026-04", 5000), ("2026-05", 5800), ("2026-06", 6500), ("2026-07", 7300), ("2026-08", 8100)]),
                ("WhatsApp", "Comunidad ADRED", "#25D366", 284,
                    [("2026-04", 180), ("2026-05", 204), ("2026-06", 228), ("2026-07", 256), ("2026-08", 284)]),
            };

            for (var i = 0; i < channels.Length; i++)
            {
                var (name, handle, color, followers, growth) = channels[i];
                var channel = new NetworkChannel
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Handle = handle,
                    Color = color,
                    Followers = followers,
                    SortOrder = i,
                };
                db.NetworkChannels.Add(channel);

                foreach (var (month, value) in growth)
                {
                    db.NetworkGrowthPoints.Add(new NetworkGrowthPoint
                    {
                        Id = Guid.NewGuid(),
                        ChannelId = channel.Id,
                        Month = month,
                        Value = value,
                    });
                }
            }
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
