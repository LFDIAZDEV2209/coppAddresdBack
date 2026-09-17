using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoppAddresd.Community;

/// <summary>
/// Siembra de clubes de la comunidad (idempotente, solo si no hay clubes):
/// catálogo de categorías, perfiles demo deterministas, 5 clubes de sistema
/// (antiguos destinos de <see cref="PostDestination"/>, <c>IsSystem=true</c>)
/// y 9 clubes demo espejo del mock de la app/ERP (contrato D1). Cada club demo
/// trae miembros, publicaciones (texto/anuncio/encuesta, públicas y privadas,
/// fijadas/destacadas/programadas), eventos con cupos/lista de espera, lives
/// con chat y solicitudes pendientes. Fechas relativas a una base fija
/// (2026-09-01 UTC) para que las demos sean repetibles.
/// </summary>
public static class ClubSeeder
{
    /// <summary>Base fija de fechas de los datos demo (espejo del mock de los frentes).</summary>
    private static readonly DateTime Base = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>GUIDs deterministas de los perfiles demo (espejo de m-1..m-12 del mock).</summary>
    private static Guid ProfileId(int n) => new($"00000000-0000-0000-0000-{n:000000000000}");

    private static readonly (int N, string Name, ClubMemberRole Role)[] DemoMembers =
    [
        (2, "María Fernanda Rojas", ClubMemberRole.Admin),
        (3, "Carlos Andrés Pardo", ClubMemberRole.Moderador),
        (4, "Luisa Martínez", ClubMemberRole.Miembro),
        (5, "Andrés Felipe Gil", ClubMemberRole.Miembro),
        (6, "Valentina Ospina", ClubMemberRole.Miembro),
        (7, "Jorge Iván Salazar", ClubMemberRole.Miembro),
        (8, "Daniela Cárdenas", ClubMemberRole.Miembro),
        (9, "Ricardo Peña", ClubMemberRole.Miembro),
        (10, "Sofía Herrera", ClubMemberRole.Miembro),
        (11, "Manuel Estrada", ClubMemberRole.Miembro),
    ];

    private static readonly (string Name, string Slug, string Icon)[] Categories =
    [
        ("Salud", "salud", "🫀"),
        ("Deporte", "deporte", "🏃"),
        ("Bienestar", "bienestar", "🌿"),
        ("Nutrición", "nutricion", "🥗"),
        ("Embarazo", "embarazo", "🤰"),
        ("Diabetes", "diabetes", "🩸"),
        ("Adultos mayores", "adultos-mayores", "👵"),
        ("Empresas", "empresas", "🏢"),
        ("Hobbies", "hobbies", "📷"),
    ];

