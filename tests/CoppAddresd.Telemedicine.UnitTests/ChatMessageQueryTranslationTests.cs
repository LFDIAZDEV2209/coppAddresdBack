using CoppAddresd.Telemedicine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// El cursor keyset de chat usa <c>Guid.CompareTo</c> para desempatar mensajes
/// con el mismo instante: sin traducción EF la consulta fallaría en runtime
/// (los fakes en memoria no lo detectan). <c>ToQueryString</c> valida la
/// traducción sin base de datos.
/// </summary>
public class ChatMessageQueryTranslationTests
{
    [Fact]
    public void ListAfter_ConCursorTraduceKeysetSinFallar()
    {
        var options = new DbContextOptionsBuilder<TelemedicineDbContext>()
            .UseNpgsql("Host=localhost;Database=coppaddresd")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new TelemedicineDbContext(options);
        var cursorUtc = DateTime.UtcNow;
        var cursorId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        var sql = db.ChatMessages
            .Where(m => m.AppointmentId == appointmentId)
            .Where(m =>
                m.CreatedAt > cursorUtc
                || (m.CreatedAt == cursorUtc && m.Id.CompareTo(cursorId) > 0))
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Take(50)
            .ToQueryString();

        Assert.Contains("FROM tele.chat_messages", sql);
        Assert.Contains("ORDER BY", sql);
    }
}
