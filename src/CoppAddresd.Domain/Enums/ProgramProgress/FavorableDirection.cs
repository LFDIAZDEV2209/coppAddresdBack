namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Dirección favorable de una métrica clínica (SPEC §13.1.2): qué sentido del
/// cambio cuenta como "mejoría" para el Índice de Transformación.
/// <c>LowerIsBetter = -1</c> (bajar es mejor: peso, glucosa) y
/// <c>HigherIsBetter = 1</c> (subir es mejor). Se persiste como
/// <c>smallint</c> con CHECK <c>IN (-1, 1)</c>.
/// </summary>
public enum FavorableDirection
{
    LowerIsBetter = -1,
    HigherIsBetter = 1,
}