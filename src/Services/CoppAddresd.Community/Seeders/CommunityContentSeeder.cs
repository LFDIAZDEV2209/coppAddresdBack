using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace CoppAddresd.Community.Seeders;

/// <summary>
/// Siembra el contenido demo de la comunidad ANTARES (desarrollo): perfiles,
/// publicaciones (feed), likes, comentarios y respuestas, seguimientos, chats
/// 1:1 y grupos de chat con sus mensajes.
///
/// El Id de cada perfil es determinístico (derivado del número de documento del
/// usuario demo, misma función que el <c>CommunityDemoSeeder</c> del Auth
/// service): no hay dependencia de orden de arranque entre servicios.
///
/// El seeder es idempotente: verifica la existencia de cada elemento antes de
/// insertarlo y no borra ni modifica contenido creado por usuarios reales.
/// Los mensajes 1:1 solo se siembran si la conversación entre el par está
/// vacía; los de grupo solo si el grupo aún no tiene mensajes.
/// </summary>
public static class CommunityContentSeeder
{
    private sealed record DemoMember(string DocumentNumber, string DisplayName, string Bio);

    /// <summary>Mantener sincronizado con la lista del Auth service
    /// (<c>CommunityDemoSeeder</c>): mismo documento → mismo Id de usuario.</summary>
    private static readonly IReadOnlyList<DemoMember> Members =
    [
        new("1000000001", "Valentina Ríos", "Paciente hipertensa desde 2021. Me encanta caminar y cocinar saludable."),
        new("1000000002", "Andrés Cárdenas", "Sobreviví a un infarto en 2022. Ahora corro 5K y llevo la dieta al pie de la letra."),
        new("1000000003", "Carolina Mendoza", "Nutricionista y paciente de corazón. Fan del programa INFINITO."),
        new("1000000004", "Jorge Herrera", "Hipertenso diagnosticado en 2023. Aprendiendo a bajar la sal, no el ánimo."),
        new("1000000005", "Luisa Fernández", "Madre, cardiópata y optimista. Aquí para compartir y aprender."),
        new("1000000006", "Miguel Ángel Peña", "Ex-fumador desde marzo. El COPP-ADRESD me cambió la vida."),
        new("1000000007", "Diana Ospina", "Enfermera jubilada. Vigilo mi presión a diario y ayudo a otros a hacerlo."),
        new("1000000008", "Camilo Restrepo", "Deportista amateur. La presión alta no me detuvo: me reinventó."),
    ];

    private static readonly IReadOnlyDictionary<string, DemoMember> ByDocument =
        Members.ToDictionary(m => m.DocumentNumber);

    // ---------- Publicaciones ----------

    private sealed record PostSeed(string AuthorDoc, string Body, bool Pinned, int AgeHours, string[] LikeDocs);

