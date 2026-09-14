using System.Net;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.Api.Security;

public sealed class ErpAccessClient(HttpClient http) : IErpAccessClient
{
    private const string Path = "/api/auth/internal/erp-access";

    public async Task<ErpAccessOperation> ChangeAsync(
        Guid operationId,
        Guid userId,
        Guid employeeId,
        string status,
        CancellationToken ct
    )
    {
        using var response = await http.PutAsJsonAsync(
            $"{Path}/{userId}",
            new
            {
                operationId,
                employeeId,
                status,
            },
            ct
        );
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
            throw new BusinessRuleViolationException(
                "No se puede cambiar el acceso: revisa la asignación ERP o espera a que termine el cambio pendiente."
            );
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ErpAccessOperation>(ct)
            ?? throw new HttpRequestException("Auth devolvió una operación vacía.");
    }

    public async Task<IReadOnlyList<ErpAccessOperation>> PendingAsync(
        Guid[]? employeeIds,
        CancellationToken ct
    )
    {
        using var response = employeeIds is null
            ? await http.GetAsync($"{Path}/pending", ct)
            : await http.PostAsJsonAsync($"{Path}/pending", employeeIds, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ErpAccessOperation>>(ct) ?? [];
    }

    public async Task CompleteAsync(Guid operationId, CancellationToken ct)
    {
        using var response = await http.PostAsync($"{Path}/{operationId}/complete", null, ct);
        response.EnsureSuccessStatusCode();
    }
}
