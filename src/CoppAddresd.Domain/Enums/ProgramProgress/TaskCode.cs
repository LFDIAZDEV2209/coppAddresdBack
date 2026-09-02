namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Códigos de tarea que usa la app móvil. Los nombres del enum son
/// exactamente los valores almacenados (varchar) y los que el cliente envía
/// en el contrato de la API: <c>podcast</c>, <c>vitals</c>, <c>nut</c>,
/// <c>ejercicio</c>, <c>nutraceutico</c>, <c>emocional</c>.
/// </summary>
public enum TaskCode
{
    podcast = 1,
    vitals = 2,
    nut = 3,
    ejercicio = 4,
    nutraceutico = 5,
    emocional = 6,
}