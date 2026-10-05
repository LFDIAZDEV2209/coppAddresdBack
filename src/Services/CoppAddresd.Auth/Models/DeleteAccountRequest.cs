using System.ComponentModel.DataAnnotations;

namespace CoppAddresd.Auth.Models;

public record DeleteAccountRequest
{
    /// <summary>Palabra de confirmación explícita ("ELIMINAR") escrita por el usuario.</summary>
    [Required(ErrorMessage = "Confirmation es requerido")]
    public string Confirmation { get; init; } = string.Empty;

    /// <summary>Código de la aplicación desde la que se elimina ("app"). Solo respaldo: manda el aud del token.</summary>
    public string? Application { get; init; }
}
