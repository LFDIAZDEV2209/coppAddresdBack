using System.Security.Cryptography;
using System.Text;

namespace CoppAddresd.Application.Features.Redes;

/// <summary>
/// Validador de CUV (Código Único de Validación) para el autocompleto de la
/// factura. Hoy es un validador determinístico mock: mismo CUV → misma
/// respuesta, prestadores tomados del catálogo de redes habilitadas. Cuando
/// exista la integración real con el validador de la plataforma (MiPres),
/// se implementa la misma interfaz en Infrastructure.
/// </summary>
public interface ICuvValidator
{
    /// <summary>Devuelve la validación del CUV o null si el código es inválido.</summary>
    CuvValidationDto? Validate(string cuv, IReadOnlyList<RedPrestadoraDto> prestadoresHabilitados);
}

public sealed class DeterministicCuvValidator : ICuvValidator
{
    private const int CuvMinimo = 6;

    private static readonly string[] Usuarios =
    [
        "Mariana Restrepo Vélez",
        "Carlos Mendoza Ruiz",
        "Laura Gómez Álvarez",
        "Andrés Felipe Vargas",
    ];

    private static readonly string[] Documentos =
        ["1.038.442.117", "80.012.345", "1.052.773.410", "91.998.712"];

    private static readonly string[] Modalidades = ["Evento", "Capitación", "Pago por servicio"];

    public CuvValidationDto? Validate(string cuv, IReadOnlyList<RedPrestadoraDto> prestadoresHabilitados)
    {
        var value = cuv.Trim();
        if (value.Length < CuvMinimo || prestadoresHabilitados.Count == 0)
        {
            return null;
        }

        // Hash FNV-1a determinista a partir del código: mismo CUV → mismos datos.
        var h = Fnv1a(value);
        var prestador = prestadoresHabilitados[(int)(h % (uint)prestadoresHabilitados.Count)];
        var idx = (int)(h % (uint)Usuarios.Length);
        var modalidad = Modalidades[h % (uint)Modalidades.Length];

        var valorTotal = 480_000 + (int)(h % 420) * 11_500;
        var copago = valorTotal * 0.04m;
        var cuota = valorTotal * 0.015m;
        var hoy = DateTime.UtcNow.Date;
        var expedicion = hoy.AddDays(-(int)(h % 12));
        var inicio = expedicion.AddDays(-20);

        return new CuvValidationDto(
            value,
            prestador.Nit,
            prestador.RazonSocial,
            $"FV-{(h % 90000) + 10000}",
            expedicion.ToString("yyyy-MM-dd"),
            valorTotal,
            copago,
            cuota,
            valorTotal - copago - cuota,
            idx % 2 == 0 ? "cotizante" : "beneficiario",
            Documentos[idx],
            Usuarios[idx],
            $"CTO-{(h % 900) + 100}",
            modalidad,
            modalidad == "Evento" ? "Urgencias y hospitalización" : "Plan Biohacking Integral",
            $"{inicio:yyyy-MM-dd} a {expedicion:yyyy-MM-dd}");
    }

    internal static uint Fnv1a(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value.Trim().ToUpperInvariant());
        uint hash = 0x811C9DC5;
        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= 0x01000193;
        }
        return hash;
    }
}