    public static async Task SeedAsync(CommunityDbContext db, IConfiguration? configuration = null, CancellationToken ct = default)
    {
        if (await db.Clubs.AnyAsync(c => c.Slug == "caminantes-adres", ct))
        {
            // Los clubes demo ya están sembrados; no duplicar. Los clubes reales
            // creados desde el ERP se preservan (la guarda es por slug demo).
            return;
        }

        // ─── CATÁLOGO DE CATEGORÍAS ─────────────────────────────────────
        foreach (var (name, slug, icon) in Categories)
        {
            if (!await db.ClubCategories.AnyAsync(c => c.Slug == slug, ct))
            {
                db.ClubCategories.Add(new ClubCategory { Id = Guid.NewGuid(), Name = name, Slug = slug, Icon = icon });
            }
        }

        // ─── PERFILES DEMO (deterministas) ─────────────────────────────
        // "Equipo ANTARES" es el perfil de sistema (ya lo crea CommunitySeeder);
        // el resto se busca por nombre y se crea con GUID fijo si no existe.
        var systemProfile = await db.Profiles.FirstOrDefaultAsync(p => p.IsSystem, ct)
            ?? throw new InvalidOperationException(
                "ClubSeeder requiere el perfil de sistema 'Equipo Copp Adresd' (lo crea CommunitySeeder).");

        var byName = await db.Profiles
            .Where(p => DemoMembers.Select(d => d.Name).Contains(p.DisplayName))
            .ToDictionaryAsync(p => p.DisplayName, ct);

        var demoProfiles = new Dictionary<int, Profile>();
        foreach (var (n, name, _) in DemoMembers)
        {
            if (byName.TryGetValue(name, out var existing))
            {
                demoProfiles[n] = existing;
                continue;
            }
            var profile = new Profile
            {
                Id = ProfileId(n),
                UserId = Guid.NewGuid(),
                DisplayName = name,
                Status = ProfileStatus.Active,
                CreatedAt = Base.AddMinutes(-600_000),
            };
            db.Profiles.Add(profile);
            demoProfiles[n] = profile;
        }

        var applicant = await FindOrCreateApplicantAsync(db, 12, "Nicolás Torres", ct);
        var applicant2 = await FindOrCreateApplicantAsync(db, 13, "Julián Castaño", ct);
        var applicant3 = await FindOrCreateApplicantAsync(db, 14, "Paula Restrepo", ct);

        // ─── CLUBES DE SISTEMA (antiguos PostDestination) ──────────────
        SeedSystemClub(db, systemProfile, "comunidad-adres", "Comunidad ADRES",
            "Comunidad oficial del programa COPP-ADRESD: anuncios, información y acompañamiento general.",
            "Salud", ClubVisibility.Publico, 5000);
        SeedSystemClub(db, systemProfile, "reto-caminata", "Reto Caminata",
            "Club del reto de caminata: 10.000 pasos diarios, retos mensuales y grupos por ciudad.",
            "Deporte", ClubVisibility.Publico, 2000);
        SeedSystemClub(db, systemProfile, "apoyo-emocional", "Apoyo Emocional",
            "Espacio seguro de acompañamiento emocional entre miembros, con moderación activa.",
            "Bienestar", ClubVisibility.Privado, 1000);
        SeedSystemClub(db, systemProfile, "cocina-saludable", "Cocina Saludable",
            "Recetas, intercambio de menús y consejos de alimentación saludable.",
            "Nutrición", ClubVisibility.Publico, 2000);
        SeedSystemClub(db, systemProfile, "solo-inactivos", "Solo Inactivos",
            "Club de reactivación para miembros con baja actividad: retos suaves y acompañamiento.",
            "Bienestar", ClubVisibility.Privado, 1000);

        // ─── CLUBES DEMO (espejo del mock de los frentes) ──────────────
        SeedCaminantesAdres(db, systemProfile, demoProfiles, applicant);
        SeedNutricionInteligente(db, systemProfile, demoProfiles, applicant2);
        SeedDiabetesEnControl(db, systemProfile, demoProfiles, applicant3);
        SeedEmbarazoPlena(db, systemProfile, demoProfiles);
        SeedBienestarEmpresarial(db, systemProfile, demoProfiles);
        SeedAdultosMayores(db, systemProfile, demoProfiles);
        SeedSaludMental(db, systemProfile, demoProfiles);
        SeedFotografia(db, systemProfile, demoProfiles);
        SeedReto10K(db, systemProfile, demoProfiles);

        await db.SaveChangesAsync(ct);
    }