    private static readonly IReadOnlyList<PostSeed> Posts =
    [
        new("1000000001",
            "¡Bienvenidos a la Comunidad ANTARES! 👋 Este espacio es de todos: comparte tus avances, dudas y recetas. Reglas simples: respeto ante todo, nada de consejos médicos personalizados y mucho ánimo. ¡Empecemos! 💜",
            true, 240, ["1000000002", "1000000003", "1000000004", "1000000005", "1000000006"]),
        new("1000000002",
            "Recordatorio del programa: tomen su medicación a la misma hora todos los días. ⏰ Usar la alarma del celular me ayudó a no fallar nunca más.",
            true, 216, ["1000000001", "1000000003", "1000000004", "1000000005", "1000000007", "1000000008"]),
        new("1000000003",
            "Receta del día: avena con canela y manzana 🍎 sin azúcar añadida. 15 minutos de preparación y el corazón te lo agradece. ¿Quién la prueba?",
            false, 190, ["1000000001", "1000000005", "1000000007"]),
        new("1000000004",
            "Hoy cumplí 4 semanas midiendo mi presión todos los días. Promedio: 132/85. ¡Bajamos! 💪",
            false, 160, ["1000000001", "1000000002", "1000000003", "1000000006"]),
        new("1000000005",
            "Día difícil. La ansiedad me jugó una mala pasada y comí de más. Mañana se empieza de nuevo, sin culpas. 🫂",
            false, 130, ["1000000003", "1000000007", "1000000008"]),
        new("1000000006",
            "3 meses sin fumar 🚭. Mi presión mejoró muchísimo y mi respiración también. Si estás pensando en dejarlo: ¡hazlo!",
            false, 100, ["1000000001", "1000000002", "1000000004", "1000000008"]),
        new("1000000007",
            "Como enfermera jubilada les digo: no dejen de ir a sus controles. La prevención lo es todo. 💙",
            false, 72, ["1000000003", "1000000005", "1000000006"]),
        new("1000000008",
            "Hoy hice mi primer trote de 30 minutos sin parar. Hace 6 meses no podía subir 2 pisos sin agitarme. 🏃",
            false, 48, ["1000000001", "1000000002", "1000000003", "1000000005", "1000000006", "1000000007"]),
        new("1000000001",
            "Caminata comunitaria este sábado a las 7 am en el parque central. ¡Nos vemos! 🚶‍♀️",
            false, 30, ["1000000002", "1000000003", "1000000004", "1000000005", "1000000008"]),
        new("1000000003",
            "Pregunta rápida: ¿alguien ha probado los ejercicios de respiración de la app? Quiero saber si valen la pena.",
            false, 20, ["1000000001", "1000000005", "1000000007"]),
        new("1000000004",
            "Mi médico me cambió el medicamento y tuve mareos los primeros días. ¿Alguien más pasó por eso? Me ayudaría saberlo.",
            false, 10, ["1000000002", "1000000003", "1000000006"]),
        new("1000000005",
            "Comparto algo que me funciona: escribir 3 cosas buenas del día antes de dormir. Me bajó el estrés un montón. 📓",
            false, 5, ["1000000001", "1000000003", "1000000007", "1000000008"]),
    ];

    // ---------- Comentarios y respuestas ----------

    private sealed record ReplySeed(string AuthorDoc, string Body, int AgeHours);

    private sealed record CommentSeed(string PostAuthorDoc, string PostBody, string AuthorDoc, string Body, int AgeHours, ReplySeed[]? Replies = null);

    private static readonly IReadOnlyList<CommentSeed> Comments =
    [
        new("1000000001", "¡Bienvenidos a la Comunidad ANTARES! 👋 Este espacio es de todos: comparte tus avances, dudas y recetas. Reglas simples: respeto ante todo, nada de consejos médicos personalizados y mucho ánimo. ¡Empecemos! 💜",
            "1000000003", "¡Me encanta! Ojalá hubiera existido esto cuando me diagnosticaron. 💜", 230,
            [
                new("1000000001", "¡Bienvenida, Carolina! Gracias por sumarte.", 225),
            ]),
        new("1000000001", "¡Bienvenidos a la Comunidad ANTARES! 👋 Este espacio es de todos: comparte tus avances, dudas y recetas. Reglas simples: respeto ante todo, nada de consejos médicos personalizados y mucho ánimo. ¡Empecemos! 💜",
            "1000000002", "Listo, cumpliendo desde ya. ¡Vamos con todo! 💪", 220),
        new("1000000003", "Receta del día: avena con canela y manzana 🍎 sin azúcar añadida. 15 minutos de preparación y el corazón te lo agradece. ¿Quién la prueba?",
            "1000000001", "La probé esta mañana y quedó deliciosa. ¿Puedes compartir la medida exacta de canela?", 180,
            [
                new("1000000003", "¡Claro! Una cucharadita rasa es suficiente. Cuéntame cómo te quedó.", 175),
                new("1000000001", "Quedó perfecta, gracias. 😊", 170),
            ]),
        new("1000000004", "Hoy cumplí 4 semanas midiendo mi presión todos los días. Promedio: 132/85. ¡Bajamos! 💪",
            "1000000002", "¡Ese promedio es excelente, Jorge! Sigue así.", 155),
        new("1000000005", "Día difícil. La ansiedad me jugó una mala pasada y comí de más. Mañana se empieza de nuevo, sin culpas. 🫂",
            "1000000007", "Un día no define tu progreso, Luisa. Mañana es una nueva oportunidad. 💙", 125,
            [
                new("1000000005", "Gracias, Diana. Tu mensaje me llegó justo cuando lo necesitaba.", 120),
            ]),
        new("1000000006", "3 meses sin fumar 🚭. Mi presión mejoró muchísimo y mi respiración también. Si estás pensando en dejarlo: ¡hazlo!",
            "1000000008", "¡Eres una inspiración, Miguel! Yo llevo 2 meses y también me siento increíble.", 95),
        new("1000000008", "Hoy hice mi primer trote de 30 minutos sin parar. Hace 6 meses no podía subir 2 pisos sin agitarme. 🏃",
            "1000000002", "¡Felicitaciones, Camilo! El próximo reto es el 5K. 😉", 45,
            [
                new("1000000008", "¡Acepto el reto, Andrés!", 40),
            ]),
        new("1000000004", "Mi médico me cambió el medicamento y tuve mareos los primeros días. ¿Alguien más pasó por eso? Me ayudaría saberlo.",
            "1000000002", "A mí me pasó igual con el losartán. En mi caso pasó en una semana, pero coméntalo con tu médico si persiste.", 8),
        new("1000000005", "Comparto algo que me funciona: escribir 3 cosas buenas del día antes de dormir. Me bajó el estrés un montón. 📓",
            "1000000001", "¡Qué buena idea! Lo empiezo hoy mismo. 📝", 3),
    ];

