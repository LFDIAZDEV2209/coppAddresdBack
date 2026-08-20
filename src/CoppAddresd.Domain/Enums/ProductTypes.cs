namespace CoppAddresd.Domain.Enums;

/// <summary>
/// Tipos de producto en el inventario. Combina productos clínicos y bienestar.
/// Almacena como string en la BD (máx 40 chars) para simplicidad de queries.
/// </summary>
public static class ProductTypes
{
    public const string Medicamento = "Medicamento";
    public const string InsumoMedico = "Insumo médico";
    public const string MaterialHospitalario = "Material hospitalario";
    public const string ProductoFarmacia = "Producto de farmacia";
    public const string AlimentoSaludable = "Alimento saludable";
    public const string SnackSaludable = "Snack saludable";
    public const string Bebida = "Bebida";
    public const string Suplemento = "Suplemento";
    public const string DispositivoSalud = "Dispositivo de salud";
    public const string EquipamientoFitness = "Equipamiento fitness";
    public const string CuidadoPersonal = "Cuidado personal";
    public const string Otro = "Otro";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Medicamento, InsumoMedico, MaterialHospitalario, ProductoFarmacia,
        AlimentoSaludable, SnackSaludable, Bebida, Suplemento,
        DispositivoSalud, EquipamientoFitness, CuidadoPersonal, Otro
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());
}