    private static void SeedSystemClub(CommunityDbContext db, Profile system, string slug, string name, string description, string category, ClubVisibility visibility, int maxMembers)
    {
        db.Clubs.Add(new Club
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = name,
            Description = description,
            Rules = ["Respeto ante todo", "No compartir datos médicos de terceros"],
            Objectives = ["Acompañar a los miembros del programa", "Crear comunidad alrededor del bienestar"],
            Category = category,
            Tags = [],
            Visibility = visibility,
            MaxMembers = maxMembers,
            CreatedByProfileId = system.Id,
            IsSystem = true,
            CreatedAt = Base.AddMinutes(-60_000),
        });
    }

    /// <summary>Club demo más completo: anuncio fijado, imagen destacada, encuesta, post privado y programado, 2 eventos, 1 live activo y solicitud pendiente.</summary>
    private static void SeedCaminantesAdres(CommunityDbContext db, Profile system, Dictionary<int, Profile> p, Profile applicant)
    {
        var club = NewClub("caminantes-adres", "Caminantes ADRES", "Deporte", ClubVisibility.Publico, 500,
            "Club para quienes quieren moverse más: caminatas grupales, retos de pasos y acompañamiento entre miembros.",
            ["Respeto ante todo", "No compartir datos médicos de terceros", "Participa al menos una vez al mes"],
            ["Fomentar 10.000 pasos diarios", "Crear grupos de caminata por ciudad"],
            ["caminata", "pasos", "reto", "grupal"], system.Id, Base.AddMinutes(-60_000));
        db.Clubs.Add(club);

        AddMembers(db, club, system, p, joinedMinutesAgo: [60_000, 30_000, 20_000, 15_000, 14_000, 13_000, 12_000, 10_000, 9_000, 8_000]);

        // Solicitud pendiente de Nicolás Torres.
        db.ClubMembers.Add(new ClubMember
        {
            ClubId = club.Id,
            ProfileId = applicant.Id,
            Role = ClubMemberRole.Miembro,
            Status = ClubMemberStatus.Pendiente,
            JoinedAt = Base.AddMinutes(-120),
        });

        // Anuncio fijado.
        var anuncio = AddPost(db, club, p[2], "¡Nuevo reto de septiembre! 300.000 pasos en el mes. ¿Quién se apunta? 💪",
            PostType.Texto, ClubPostVisibility.Publico, pinned: true, minutesAgo: 60, featured: false, Base, null);
        AddLikes(db, anuncio, [p[2], p[3], p[4], p[5], p[6], p[7], p[8], p[9], p[10], p[11], system]);

        // Imagen destacada.
        var imagen = AddPost(db, club, p[3], "Fotos de la caminata del domingo en el parque Simón Bolívar. ¡Gracias a los 45 asistentes!",
            PostType.Imagen, ClubPostVisibility.Publico, pinned: false, minutesAgo: 55, featured: true, Base, null);
        AddLikes(db, imagen, [system, p[2], p[4], p[6], p[9]]);

        // Encuesta.
        var encuesta = AddPost(db, club, p[2], "Encuesta: ¿qué día prefieres para las caminatas grupales?",
            PostType.Encuesta, ClubPostVisibility.Publico, pinned: false, minutesAgo: 45, featured: false, Base, null);
        var poll = new Poll { Id = Guid.NewGuid(), PostId = encuesta.Id, CreatedAt = Base.AddMinutes(-45) };
        var optSabado = new PollOption { Id = Guid.NewGuid(), PollId = poll.Id, Text = "Sábado", Position = 0 };
        var optDomingo = new PollOption { Id = Guid.NewGuid(), PollId = poll.Id, Text = "Domingo", Position = 1 };
        db.Polls.Add(poll);
        db.PollOptions.AddRange(optSabado, optDomingo);
        db.PollVotes.AddRange(
            new PollVote { Id = Guid.NewGuid(), OptionId = optSabado.Id, ProfileId = p[5].Id, CreatedAt = Base.AddMinutes(-40) },
            new PollVote { Id = Guid.NewGuid(), OptionId = optSabado.Id, ProfileId = p[6].Id, CreatedAt = Base.AddMinutes(-40) },
            new PollVote { Id = Guid.NewGuid(), OptionId = optSabado.Id, ProfileId = p[7].Id, CreatedAt = Base.AddMinutes(-40) },
            new PollVote { Id = Guid.NewGuid(), OptionId = optDomingo.Id, ProfileId = p[2].Id, CreatedAt = Base.AddMinutes(-39) },
            new PollVote { Id = Guid.NewGuid(), OptionId = optDomingo.Id, ProfileId = p[3].Id, CreatedAt = Base.AddMinutes(-39) });

        // Privado (solo miembros).
        AddPost(db, club, p[3], "Guía de calentamiento antes de caminar (solo miembros)",
            PostType.Texto, ClubPostVisibility.Privado, pinned: false, minutesAgo: 30, featured: false, Base, null);

        // Programado.
        AddPost(db, club, p[2], "Publicación programada: resultados del reto de agosto",
            PostType.Texto, ClubPostVisibility.Publico, pinned: false, minutesAgo: 0, featured: false, Base,
            ClubPostStatus.Programado, Base.AddMinutes(1_440));

        // Evento virtual con cupo casi lleno.
        db.ClubEvents.Add(new ClubEvent
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Title = "Charla: cómo iniciar la caminata sin lesiones",
            Description = "Sesión virtual con la fisioterapeuta del programa sobre calentamiento y ritmo.",
            Type = ClubEventType.Virtual,
            StartsAt = Base.AddMinutes(2_880),
            EndsAt = Base.AddMinutes(3_240),
            MeetingUrl = "https://meet.coppaddresd.com/caminantes",
            MaxAttendees = 50,
            Status = ClubEventStatus.Abierto,
            ConfirmedCount = 45,
            WaitlistCount = 4,
            CreatedAt = Base.AddMinutes(-720),
        });

        // Evento presencial pasado (finalizado).
        db.ClubEvents.Add(new ClubEvent
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Title = "Caminata dominical en el parque Simón Bolívar",
            Description = "Salida grupal de 5 km con puntos de hidratación.",
            Type = ClubEventType.Presencial,
            StartsAt = Base.AddMinutes(-720),
            EndsAt = Base.AddMinutes(-600),
            Location = "Parque Simón Bolívar, Bogotá",
            MaxAttendees = 60,
            Status = ClubEventStatus.Finalizado,
            ConfirmedCount = 45,
            WaitlistCount = 0,
            CreatedAt = Base.AddMinutes(-4_320),
        });

        // Live activo con chat y ponentes.
        var live = new LiveSession
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Title = "Calentamiento en vivo para el reto de septiembre",
            ScheduledStartAt = Base.AddMinutes(-30),
            Status = LiveSessionStatus.Activo,
            CreatedAt = Base.AddMinutes(-720),
        };
        db.LiveSessions.Add(live);
        db.LiveSessionSpeakers.AddRange(
            new LiveSessionSpeaker { LiveSessionId = live.Id, ProfileId = p[2].Id },
            new LiveSessionSpeaker { LiveSessionId = live.Id, ProfileId = p[3].Id });
        db.LiveChatMessages.AddRange(
            new LiveChatMessage { Id = Guid.NewGuid(), LiveSessionId = live.Id, SenderProfileId = p[4].Id, Body = "¡Hola a todos! Listos para empezar 🔥", SentAt = Base.AddMinutes(-25) },
            new LiveChatMessage { Id = Guid.NewGuid(), LiveSessionId = live.Id, SenderProfileId = p[5].Id, Body = "Ya voy en el calentamiento, genial", SentAt = Base.AddMinutes(-20) },
            new LiveChatMessage { Id = Guid.NewGuid(), LiveSessionId = live.Id, SenderProfileId = p[8].Id, Body = "¿Cuántos pasos llevan hoy?", SentAt = Base.AddMinutes(-10) });

        // Notificación demo (live programado) y log de moderación (aprobación).
        db.ClubNotifications.Add(new ClubNotification
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            ProfileId = p[4].Id,
            Type = ClubNotificationType.LiveProgramado,
            Payload = $"{{\"liveId\":\"{live.Id}\"}}",
            CreatedAt = Base.AddMinutes(-700),
        });
        db.ModerationLogs.Add(new ModerationLog
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            ActorProfileId = p[2].Id,
            TargetProfileId = applicant.Id,
            Action = "SolicitudAprobada",
            CreatedAt = Base.AddMinutes(-110),
        });

        // Ejemplos de moderación: un comentario con su reporte y un reporte de post.
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            PostId = anuncio.Id,
            ProfileId = p[5].Id,
            Body = "Me apunto, ¡llevo 12.000 pasos hoy!",
            CreatedAt = Base.AddMinutes(-50),
        };
        db.Comments.Add(comment);
        db.CommentReports.Add(new CommentReport
        {
            Id = Guid.NewGuid(),
            CommentId = comment.Id,
            ReportedByProfileId = p[8].Id,
            Reason = "Lenguaje ofensivo",
            CreatedAt = Base.AddMinutes(-40),
        });
        db.PostReports.Add(new PostReport
        {
            Id = Guid.NewGuid(),
            PostId = anuncio.Id,
            ReportedByProfileId = p[9].Id,
            Reason = "Información engañosa sobre el reto",
            CreatedAt = Base.AddMinutes(-25),
        });
    }

    private static void SeedNutricionInteligente(CommunityDbContext db, Profile system, Dictionary<int, Profile> p, Profile applicant)
    {
        var club = NewClub("nutricion-inteligente", "Nutrición Inteligente", "Nutrición", ClubVisibility.Publico, 400,
            "Aprende a comer mejor con guías prácticas, recetas y seguimiento nutricional entre miembros.",
            ["Respeto ante todo", "No dar consejos médicos sin sustento"],
            ["Mejorar hábitos alimentarios", "Compartir recetas saludables"],
            ["recetas", "nutrición", "hábitos"], system.Id, Base.AddMinutes(-55_000));
        db.Clubs.Add(club);
        AddMembers(db, club, system, p, [55_000, 25_000, 18_000, 12_000, 11_000, 10_000, 9_000, 8_000, 7_000, 6_000]);

        AddPost(db, club, p[2], "Guía de la semana: plato saludable 50/25/25 🥗",
            PostType.Texto, ClubPostVisibility.Publico, pinned: true, minutesAgo: 50, featured: false, Base, null);
        AddPost(db, club, p[3], "Receta: avena nocturna con frutas (solo miembros)",
            PostType.Texto, ClubPostVisibility.Privado, pinned: false, minutesAgo: 20, featured: false, Base, null);

        // Solicitud pendiente de Julián Castaño.
        db.ClubMembers.Add(new ClubMember
        {
            ClubId = club.Id,
            ProfileId = applicant.Id,
            Role = ClubMemberRole.Miembro,
            Status = ClubMemberStatus.Pendiente,
            JoinedAt = Base.AddMinutes(-90),
        });

        db.ClubEvents.Add(new ClubEvent
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Title = "Taller virtual: etiquetas nutricionales sin letra pequeña",
            Description = "Aprende a leer las etiquetas de los productos del supermercado.",
            Type = ClubEventType.Virtual,
            StartsAt = Base.AddMinutes(4_320),
            EndsAt = Base.AddMinutes(4_680),
            MeetingUrl = "https://meet.coppaddresd.com/nutricion",
            MaxAttendees = 80,
            Status = ClubEventStatus.Abierto,
            ConfirmedCount = 62,
            WaitlistCount = 0,
            CreatedAt = Base.AddMinutes(-500),
        });
    }

    private static void SeedEmbarazoPlena(CommunityDbContext db, Profile system, Dictionary<int, Profile> p)
    {
        var club = NewClub("embarazo-plena", "Embarazo Plena", "Embarazo", ClubVisibility.Privado, 300,
            "Acompañamiento para futuras mamás: nutrición, actividad segura y experiencias compartidas.",
            ["Respeto ante todo", "Espacio confidencial"],
            ["Acompañar el embarazo", "Compartir experiencias seguras"],
            ["embarazo", "maternidad", "bienestar"], system.Id, Base.AddMinutes(-50_000));
        db.Clubs.Add(club);
        AddMembers(db, club, system, p, [50_000, 22_000, 16_000, 13_000, 10_000, 9_000, 8_000, 7_000, 6_000, 5_000]);

        AddPost(db, club, p[2], "Semana 20: qué esperar y ejercicios seguros",
            PostType.Texto, ClubPostVisibility.Publico, pinned: false, minutesAgo: 40, featured: true, Base, null);
        AddPost(db, club, p[3], "Preguntas frecuentes del segundo trimestre",
            PostType.Texto, ClubPostVisibility.Privado, pinned: false, minutesAgo: 15, featured: false, Base, null);

        db.ClubEvents.Add(new ClubEvent
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Title = "Encuentro presencial: yoga prenatal",
            Description = "Sesión guiada de yoga prenatal en el centro comunitario.",
            Type = ClubEventType.Presencial,
            StartsAt = Base.AddMinutes(5_760),
            EndsAt = Base.AddMinutes(6_120),
            Location = "Centro comunitario, Cali",
            MaxAttendees = 30,
            Status = ClubEventStatus.Abierto,
            ConfirmedCount = 24,
            WaitlistCount = 0,
            CreatedAt = Base.AddMinutes(-400),
        });
    }

    private static void SeedDiabetesEnControl(CommunityDbContext db, Profile system, Dictionary<int, Profile> p, Profile applicant)
    {
        var club = NewClub("diabetes-en-control", "Diabetes en Control", "Diabetes", ClubVisibility.Publico, 500,
            "Comunidad para personas con diabetes: educación, recetas y control de glucemia entre pares.",
            ["Respeto ante todo", "No compartir datos médicos de terceros", "Consultar siempre a tu equipo de salud"],
            ["Educar sobre el manejo de la diabetes", "Compartir experiencias de control"],
            ["diabetes", "glucemia", "educación"], system.Id, Base.AddMinutes(-45_000));
        db.Clubs.Add(club);
        AddMembers(db, club, system, p, [45_000, 21_000, 15_000, 12_000, 10_000, 9_000, 8_000, 7_000, 6_000, 5_000]);

        AddPost(db, club, p[2], "Recordatorio: semana del pie diabético, revisa tus pies hoy",
            PostType.Texto, ClubPostVisibility.Publico, pinned: true, minutesAgo: 35, featured: false, Base, null);
        AddPost(db, club, p[3], "Mitos y verdades sobre la insulina",
            PostType.Texto, ClubPostVisibility.Privado, pinned: false, minutesAgo: 10, featured: false, Base, null);

        // Solicitud pendiente de Paula Restrepo.
        db.ClubMembers.Add(new ClubMember
        {
            ClubId = club.Id,
            ProfileId = applicant.Id,
            Role = ClubMemberRole.Miembro,
            Status = ClubMemberStatus.Pendiente,
            JoinedAt = Base.AddMinutes(-60),
        });

        db.ClubEvents.Add(new ClubEvent
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Title = "Charla: glucosa en ayunas vs postprandial",
            Description = "Educación sobre la medición y metas de glucemia.",
            Type = ClubEventType.Virtual,
            StartsAt = Base.AddMinutes(7_200),
            EndsAt = Base.AddMinutes(7_560),
            MeetingUrl = "https://meet.coppaddresd.com/diabetes",
            MaxAttendees = 100,
            Status = ClubEventStatus.Abierto,
            ConfirmedCount = 88,
            WaitlistCount = 0,
            CreatedAt = Base.AddMinutes(-300),
        });
    }

    private static void SeedBienestarEmpresarial(CommunityDbContext db, Profile system, Dictionary<int, Profile> p)
    {
        var club = NewClub("bienestar-empresarial", "Bienestar Empresarial", "Empresas", ClubVisibility.Privado, 200,
            "Bienestar corporativo: pausas activas, retos de equipo y salud mental en el trabajo.",
            ["Respeto ante todo", "Canal exclusivo de empleados"],
            ["Fomentar pausas activas", "Retos de bienestar por equipos"],
            ["empresas", "bienestar", "equipos"], system.Id, Base.AddMinutes(-40_000));
        db.Clubs.Add(club);
        AddMembers(db, club, system, p, [40_000, 20_000, 14_000, 11_000, 10_000, 9_000, 8_000, 7_000, 6_000, 5_000]);

        AddPost(db, club, p[2], "Reto de pausas activas: 3 veces al día esta semana",
            PostType.Texto, ClubPostVisibility.Publico, pinned: false, minutesAgo: 25, featured: true, Base, null);

        db.ClubEvents.Add(new ClubEvent
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Title = "Sesión virtual: manejo del estrés laboral",
            Description = "Herramientas prácticas de respiración y gestión del tiempo.",
            Type = ClubEventType.Virtual,
            StartsAt = Base.AddMinutes(8_640),
            EndsAt = Base.AddMinutes(9_000),
            MeetingUrl = "https://meet.coppaddresd.com/empresas",
            MaxAttendees = 50,
            Status = ClubEventStatus.Abierto,
            ConfirmedCount = 31,
            WaitlistCount = 0,
            CreatedAt = Base.AddMinutes(-250),
        });
    }

    private static void SeedAdultosMayores(CommunityDbContext db, Profile system, Dictionary<int, Profile> p)
    {
        var club = NewClub("adultos-mayores-activos", "Adultos Mayores Activos", "Adultos mayores", ClubVisibility.Publico, 300,
            "Actividad física y social adaptada para mayores: caminatas suaves, juegos de memoria y compañía.",
            ["Respeto ante todo", "Avisar al moderador si algo incomoda"],
            ["Mantenerse activos", "Compartir y socializar"],
            ["mayores", "actividad", "memoria"], system.Id, Base.AddMinutes(-35_000));
        db.Clubs.Add(club);
        AddMembers(db, club, system, p, [35_000, 19_000, 13_000, 12_000, 10_000, 9_000, 8_000, 7_000, 6_000, 5_000]);

        AddPost(db, club, p[2], "Rutina de movilidad para empezar el día (5 minutos)",
            PostType.Video, ClubPostVisibility.Publico, pinned: false, minutesAgo: 20, featured: true, Base, null);

        db.ClubEvents.Add(new ClubEvent
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Title = "Encuentro presencial: tarde de juegos de memoria",
            Description = "Bingo, memoria y café de mediodía en el salón comunitario.",
            Type = ClubEventType.Presencial,
            StartsAt = Base.AddMinutes(10_080),
            EndsAt = Base.AddMinutes(10_620),
            Location = "Salón comunitario, Medellín",
            MaxAttendees = 40,
            Status = ClubEventStatus.Abierto,
            ConfirmedCount = 17,
            WaitlistCount = 0,
            CreatedAt = Base.AddMinutes(-200),
        });
    }

    private static void SeedSaludMental(CommunityDbContext db, Profile system, Dictionary<int, Profile> p)
    {
        var club = NewClub("salud-mental-conversa", "Salud Mental Conversa", "Bienestar", ClubVisibility.Privado, 400,
            "Espacio seguro para conversar sobre salud mental con acompañamiento y recursos.",
            ["Confidencialidad absoluta", "No juzgar", "El moderador puede expulsar por faltas graves"],
            ["Reducir el estigma", "Ofrecer herramientas de autocuidado"],
            ["salud mental", "autocuidado", "apoyo"], system.Id, Base.AddMinutes(-30_000));
        db.Clubs.Add(club);
        AddMembers(db, club, system, p, [30_000, 18_000, 12_000, 11_000, 10_000, 9_000, 8_000, 7_000, 6_000, 5_000]);

        AddPost(db, club, p[2], "Recurso de la semana: respiración 4-7-8 en 3 minutos",
            PostType.Texto, ClubPostVisibility.Publico, pinned: true, minutesAgo: 15, featured: false, Base, null);
        AddPost(db, club, p[3], "Círculo de escucha: agenda tu espacio esta semana",
            PostType.Texto, ClubPostVisibility.Privado, pinned: false, minutesAgo: 5, featured: false, Base, null);
    }

    private static void SeedFotografia(CommunityDbContext db, Profile system, Dictionary<int, Profile> p)
    {
        var club = NewClub("fotografia-de-ciudad", "Fotografía de Ciudad", "Hobbies", ClubVisibility.Publico, 200,
            "Captura la ciudad: retos semanales de fotografía, técnica y salidas grupales.",
            ["Respeto ante todo", "Publica fotos propias"],
            ["Mejorar técnica fotográfica", "Documentar la ciudad"],
            ["fotografía", "hobby", "ciudad"], system.Id, Base.AddMinutes(-25_000));
        db.Clubs.Add(club);
        AddMembers(db, club, system, p, [25_000, 17_000, 11_000, 10_000, 9_000, 8_000, 7_000, 6_000, 5_000]);

        AddPost(db, club, p[2], "Reto de la semana: luz dorada al atardecer 🌇",
            PostType.Texto, ClubPostVisibility.Publico, pinned: false, minutesAgo: 12, featured: true, Base, null);

        db.ClubEvents.Add(new ClubEvent
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Title = "Salida fotográfica: centro histórico",
            Description = "Caminata fotográfica guiada por el centro de la ciudad.",
            Type = ClubEventType.Presencial,
            StartsAt = Base.AddMinutes(11_520),
            EndsAt = Base.AddMinutes(12_240),
            Location = "Plaza Mayor, punto de encuentro",
            MaxAttendees = 20,
            Status = ClubEventStatus.Abierto,
            ConfirmedCount = 12,
            WaitlistCount = 0,
            CreatedAt = Base.AddMinutes(-150),
        });
    }

    private static void SeedReto10K(CommunityDbContext db, Profile system, Dictionary<int, Profile> p)
    {
        var club = NewClub("reto-10k-pasos", "Reto 10K Pasos", "Deporte", ClubVisibility.Publico, 1000,
            "El reto grande: 10.000 pasos diarios con rankings semanales y medallas por racha.",
            ["Respeto ante todo", "Los rankings se actualizan cada lunes"],
            ["10.000 pasos diarios", "Rankings y medallas"],
            ["pasos", "reto", "ranking"], system.Id, Base.AddMinutes(-20_000));
        db.Clubs.Add(club);
        AddMembers(db, club, system, p, [20_000, 16_000, 10_000, 9_000, 8_000, 7_000, 6_000, 5_000]);

        AddPost(db, club, p[2], "Ranking de la semana: ¡María lidera con 78.000 pasos! 🏅",
            PostType.Texto, ClubPostVisibility.Publico, pinned: true, minutesAgo: 8, featured: false, Base, null);

        db.LiveSessions.Add(new LiveSession
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Title = "Entrenamiento guiado: cómo llegar a los 10K",
            ScheduledStartAt = Base.AddMinutes(1_800),
            Status = LiveSessionStatus.Programado,
            CreatedAt = Base.AddMinutes(-120),
        });
    }

    // ─── HELPERS ──────────────────────────────────────────────────────────

    /// <summary>Perfil solicitante demo (GUID determinista) reutilizado entre corridas.</summary>
    private static async Task<Profile> FindOrCreateApplicantAsync(CommunityDbContext db, int n, string name, CancellationToken ct)
    {
        var existing = await db.Profiles.FirstOrDefaultAsync(p => p.Id == ProfileId(n) || p.DisplayName == name, ct);
        if (existing is not null) return existing;

        var profile = new Profile
        {
            Id = ProfileId(n),
            UserId = Guid.NewGuid(),
            DisplayName = name,
            Status = ProfileStatus.Active,
            CreatedAt = Base.AddMinutes(-600_000),
        };
        db.Profiles.Add(profile);
        return profile;
    }

    private static Club NewClub(string slug, string name, string category, ClubVisibility visibility, int maxMembers,
        string description, string[] rules, string[] objectives, string[] tags, Guid createdBy, DateTime createdAt)
        => new()
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            Name = name,
            Description = description,
            Rules = rules,
            Objectives = objectives,
            Category = category,
            Tags = tags,
            Visibility = visibility,
            MaxMembers = maxMembers,
            CreatedByProfileId = createdBy,
            CreatedAt = createdAt,
        };

    private static void AddMembers(CommunityDbContext db, Club club, Profile system, Dictionary<int, Profile> p, int[] joinedMinutesAgo)
    {
        // "Equipo ANTARES" (perfil de sistema) es Admin de todos los clubes demo (espejo de m-1 del mock).
        db.ClubMembers.Add(new ClubMember
        {
            ClubId = club.Id,
            ProfileId = system.Id,
            Role = ClubMemberRole.Admin,
            Status = ClubMemberStatus.Activo,
            JoinedAt = Base.AddMinutes(-60_000),
        });

        var roles = new[] { ClubMemberRole.Admin, ClubMemberRole.Moderador };
        for (var i = 0; i < joinedMinutesAgo.Length; i++)
        {
            var profile = p[i + 2]; // m-2..m-11
            db.ClubMembers.Add(new ClubMember
            {
                ClubId = club.Id,
                ProfileId = profile.Id,
                Role = i < roles.Length ? roles[i] : ClubMemberRole.Miembro,
                Status = ClubMemberStatus.Activo,
                JoinedAt = Base.AddMinutes(-joinedMinutesAgo[i]),
            });
        }
    }

    private static Post AddPost(CommunityDbContext db, Club club, Profile author, string body, PostType type,
        ClubPostVisibility visibility, bool pinned, int minutesAgo, bool featured, DateTime baseDate,
        ClubPostStatus? status = null, DateTime? scheduledFor = null)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            ProfileId = author.Id,
            ClubId = club.Id,
            Body = body,
            Type = type,
            ClubVisibility = visibility,
            ClubStatus = status ?? ClubPostStatus.Publicado,
            Featured = featured,
            ScheduledFor = scheduledFor,
            Pinned = pinned,
            CreatedAt = baseDate.AddMinutes(-minutesAgo),
        };
        db.Posts.Add(post);
        return post;
    }

    private static void AddLikes(CommunityDbContext db, Post post, Profile[] profiles)
    {
        foreach (var profile in profiles)
        {
            db.Likes.Add(new Like { Id = Guid.NewGuid(), ProfileId = profile.Id, PostId = post.Id, CreatedAt = post.CreatedAt.AddMinutes(1) });
        }
    }
}