    // ---------- Seguimientos ----------

    private static readonly (string Follower, string Following)[] MutualFollows =
    [
        ("1000000001", "1000000002"), // Valentina ↔ Andrés
        ("1000000001", "1000000003"), // Valentina ↔ Carolina
        ("1000000002", "1000000004"), // Andrés ↔ Jorge
        ("1000000003", "1000000005"), // Carolina ↔ Luisa
        ("1000000005", "1000000007"), // Luisa ↔ Diana
        ("1000000006", "1000000008"), // Miguel Ángel ↔ Camilo
        ("1000000003", "1000000007"), // Carolina ↔ Diana
    ];

    private static readonly (string Follower, string Following)[] OneWayFollows =
    [
        ("1000000001", "1000000004"), // Valentina sigue a Jorge
        ("1000000007", "1000000006"), // Diana sigue a Miguel Ángel
    ];

    // ---------- Chats 1:1 ----------

    private sealed record MessageSeed(string SenderDoc, string RecipientDoc, string Body, int AgeMinutes);

    private static readonly IReadOnlyList<IReadOnlyList<MessageSeed>> Conversations =
    [
        // Valentina ↔ Andrés: la caminata del sábado
        [
            new("1000000001", "1000000002", "¡Hola Andrés! ¿Vas a la caminata del sábado?", 2600),
            new("1000000002", "1000000001", "¡Hola! Sí, llevo a mi esposa. ¿A qué hora quedamos?", 2590),
            new("1000000001", "1000000002", "7 am en la entrada principal del parque. Lleva agua y buen ánimo 😊", 2580),
            new("1000000002", "1000000001", "Perfecto, allá estaremos. ¡Gracias por organizar!", 2570),
        ],
        // Valentina ↔ Carolina: receta de avena
        [
            new("1000000003", "1000000001", "Valentina, vi que probaste la avena con canela. ¿Qué tal?", 5400),
            new("1000000001", "1000000003", "¡Deliciosa! ¿Tienes otra receta baja en sodio para la cena?", 5390),
            new("1000000003", "1000000001", "Claro: pollo al horno con limón, romero y verduras al vapor. Sin sal, ¡prometo que sabe bien!", 5380),
            new("1000000001", "1000000003", "¡La pruebo esta noche! Te cuento mañana.", 5370),
        ],
        // Andrés ↔ Jorge: mareos por el medicamento
        [
            new("1000000004", "1000000002", "Andrés, ¿tú también tuviste mareos al cambiar de medicamento?", 600),
            new("1000000002", "1000000004", "Sí, con el losartán. Me duraron casi una semana. Tuve paciencia y se fueron.", 590),
            new("1000000004", "1000000002", "Gracias, eso me tranquiliza. Hoy es mi tercer día y ya siento mejoría.", 580),
            new("1000000002", "1000000004", "Genial. Y si algo se siente raro, nunca está de más llamar al médico.", 570),
        ],
        // Miguel Ángel ↔ Camilo: rutina de trote
        [
            new("1000000008", "1000000006", "Miguel, vi tu post de los 3 meses sin fumar. ¡Me motivó un montón!", 3000),
            new("1000000006", "1000000008", "¡Gracias, Camilo! Tú con tu trote vas increíble. ¿Cómo empezaste?", 2990),
            new("1000000008", "1000000006", "Caminando 20 minutos. Después fui alternando con trote corto. Poco a poco.", 2980),
            new("1000000006", "1000000008", "Me gusta. Empiezo esta semana con eso. ¡Nos vemos en la caminata!", 2970),
        ],
    ];

