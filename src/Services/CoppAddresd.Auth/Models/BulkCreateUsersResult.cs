namespace CoppAddresd.Auth.Models;

/// <summary>
/// Resultado de la creación masiva de usuarios.
/// Cada fila incluye su propio resultado independiente.
/// </summary>
public record BulkCreateUsersResult
{
    /// <summary>Resultados individuales por fila.</summary>
    public List<BulkCreateUserRowResult> Results { get; init; } = [];

    /// <summary>Total de usuarios creados exitosamente.</summary>
    public int Created { get; init; }

    /// <summary>Total de filas que fallaron.</summary>
    public int Failed { get; init; }
}

/// <summary>
/// Resultado de una fila individual en la creación masiva.
/// </summary>
public record BulkCreateUserRowResult
{
    /// <summary>Número de línea (1-indexed) en el request original.</summary>
    public int Line { get; init; }

    /// <summary>Indica si la fila se procesó exitosamente.</summary>
    public bool Success { get; init; }

    /// <summary>ID del usuario creado (solo si Success = true).</summary>
    public string? UserId { get; init; }

    /// <summary>Email del usuario (presente en éxito y en error de duplicado).</summary>
    public string? Email { get; init; }

    /// <summary>Contraseña temporal generada (solo si Success = true, retorna una sola vez).</summary>
    public string? TemporaryPassword { get; init; }

    /// <summary>Mensaje de error (solo si Success = false).</summary>
    public string? Error { get; init; }
}
