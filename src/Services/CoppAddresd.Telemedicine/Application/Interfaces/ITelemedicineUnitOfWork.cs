namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Unidad de trabajo con transacción explícita. Permite ejecutar varias
/// escrituras como todo-o-nada (p. ej. procesar un webhook: reservar la clave de
/// idempotencia + aplicar las mutaciones de sala/sesión/cita en la misma
/// transacción). Solo se usa cuando la atomicidad lo exige; una sola escritura
/// no necesita transacción explícita.
/// </summary>
public interface ITelemedicineUnitOfWork
{
    /// <summary>
    /// Ejecuta <paramref name="action"/> dentro de una transacción: commit si no
    /// lanza, rollback si lanza. La acción debe contener SOLO operaciones de BD
    /// (cortas); nunca llamadas externas ni I/O de usuario.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