    // ---------- Grupos de chat ----------

    private sealed record GroupSeed(string Name, string CreatorDoc, string[] MemberDocs, int AgeHours, IReadOnlyList<MessageSeed> Messages);

    private static readonly IReadOnlyList<GroupSeed> Groups =
    [
        new("Reto caminata 30 días", "1000000001",
            ["1000000001", "1000000002", "1000000003", "1000000004"],
            72,
            [
                new("1000000001", "1000000001", "¡Bienvenidos al reto! 🚶‍♀️ Meta: 30 días caminando mínimo 20 minutos. ¿Quién se apunta?", 4200),
                new("1000000002", "1000000001", "Apuntado. Voy a caminar después del almuerzo.", 4190),
                new("1000000004", "1000000001", "Yo me apunto también, necesito el empujón.", 4180),
                new("1000000003", "1000000001", "Día 1 cumplido: 25 minutos en la mañana. ✅", 3000),
                new("1000000001", "1000000001", "¡Vamos bien! Día 2: 22 minutos. ¿Cómo van?", 2400),
                new("1000000002", "1000000001", "Día 2 listo. La rodilla me dolió un poco, pero aguanté.", 2300),
                new("1000000003", "1000000001", "Cuidado con la rodilla, Andrés. Estira bien antes.", 2290),
                new("1000000001", "1000000001", "Día 3: hoy toca la caminata grupal en el parque. ¡Nos vemos a las 7! 🌅", 90),
            ]),
        new("Cocina saludable ANTARES", "1000000003",
            ["1000000003", "1000000001", "1000000005", "1000000007"],
            120,
            [
                new("1000000003", "1000000003", "Hola a todos! Este grupo es para compartir recetas bajas en sodio y grasas. Empiezo yo: avena con canela y manzana 🍎", 6600),
                new("1000000001", "1000000003", "¡La hice y quedó increíble! Añadí un poquito de nuez.", 6500),
                new("1000000005", "1000000003", "¿Alguien tiene una idea para el desayuno de un niño que no come fruta?", 6400),
                new("1000000007", "1000000003", "Batido de banano con avena y yogur natural. ¡A los niños les encanta!", 6300),
                new("1000000003", "1000000003", "Receta de hoy: pollo al horno con limón y romero. Sin sal y delicioso. 🍗", 2000),
                new("1000000005", "1000000003", "La hice anoche para la familia. Nadie notó que no tenía sal. 😄", 1900),
            ]),
        new("Apoyo emocional", "1000000005",
            ["1000000005", "1000000007", "1000000006", "1000000008"],
            96,
            [
                new("1000000005", "1000000005", "Este espacio es para hablar sin miedo. Aquí nadie te juzga. 🫂", 5200),
                new("1000000007", "1000000005", "Gracias, Luisa. Los días difíciles existen y está bien decirlo.", 5100),
                new("1000000006", "1000000005", "Dejar de fumar fue un vaivén emocional. Este tipo de grupos ayudan de verdad.", 5000),
                new("1000000005", "1000000005", "Tip de hoy: 5 minutos de respiración profunda cuando sientan ansiedad. Inspiren 4 segundos, exhalen 6.", 3000),
                new("1000000008", "1000000005", "Lo probé antes de dormir y dormí como un bebé. Gracias por el tip.", 2900),
                new("1000000007", "1000000005", "Recuerden: pedir ayuda también es fortaleza. Aquí estamos. 💙", 1500),
            ]),
    ];

