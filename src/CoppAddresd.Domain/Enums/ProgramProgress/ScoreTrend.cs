namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Tendencia de un puntaje respecto al valor anterior (SPEC §13.5): <c>up</c>,
/// <c>down</c> o <c>stable</c>. Los nombres del enum son los valores
/// almacenados (<c>varchar(10)</c> con CHECK <c>IN ('up','down','stable')</c>)
/// y los del contrato JSON de <c>GET /program/scores</c> (SPEC §13.7.1).
/// </summary>
public enum ScoreTrend
{
    up = 1,
    down = 2,
    stable = 3,
}