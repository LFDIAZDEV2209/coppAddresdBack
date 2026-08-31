using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoppAddresd.Community;

/// <summary>
/// Siembra datos de demostración de la comunidad ERP solo en desarrollo (si la tabla
/// de perfiles está vacía). Genera ~60 perfiles con región/diagnóstico/semana y señales
/// de actividad variadas (para que el riesgo varíe), entradas de XP, publicaciones en
/// todos los tipos/destinos y eventos de feed coherentes. Las publicaciones se extienden
/// por los últimos 30 días con horas variadas, y se incluyen comentarios (con respuestas),
/// likes de publicación y comentarios, seguimientos, reportes y perfiles baneados.
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
        // --- 20 nuevos nombres (60 total) ---
        "Santiago Vega", "Valeria Ortiz", "Emilia Rojas", "Felipe Mora",
        "Ana Beltrán", "Jorge Medina", "Luciano Ferrer", "Paula Herrera",
        "Cristian Salas", "Diana Ponce", "Hugo Campos", "Marta Ibarra",
        "Rodrigo León", "Silvia Marín", "Óscar Vidal", "Claudia Solís",
        "Mario Duarte", "Teresa Aguilar", "Iván Molina", "Lorena Figueroa",
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
        "¡Qué inspirador! Gracias por motivarnos.",
        "Voy a probar eso, gracias por el consejo.",
        "Me pasó igual, ánimo que se puede.",
        "Excelente, ya voy por el día 15.",
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
            // Ya hay datos demo (o reales): no tocar nada al reiniciar el servicio.
            // Para re-sembrar desde cero, truncar manualmente las tablas en desarrollo.
            return;
        }

        var now = DateTime.UtcNow;
        var rnd = new Random(20260826);
        var profiles = new List<Profile>();
        var allPosts = new List<Post>(); // Posts creados, para asignar comentarios/likes

        // ─── PERFILES (~60) ──────────────────────────────────────────────
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

        // ─── PUBLICACIONES (~50 posts, spread 30 días) ────────────────────
        const int postCount = 50;
        var postOwners = profiles.Take(postCount).ToList();
        var postDates = new DateTime[postCount];
        for (var k = 0; k < postCount; k++)
        {
            // Distribuir en 30 días: día = k*30/50, hora variada (8..22)
            var dayOffset = (int)(k * 30L / postCount);
            var hourOffset = 8 + (k * 7) % 15; // 8..22
            postDates[k] = now.AddDays(-dayOffset).AddHours(-hourOffset).AddMinutes(-(k * 13) % 60);
        }

        // Imagen keys reales (community/posts/<filename>).
        var imageKeys = new[]
        {
            "community/posts/141788306d974de39283dc1c1b7d6be1.jpeg",
            "community/posts/203b3324baec47148242a18f6501fd3c.jpeg",
            "community/posts/48c5b15e08124b5baea9707f3551e2f1.jpg",
            "community/posts/4fd61a2d275f4c488b5b84f8a68d915d.png",
            "community/posts/8390ef443c1b47acb727a3b8d687a327.png",
            "community/posts/8d41cb0572234cea84de0ec72abcf868.png",
            "community/posts/9fe5741eec7243ee84f19674ccf57b16.jpg",
            "community/posts/ab779f9dd4ca4aac9e49b1b1eeb6f149.png",
        };

        // Video keys (archivos de referencia; se crearán en otro paso).
        var videoKeys = new[]
        {
            "community/posts/sample1.mp4",
            "community/posts/sample2.mp4",
            "community/posts/sample3.mp4",
        };

        // Textos realistas por tipo de publicación (~13 Texto bodies).
        var textoBodies = new[]
        {
            "Hoy completé mi caminata matutina de 30 minutos. Al principio costaba, pero ahora es lo que más espero del día. ¡Pequeños pasos grandes resultados!",
            "Comparto algo que me funciona: escribir 3 cosas buenas del día antes de dormir. Me bajó el estrés un montón.",
            "Día difícil. La ansiedad me jugó una mala pasada y comí de más. Mañana se empieza de nuevo, sin culpas.",
            "Hoy cumplí 4 semanas midiendo mi presión todos los días. Promedio: 132/85. ¡Bajamos!",
            "Como enfermera jubilada les digo: no dejen de ir a sus controles. La prevención lo es todo.",
            "3 meses sin fumar. Mi presión mejoró muchísimo y mi respiración también. Si estás pensando en dejarlo: ¡hazlo!",
            "Mi médico me cambió el medicamento y tuve mareos los primeros días. ¿Alguien más pasó por eso? Me ayudaría saberlo.",
            "Recuerden que no están solos en este proceso. La constancia también se construye con descanso.",
            "Receta del día: avena con canela y manzana sin azúcar añadida. 15 minutos de preparación y el corazón te lo agradece.",
            "Pregunta rápida: ¿alguien ha probado los ejercicios de respiración de la app? Quiero saber si valen la pena.",
            "Recordatorio del programa: tomen su medicación a la misma hora todos los días. Usar la alarma del celular me ayudó a no fallar nunca más.",
            "Mi progreso de esta semana: 5 caminatas completas, 3 litros de agua diarios y mejor sueño. ¡No me lo creo!",
            "Esto es justo lo que necesitaba leer hoy. Gracias comunidad por tanto apoyo.",
        };

        var imagenBodies = new[]
        {
            "Mi progreso de esta semana: fotos del día 1 y día 28. ¡El cambio es real! 💪",
            "Así se ve mi almuerzo saludable de hoy: ensalada de quinoa con vegetales grillados. ¡Delicioso y nutritivo!",
            "Hoy celebré mis 90 días de racha con esta foto en el parque. ¡Gracias a todos por el apoyo! 🎉",
            "Mi setup de ejercicio en casa. No necesitas gimnasio para cuidarte. 🏠",
            "Resultado de mis análisis de sangre después de 3 meses en el programa. ¡Los números hablan solos!",
        };

        var videoBodies = new[]
        {
            "Mi rutina de ejercicio de hoy — 20 min de cardio en casa. ¡Vamos con todo! 🎬",
            "Tutorial de estiramientos para principiantes. Solo necesitan 10 minutos y una esterilla. 🏋️",
            "Así preparé mi batido verde antiinflamatorio. Super fácil y delicioso. 🥤",
        };

        var encuestaBodies = new[]
        {
            "¿Qué tema te gustaría para el próximo taller de la comunidad?",
            "¿Cuál es tu mayor reto para mantener una alimentación saludable?",
            "¿Qué actividad física disfrutas más?",
            "¿Con qué frecuencia haces ejercicio?",
            "¿Qué te motiva más?",
        };

        var logroBodies = new[]
        {
            "Hoy completé mi primer trote de 30 minutos sin parar. Hace 6 meses no podía subir 2 pisos sin agitarme.",
            "Caminata comunitaria este sábado a las 7 am en el parque central. ¡Nos vemos! 🚶‍♀️",
            "¡Bienvenidos a la Comunidad ANTARES! Este espacio es de todos: comparte tus avances, dudas y recetas.",
            "Hoy cumplí 100 días de racha. Empecé con una caminata de 10 minutos y ahora hago 45. ¡Sigan adelante!",
            "Mi meta del mes: reducir 2 cm de cintura. ¡Logrado en 22 días con caminata y alimentación consciente!",
            "Completé el reto de 30 días de caminata. De 0 a 100 km recorridos este mes. ¡No me lo creo!",
            "Hoy me pesé y bajé 3 kg en las últimas 4 semanas. El combo caminata + comida saludable funciona.",
            "Mi primer mes sin refrescos. La verdad: al principio fue difícil pero ahora no los extraño para nada.",
        };

        var encuestaPollData = new (string Question, string[] Options)[]
        {
            ("¿Qué tema te gustaría para el próximo taller?",
                ["Nutrición y recetas saludables", "Ejercicio para principiantes", "Manejo del estrés", "Control de presión arterial"]),
            ("¿Cuál es tu mayor reto para mantener una alimentación saludable?",
                ["Falta de tiempo para cocinar", "Antojos nocturnos", "No saber qué comer", "Costo de los alimentos saludables"]),
            ("¿Qué actividad física disfrutas más?",
                ["Caminar al aire libre", "Ejercicios en casa", "Yoga o estiramientos", "Natación"]),
            ("¿Con qué frecuencia haces ejercicio?",
                ["Todos los días", "3-4 veces/semana", "1-2 veces/semana", "Casi nunca"]),
            ("¿Qué te motiva más?",
                ["Sentirme mejor", "Bajar de peso", "Mi familia", "Mi salud"]),
        };

        var imageIdx = 0;
        var videoIdx = 0;
        var textoIdx = 0;
        var imagenIdx = 0;
        var encuestaIdx = 0;
        var logroIdx = 0;
        var pinnedOrder = 1;

        for (var k = 0; k < postCount; k++)
        {
            var owner = postOwners[k];
            var type = AllPostTypes[k % AllPostTypes.Length];
            var destination = AllDestinations[k % AllDestinations.Length];
            var created = postDates[k];

            string body;
            string? imageKey = null;

            // Pin los primeros 3 posts para demo.
            var isPinned = k < 3;

            switch (type)
            {
                case PostType.Texto:
                    body = textoBodies[textoIdx % textoBodies.Length];
                    textoIdx++;
                    break;
                case PostType.Imagen:
                    body = imagenBodies[imagenIdx % imagenBodies.Length];
                    imageKey = imageKeys[imageIdx % imageKeys.Length];
                    imageIdx++;
                    imagenIdx++;
                    break;
                case PostType.Video:
                    body = videoBodies[videoIdx % videoBodies.Length];
                    imageKey = videoKeys[videoIdx % videoKeys.Length]; // ImageKey almacena la clave del video
                    videoIdx++;
                    break;
                case PostType.Encuesta:
                    body = encuestaBodies[encuestaIdx % encuestaBodies.Length];
                    encuestaIdx++;
                    break;
                case PostType.Logro:
                    body = logroBodies[logroIdx % logroBodies.Length];
                    logroIdx++;
                    break;
                default:
                    body = $"{owner.DisplayName} compartió algo con la comunidad.";
                    break;
            }

            var post = new Post
            {
                Id = Guid.NewGuid(),
                ProfileId = owner.Id,
                Body = body,
                ImageKey = imageKey,
                Type = type,
                Destination = destination,
                Pinned = isPinned,
                PinnedOrder = isPinned ? pinnedOrder++ : 0,
                CreatedAt = created,
            };
            db.Posts.Add(post);
            allPosts.Add(post);

            // Para posts de Encuesta, crear la Poll con opciones y votos.
            if (type == PostType.Encuesta)
            {
                var pollData = encuestaPollData[(k / AllPostTypes.Length) % encuestaPollData.Length];
                var poll = new Poll
                {
                    Id = Guid.NewGuid(),
                    PostId = post.Id,
                    CreatedAt = created,
                };
                post.Poll = poll;

                for (var pi = 0; pi < pollData.Options.Length; pi++)
                {
                    var option = new PollOption
                    {
                        Id = Guid.NewGuid(),
                        PollId = poll.Id,
                        Text = pollData.Options[pi],
                        Position = pi,
                    };
                    poll.Options.Add(option);

                    // Asignar votos aleatorios a cada opción (3-8 votos por opción).
                    var voteCount = 3 + rnd.Next(6); // 3..8
                    for (var vi = 0; vi < voteCount; vi++)
                    {
                        var voterIdx = rnd.Next(profiles.Count);
                        var voter = profiles[voterIdx];
                        // Evitar duplicados: solo si el perfil aún no votó en esta encuesta.
                        if (!poll.Options.SelectMany(o => o.Votes).Any(v => v.ProfileId == voter.Id))
                        {
                            option.Votes.Add(new PollVote
                            {
                                Id = Guid.NewGuid(),
                                OptionId = option.Id,
                                ProfileId = voter.Id,
                                CreatedAt = created.AddMinutes(30 + rnd.Next(120)),
                            });
                        }
                    }
                }
            }

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

        // ─── COMENTARIOS (~40-60 sobre ~20-25 posts, con respuestas) ─────
        // Los primeros 25 posts reciben 2-3 comentarios cada uno.
        var commentTargets = allPosts.Take(25).ToList();
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

                // ~30% de los comentarios reciben 1-2 respuestas.
                if (ci % 10 < 3 && cj == 0)
                {
                    var replyCount = 1 + rnd.Next(2); // 1 o 2 respuestas
                    for (var ri = 0; ri < replyCount; ri++)
                    {
                        var replierIdx = (ci * 5 + ri + 22) % profiles.Count;
                        var replier = profiles[replierIdx];
                        var replyCreatedAt = createdAt.AddHours(1 + (ri * 3) % 24);

                        var reply = new Comment
                        {
                            Id = Guid.NewGuid(),
                            PostId = targetPost.Id,
                            ProfileId = replier.Id,
                            ParentCommentId = comment.Id,
                            Body = CommentBodies[(ci + cj + ri + 4) % CommentBodies.Length],
                            CreatedAt = replyCreatedAt,
                        };
                        db.Comments.Add(reply);
                        comments.Add(reply);

                        db.FeedEvents.Add(new FeedEvent
                        {
                            Id = Guid.NewGuid(),
                            ProfileId = replier.Id,
                            Kind = FeedEventKind.Comentario,
                            Body = reply.Body.Length > 500 ? reply.Body[..500] : reply.Body,
                            CreatedAt = replyCreatedAt,
                        });
                    }
                }
            }
        }

        // ─── LIKES DE PUBLICACIÓN (~100-150 en ~30 posts) ─────────────────
        // Distribuir ~120 likes entre los primeros 30 posts (8-15 por post).
        var likeTargets = allPosts.Take(30).ToList();
        var usedPostLikeKeys = new HashSet<(Guid PostId, Guid ProfileId)>();
        for (var li = 0; li < 120; li++)
        {
            var targetPost = likeTargets[li % likeTargets.Count];
            var likerIdx = (li * 5 + 7) % profiles.Count;
            var liker = profiles[likerIdx];

            // Evitar self-like y duplicados.
            if (liker.Id == targetPost.ProfileId) continue;
            if (!usedPostLikeKeys.Add((targetPost.Id, liker.Id))) continue;

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

        // ─── LIKES DE COMENTARIO (~40% de comentarios, 1-3 likes cada uno) ──
        var usedCommentLikeKeys = new HashSet<(Guid CommentId, Guid ProfileId)>();
        for (var cli = 0; cli < comments.Count; cli++)
        {
            if (cli % 10 >= 4) continue; // ~40% de los comentarios

            var targetComment = comments[cli];
            var commentLikeCount = 1 + rnd.Next(3); // 1..3
            for (var clii = 0; clii < commentLikeCount; clii++)
            {
                var likerIdx = (cli * 7 + clii + 11) % profiles.Count;
                var liker = profiles[likerIdx];

                // Evitar self-like y duplicados.
                if (liker.Id == targetComment.ProfileId) continue;
                if (!usedCommentLikeKeys.Add((targetComment.Id, liker.Id))) continue;

                var createdAt = targetComment.CreatedAt.AddMinutes(15 + rnd.Next(1200));

                db.Likes.Add(new Like
                {
                    Id = Guid.NewGuid(),
                    ProfileId = liker.Id,
                    CommentId = targetComment.Id,
                    PostId = null,
                    CreatedAt = createdAt,
                });
            }
        }

        // ─── SEGUIMIENTOS (cada perfil sigue 3-5 otros, mutuos cuando sea posible) ──
        var usedFollowKeys = new HashSet<(Guid FollowerId, Guid FollowingId)>();
        for (var fi = 0; fi < profiles.Count; fi++)
        {
            var follower = profiles[fi];
            var followCount = 3 + rnd.Next(3); // 3..5
            var candidates = profiles
                .Where(p => p.Id != follower.Id)
                .OrderBy(_ => rnd.Next())
                .Take(followCount * 2) // Tomar más candidatos para compensar dedup
                .ToList();

            var created = 0;
            foreach (var candidate in candidates)
            {
                if (created >= followCount) break;
                if (!usedFollowKeys.Add((follower.Id, candidate.Id))) continue;

                db.Follows.Add(new Follow
                {
                    Id = Guid.NewGuid(),
                    FollowerProfileId = follower.Id,
                    FollowingProfileId = candidate.Id,
                    CreatedAt = now.AddDays(-rnd.Next(30)),
                });
                created++;

                // Crear follow mutuo a veces (~40%) para habilitar mensajes.
                if (rnd.Next(10) < 4 && usedFollowKeys.Add((candidate.Id, follower.Id)))
                {
                    db.Follows.Add(new Follow
                    {
                        Id = Guid.NewGuid(),
                        FollowerProfileId = candidate.Id,
                        FollowingProfileId = follower.Id,
                        CreatedAt = now.AddDays(-rnd.Next(30)),
                    });
                }
            }
        }

        // ─── REPORTES DE PUBLICACIONES (2-3) ─────────────────────────────
        var reportReasons = new[] { "Spam", "Contenido inapropiado", "Lenguaje ofensivo", "Información falsa" };
        var reportDetails = new[]
        {
            "Este contenido no aporta a la comunidad.",
            "Contiene información médica no verificada.",
            "Lenguaje irrespetuoso hacia otros miembros.",
        };

        for (var rpi = 0; rpi < 3; rpi++)
        {
            var reporterIdx = (rpi + 33) % profiles.Count;
            var reporter = profiles[reporterIdx];
            var reportedPost = allPosts[rpi + 5]; // Posts diferentes a los primeros

            db.PostReports.Add(new PostReport
            {
                Id = Guid.NewGuid(),
                PostId = reportedPost.Id,
                ReportedByProfileId = reporter.Id,
                Reason = reportReasons[rpi % reportReasons.Length],
                Details = reportDetails[rpi % reportDetails.Length],
                CreatedAt = now.AddDays(-rnd.Next(5)),
            });
        }

        // ─── REPORTE DE COMENTARIO (1) ───────────────────────────────────
        if (comments.Count > 0)
        {
            var commentToReport = comments[rnd.Next(Math.Min(10, comments.Count))];
            var commentReporterIdx = (rnd.Next(profiles.Count) + 44) % profiles.Count;
            var commentReporter = profiles[commentReporterIdx];

            db.CommentReports.Add(new CommentReport
            {
                Id = Guid.NewGuid(),
                CommentId = commentToReport.Id,
                ReportedByProfileId = commentReporter.Id,
                Reason = "Contenido inapropiado",
                Details = "El comentario contiene lenguaje ofensivo.",
                CreatedAt = now.AddDays(-rnd.Next(5)),
            });
        }

        // ─── PERFILES BANEADOS (1-2) ─────────────────────────────────────
        var bannedProfile1 = profiles[41]; // "Santiago Vega" (índice nuevo)
        bannedProfile1.Status = ProfileStatus.Banned;
        bannedProfile1.BannedAt = now.AddDays(-2);
        bannedProfile1.BannedBy = profiles[0].Id;
        bannedProfile1.BanReason = "Múltiples reportes";

        if (profiles.Count > 50)
        {
            var bannedProfile2 = profiles[50]; // "Mario Duarte" (índice nuevo)
            bannedProfile2.Status = ProfileStatus.Banned;
            bannedProfile2.BannedAt = now.AddDays(-3);
            bannedProfile2.BannedBy = profiles[0].Id;
            bannedProfile2.BanReason = "Spam recurrente";
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

        // ─── GRUPOS Y CHATS DEMO (si vacío) ─────────────────────────────
        if (!await db.ChatGroups.AnyAsync(ct))
        {
            var groupDefs = new (string Name, int MemberCount, (int Sender, int HoursAgo, string Body)[] Messages)[]
            {
                ("Comunidad ADRED", 0, new (int, int, string)[]
                {
                    (0, 55, "¡Bienvenidos a la comunidad ADRED! 💙"),
                    (1, 48, "Feliz de estar aquí, un saludo a todos."),
                    (2, 40, "Recuerden compartir sus avances de la semana."),
                    (3, 30, "¿Alguien tiene recomendaciones para empezar con el nutriobiótico?"),
                    (4, 20, "Yo empecé hace un mes y me he sentido increíble."),
                    (5, 8, "No se pierdan el en vivo de mañana 👀"),
                }),
                ("Chat ANTARES general", 0, new (int, int, string)[]
                {
                    (6, 52, "Buenos días a toda la comunidad ANTARES ☀️"),
                    (7, 44, "¿Ya vieron el nuevo reto de la app?"),
                    (8, 33, "Yo voy por el día 12 de mi racha 🔥"),
                    (9, 21, "Vamos que se puede, un día a la vez."),
                    (10, 10, "Nos vemos en el en vivo de hoy."),
                }),
                ("Reto caminata 30 días", 12, new (int, int, string)[]
                {
                    (0, 47, "Día 5 completado ✅ ¿Cómo van?"),
                    (1, 36, "Yo ya llevo 8 km hoy."),
                    (2, 25, "El calor está fuerte, pero no me rindo."),
                    (3, 12, "Medio camino, se siente increíble."),
                }),
                ("Apoyo emocional", 12, new (int, int, string)[]
                {
                    (4, 43, "Recuerden que no están solos en este proceso 💙"),
                    (5, 28, "Hoy fue un día difícil, pero gracias por el espacio."),
                    (6, 15, "La constancia también se construye con descanso."),
                }),
                ("Cocina saludable", 12, new (int, int, string)[]
                {
                    (7, 39, "Comparto mi receta de avena overnight sin azúcar 🥣"),
                    (8, 26, "¿Sustitutos del pan que recomienden?"),
                    (9, 14, "Probé la ensalada de la semana pasada, ¡espectacular!"),
                }),
            };

            foreach (var (name, memberCount, messages) in groupDefs)
            {
                var group = new ChatGroup
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    CreatedByProfileId = profiles[0].Id,
                    CreatedAt = now.AddDays(-45),
                };
                db.ChatGroups.Add(group);

                // 0 = todos los perfiles; si no, los primeros N perfiles.
                var members = memberCount == 0
                    ? profiles
                    : profiles.Take(memberCount).ToList();
                foreach (var p in members)
                {
                    db.ChatGroupMembers.Add(new ChatGroupMember
                    {
                        GroupId = group.Id,
                        ProfileId = p.Id,
                        JoinedAt = group.CreatedAt,
                    });
                }

                foreach (var (sender, hoursAgo, body) in messages)
                {
                    db.Messages.Add(new Message
                    {
                        Id = Guid.NewGuid(),
                        SenderProfileId = members[sender % members.Count].Id,
                        RecipientProfileId = null,
                        ConversationId = group.Id,
                        Body = body,
                        CreatedAt = now.AddHours(-hoursAgo),
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
