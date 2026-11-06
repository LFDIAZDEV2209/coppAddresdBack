using System.Security.Cryptography;
using System.Text;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Features.Nutrition;

/// <summary>Vincula la traducción revisada con su fuente exacta, sin normalizar el texto clínico.</summary>
public static class NutritionInstructionTranslation
{
    public static string? SourceHash(string? source) => string.IsNullOrWhiteSpace(source)
        ? null : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));

    public static string? PairedSourceHash(string? source, string? english) => string.IsNullOrWhiteSpace(english)
        ? null : SourceHash(source);

    public static string? VerifiedEnglish(NutritionPrescription plan) =>
        !string.IsNullOrWhiteSpace(plan.InstructionsEnSourceHash)
        && string.Equals(plan.InstructionsEnSourceHash, PairedSourceHash(plan.Instructions, plan.InstructionsEn), StringComparison.Ordinal)
            ? plan.InstructionsEn : null;
}
