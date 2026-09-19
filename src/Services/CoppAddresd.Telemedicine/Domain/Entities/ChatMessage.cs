namespace CoppAddresd.Telemedicine.Domain.Entities;

/// <summary>
/// Mensaje del chat clínico de una consulta de telemedicina (F3). Persiste el
/// texto plano enviado por un participante autorizado (profesional, paciente o
/// supervisor); el emisor y su rol se derivan del JWT en el servidor, nunca del
/// cuerpo de la petición. Sin edición, borrado ni adjuntos en F3.
/// </summary>
public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Cita a la que pertenece el mensaje (referencia débil).</summary>
    public Guid AppointmentId { get; set; }

    /// <summary>Usuario de <c>auth.users</c> que envió el mensaje (identidad del JWT).</summary>
    public Guid SenderUserId { get; set; }

    /// <summary>Rol del emisor derivado server-side: Professional | Patient | Supervisor.</summary>
    public string SenderRole { get; set; } = default!;

    /// <summary>Texto plano del mensaje (máximo 2000 caracteres).</summary>
    public string Body { get; set; } = default!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