    /// <summary>
    /// Id determinístico de un usuario demo a partir de su documento. Debe
    /// coincidir exactamente con la función homónima del Auth service.
    /// </summary>
    public static Guid DemoUserId(string documentNumber)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes($"antares-community-demo:{documentNumber}"));
        return new Guid(hash);
    }

    public static async Task SeedAsync(CommunityDbContext db, ILogger logger, CancellationToken ct)
    {
        logger.LogInformation("Seeding community demo content...");

        var profiles = await SeedProfilesAsync(db, logger, ct);
        var posts = await SeedPostsAsync(db, profiles, ct);
        await SeedCommentsAsync(db, profiles, posts, ct);
        await SeedFollowsAsync(db, profiles, ct);
        await SeedConversationsAsync(db, profiles, ct);
        await SeedGroupsAsync(db, profiles, ct);
        await LinkCurrentUserAsync(db, profiles, ct);

        logger.LogInformation("Community demo content seeded.");
    }

    // ---------- Perfiles ----------

    private static async Task<Dictionary<string, Profile>> SeedProfilesAsync(
        CommunityDbContext db, ILogger logger, CancellationToken ct)
    {
        var profiles = new Dictionary<string, Profile>();
        var existing = await db.Profiles
            .Where(p => p.UserId != null && Members.Select(m => DemoUserId(m.DocumentNumber)).Contains(p.UserId.Value))
            .ToListAsync(ct);
        var existingByUserId = existing.Where(p => p.UserId != null).ToDictionary(p => p.UserId!.Value);

        var days = 0;
        foreach (var m in Members)
        {
            var userId = DemoUserId(m.DocumentNumber);
            if (existingByUserId.TryGetValue(userId, out var profile))
            {
                profiles[m.DocumentNumber] = profile;
                continue;
            }

            profile = new Profile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DisplayName = m.DisplayName,
                Bio = m.Bio,
                Status = ProfileStatus.Active,
                CreatedAt = DateTime.UtcNow.AddDays(-(28 - days)),
            };
            db.Profiles.Add(profile);
            profiles[m.DocumentNumber] = profile;
            days++;
            logger.LogInformation("Community demo profile created: {DisplayName}", m.DisplayName);
        }

        await db.SaveChangesAsync(ct);
        return profiles;
    }

    // ---------- Publicaciones, likes y comentarios ----------

    private static async Task<Dictionary<(Guid AuthorId, string Body), Post>> SeedPostsAsync(
        CommunityDbContext db, Dictionary<string, Profile> profiles, CancellationToken ct)
    {
        var seededBodies = Posts.Select(p => (AuthorId: profiles[p.AuthorDoc].Id, p.Body)).ToList();
        var existing = await db.Posts
            .Where(p => seededBodies.Select(s => s.AuthorId).Contains(p.ProfileId)
                        && seededBodies.Select(s => s.Body).Contains(p.Body))
            .ToListAsync(ct);
        var existingKeys = existing.Select(p => (p.ProfileId, p.Body)).ToHashSet();

        var result = new Dictionary<(Guid AuthorId, string Body), Post>();
        foreach (var seed in Posts)
        {
            var author = profiles[seed.AuthorDoc];
            var key = (author.Id, seed.Body);

            Post post;
            if (existingKeys.Contains(key))
            {
                post = existing.First(p => p.ProfileId == author.Id && p.Body == seed.Body);
            }
            else
            {
                post = new Post
                {
                    Id = Guid.NewGuid(),
                    ProfileId = author.Id,
                    Body = seed.Body,
                    Pinned = seed.Pinned,
                    CreatedAt = DateTime.UtcNow.AddHours(-seed.AgeHours),
                };
                db.Posts.Add(post);
            }

            // Likes (solo si aún no existe el par post+perfil; el autor no se
            // da like a sí mismo en el seed).
            var likeProfileIds = seed.LikeDocs
                .Where(d => d != seed.AuthorDoc)
                .Select(d => profiles[d].Id)
                .ToList();
            var existingLikes = await db.Likes
                .Where(l => l.PostId == post.Id && likeProfileIds.Contains(l.ProfileId))
                .Select(l => l.ProfileId)
                .ToListAsync(ct);
            foreach (var pid in likeProfileIds.Where(id => !existingLikes.Contains(id)))
            {
                db.Likes.Add(new Like
                {
                    Id = Guid.NewGuid(),
                    PostId = post.Id,
                    ProfileId = pid,
                    CreatedAt = DateTime.UtcNow.AddHours(-seed.AgeHours + 1),
                });
            }

            result[key] = post;
        }

        await db.SaveChangesAsync(ct);
        return result;
    }

    private static async Task SeedCommentsAsync(
        CommunityDbContext db, Dictionary<string, Profile> profiles,
        Dictionary<(Guid AuthorId, string Body), Post> posts, CancellationToken ct)
    {
        foreach (var seed in Comments)
        {
            var postAuthor = profiles[seed.PostAuthorDoc];
            var post = posts[(postAuthor.Id, seed.PostBody)];
            var author = profiles[seed.AuthorDoc];

            var existingRoot = await db.Comments
                .AnyAsync(c => c.PostId == post.Id && c.ProfileId == author.Id
                               && c.Body == seed.Body && c.ParentCommentId == null, ct);
            if (existingRoot) continue;

            var root = new Comment
            {
                Id = Guid.NewGuid(),
                PostId = post.Id,
                ProfileId = author.Id,
                Body = seed.Body,
                CreatedAt = DateTime.UtcNow.AddHours(-seed.AgeHours),
            };
            db.Comments.Add(root);

            if (seed.Replies is not null)
            {
                foreach (var reply in seed.Replies)
                {
                    db.Comments.Add(new Comment
                    {
                        Id = Guid.NewGuid(),
                        PostId = post.Id,
                        ProfileId = profiles[reply.AuthorDoc].Id,
                        ParentCommentId = root.Id,
                        Body = reply.Body,
                        CreatedAt = DateTime.UtcNow.AddHours(-reply.AgeHours),
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // ---------- Seguimientos ----------

    private static async Task SeedFollowsAsync(
        CommunityDbContext db, Dictionary<string, Profile> profiles, CancellationToken ct)
    {
        var pairs = MutualFollows
            .SelectMany(f => new[]
            {
                (Follower: f.Follower, Following: f.Following),
                (Follower: f.Following, Following: f.Follower),
            })
            .Concat(OneWayFollows)
            .ToList();

        var ids = pairs.Select(p => (Follower: profiles[p.Follower].Id, Following: profiles[p.Following].Id)).ToList();
        var existing = (await db.Follows
                .Where(f => ids.Select(x => x.Follower).Contains(f.FollowerProfileId)
                            && ids.Select(x => x.Following).Contains(f.FollowingProfileId))
                .ToListAsync(ct))
            .Select(f => (f.FollowerProfileId, f.FollowingProfileId))
            .ToHashSet();

        foreach (var (followerDoc, followingDoc) in pairs)
        {
            var pair = (Follower: profiles[followerDoc].Id, Following: profiles[followingDoc].Id);
            if (existing.Contains(pair)) continue;

            db.Follows.Add(new Follow
            {
                Id = Guid.NewGuid(),
                FollowerProfileId = pair.Follower,
                FollowingProfileId = pair.Following,
                CreatedAt = DateTime.UtcNow.AddDays(-20),
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // ---------- Chats 1:1 ----------

    private static async Task SeedConversationsAsync(
        CommunityDbContext db, Dictionary<string, Profile> profiles, CancellationToken ct)
    {
        foreach (var conversation in Conversations)
        {
            var first = conversation[0];
            var (a, b) = (profiles[first.SenderDoc].Id, profiles[first.RecipientDoc].Id);

            // Solo se siembra la conversación si el par no tiene mensajes aún
            // (no duplicar contenido si el usuario ya chateó con el demo).
            var hasMessages = await db.Messages
                .AnyAsync(m => m.ConversationId == null
                               && ((m.SenderProfileId == a && m.RecipientProfileId == b)
                                   || (m.SenderProfileId == b && m.RecipientProfileId == a)), ct);
            if (hasMessages) continue;

            foreach (var msg in conversation)
            {
                db.Messages.Add(new Message
                {
                    Id = Guid.NewGuid(),
                    SenderProfileId = profiles[msg.SenderDoc].Id,
                    RecipientProfileId = profiles[msg.RecipientDoc].Id,
                    Body = msg.Body,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-msg.AgeMinutes),
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // ---------- Grupos de chat ----------

    private static async Task SeedGroupsAsync(
        CommunityDbContext db, Dictionary<string, Profile> profiles, CancellationToken ct)
    {
        foreach (var seed in Groups)
        {
            var creator = profiles[seed.CreatorDoc];
            var group = await db.ChatGroups
                .FirstOrDefaultAsync(g => g.Name == seed.Name && g.CreatedByProfileId == creator.Id, ct);

            if (group is null)
            {
                group = new ChatGroup
                {
                    Id = Guid.NewGuid(),
                    Name = seed.Name,
                    CreatedByProfileId = creator.Id,
                    CreatedAt = DateTime.UtcNow.AddHours(-seed.AgeHours),
                };
                db.ChatGroups.Add(group);
            }

            // Miembros (el creador siempre es miembro).
            var memberProfileIds = seed.MemberDocs.Select(d => profiles[d].Id).ToHashSet();
            memberProfileIds.Add(creator.Id);
            var existingMembers = await db.ChatGroupMembers
                .Where(m => m.GroupId == group.Id && memberProfileIds.Contains(m.ProfileId))
                .Select(m => m.ProfileId)
                .ToListAsync(ct);
            foreach (var pid in memberProfileIds.Where(id => !existingMembers.Contains(id)))
            {
                db.ChatGroupMembers.Add(new ChatGroupMember
                {
                    GroupId = group.Id,
                    ProfileId = pid,
                    JoinedAt = DateTime.UtcNow.AddHours(-seed.AgeHours),
                });
            }

            // Mensajes del grupo: solo si el grupo aún no tiene mensajes.
            var hasMessages = await db.Messages.AnyAsync(m => m.ConversationId == group.Id, ct);
            if (!hasMessages)
            {
                foreach (var msg in seed.Messages)
                {
                    db.Messages.Add(new Message
                    {
                        Id = Guid.NewGuid(),
                        SenderProfileId = profiles[msg.SenderDoc].Id,
                        ConversationId = group.Id,
                        Body = msg.Body,
                        CreatedAt = DateTime.UtcNow.AddMinutes(-msg.AgeMinutes),
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    // ---------- Vínculos con el usuario real ----------

    /// <summary>
    /// Conecta al usuario real de la app (el perfil creado por auto-provisión,
    /// que no es un usuario demo) con la comunidad demo: amigos mutuos, un chat
    /// 1:1 y membresía en los grupos. Solo actúa si ese perfil existe.
    /// </summary>
    private static async Task LinkCurrentUserAsync(
        CommunityDbContext db, Dictionary<string, Profile> demoProfiles, CancellationToken ct)
    {
        var demoUserIds = Members.Select(m => DemoUserId(m.DocumentNumber)).ToHashSet();
        var me = await db.Profiles.FirstOrDefaultAsync(p => p.UserId != null && !demoUserIds.Contains(p.UserId.Value), ct);
        if (me is null) return;

        // Amigos mutuos con Valentina, Andrés y Carolina; siguiendo a Luisa;
        // Jorge nos sigue a nosotros (aparece como seguidor, no amigo).
        var friends = new[] { "1000000001", "1000000002", "1000000003" };
        foreach (var doc in friends)
        {
            var other = demoProfiles[doc].Id;
            foreach (var (follower, following) in new[] { (me.Id, other), (other, me.Id) })
            {
                var exists = await db.Follows.AnyAsync(
                    f => f.FollowerProfileId == follower && f.FollowingProfileId == following, ct);
                if (!exists)
                {
                    db.Follows.Add(new Follow
                    {
                        Id = Guid.NewGuid(),
                        FollowerProfileId = follower,
                        FollowingProfileId = following,
                        CreatedAt = DateTime.UtcNow.AddDays(-10),
                    });
                }
            }
        }

        // Siguiendo a Luisa (1 vía).
        var luisa = demoProfiles["1000000005"].Id;
        if (!await db.Follows.AnyAsync(f => f.FollowerProfileId == me.Id && f.FollowingProfileId == luisa, ct))
        {
            db.Follows.Add(new Follow
            {
                Id = Guid.NewGuid(),
                FollowerProfileId = me.Id,
                FollowingProfileId = luisa,
                CreatedAt = DateTime.UtcNow.AddDays(-8),
            });
        }

        // Jorge nos sigue a nosotros.
        var jorge = demoProfiles["1000000004"].Id;
        if (!await db.Follows.AnyAsync(f => f.FollowerProfileId == jorge && f.FollowingProfileId == me.Id, ct))
        {
            db.Follows.Add(new Follow
            {
                Id = Guid.NewGuid(),
                FollowerProfileId = jorge,
                FollowingProfileId = me.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
            });
        }

        // Chat 1:1 con Valentina (si no existe aún).
        var valentina = demoProfiles["1000000001"].Id;
        var hasValentinaChat = await db.Messages.AnyAsync(m => m.ConversationId == null
            && ((m.SenderProfileId == me.Id && m.RecipientProfileId == valentina)
                || (m.SenderProfileId == valentina && m.RecipientProfileId == me.Id)), ct);
        if (!hasValentinaChat)
        {
            var msgs = new[]
            {
                new Message { Id = Guid.NewGuid(), SenderProfileId = valentina, RecipientProfileId = me.Id, Body = "¡Hola! Vi que eres nuevo en la comunidad. ¿Te apuntas a la caminata del sábado? 🚶‍♀️", CreatedAt = DateTime.UtcNow.AddHours(-6) },
                new Message { Id = Guid.NewGuid(), SenderProfileId = valentina, RecipientProfileId = me.Id, Body = "Salimos a las 7 am del parque central. Será un grupo pequeño y muy amable.", CreatedAt = DateTime.UtcNow.AddHours(-5) },
                new Message { Id = Guid.NewGuid(), SenderProfileId = me.Id, RecipientProfileId = valentina, Body = "¡Me apunto! Gracias por la invitación 😊", CreatedAt = DateTime.UtcNow.AddHours(-4) },
                new Message { Id = Guid.NewGuid(), SenderProfileId = valentina, RecipientProfileId = me.Id, Body = "¡Genial! Te esperamos. 🎉", CreatedAt = DateTime.UtcNow.AddHours(-3) },
            };
            db.Messages.AddRange(msgs);
        }

        // Chat 1:1 con Andrés (si no existe aún).
        var andres = demoProfiles["1000000002"].Id;
        var hasAndresChat = await db.Messages.AnyAsync(m => m.ConversationId == null
            && ((m.SenderProfileId == me.Id && m.RecipientProfileId == andres)
                || (m.SenderProfileId == andres && m.RecipientProfileId == me.Id)), ct);
        if (!hasAndresChat)
        {
            var msgs = new[]
            {
                new Message { Id = Guid.NewGuid(), SenderProfileId = andres, RecipientProfileId = me.Id, Body = "¡Hola! Bienvenido a la comunidad. Cualquier duda con la medicación, aquí estamos.", CreatedAt = DateTime.UtcNow.AddHours(-48) },
                new Message { Id = Guid.NewGuid(), SenderProfileId = me.Id, RecipientProfileId = andres, Body = "¡Gracias, Andrés! Me motiva ver gente con tan buen ánimo.", CreatedAt = DateTime.UtcNow.AddHours(-47) },
                new Message { Id = Guid.NewGuid(), SenderProfileId = andres, RecipientProfileId = me.Id, Body = "Eso es lo bonito de este lugar. ¡Nos vemos en la caminata!", CreatedAt = DateTime.UtcNow.AddHours(-46) },
            };
            db.Messages.AddRange(msgs);
        }

        // Membresía en los 3 grupos demo.
        foreach (var seed in Groups)
        {
            var creator = demoProfiles[seed.CreatorDoc];
            var group = await db.ChatGroups
                .FirstOrDefaultAsync(g => g.Name == seed.Name && g.CreatedByProfileId == creator.Id, ct);
            if (group is null) continue;

            var isMember = await db.ChatGroupMembers.AnyAsync(
                m => m.GroupId == group.Id && m.ProfileId == me.Id, ct);
            if (!isMember)
            {
                db.ChatGroupMembers.Add(new ChatGroupMember
                {
                    GroupId = group.Id,
                    ProfileId = me.Id,
                    JoinedAt = DateTime.UtcNow.AddHours(-seed.AgeHours),
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }
}