using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Specialized;
using System.Globalization;
using Twilio;
using Twilio.Exceptions;
using Twilio.Http;
using Twilio.Jwt.AccessToken;
using Twilio.Rest.Video.V1;
using Twilio.Rest.Video.V1.Room;
using Twilio.Security;

namespace CoppAddresd.Telemedicine.Infrastructure.VideoProvider;

/// <summary>
/// Implementación de <see cref="IVideoProvider"/> con Twilio Programmable Video
/// (SDK oficial). Responsabilidades: crear/consultar/completar salas, generar
/// tokens de acceso (JWT con VideoGrant) y validar la firma de webhooks. El
/// dominio de telemedicina no conoce Twilio: solo pasa por esta clase.
/// </summary>
public sealed class TwilioVideoProvider(
    IOptions<TwilioOptions> options,
    ILogger<TwilioVideoProvider> logger) : IVideoProvider
{
    public async Task<RoomInfo> CreateRoomAsync(RoomRequest request, CancellationToken ct)
    {
        EnsureConfigured();
        InitClient();

        // Idempotencia por UniqueName: si la sala in-progress ya existe con ese
        // nombre, Twilio la devuelve en lugar de crear una duplicada. Sin embargo,
        // el SDK lanza ApiException 409/20429 "Room exists" cuando el nombre ya
        // está tomado (p. ej. un intento previo del mismo join-token creó la sala
        // y falló antes de persistir en BD): en ese caso se recupera la existente.
        try
        {
            var room = await RoomResource.CreateAsync(new CreateRoomOptions
            {
                UniqueName = request.RoomName,
                Type = MapType(request.Type),
                MaxParticipants = request.MaxParticipants,
                StatusCallback = request.StatusCallbackUrl is null
                    ? null
                    : new Uri(request.StatusCallbackUrl)
            });

            logger.LogInformation("Room Twilio creada: {RoomSid} ({RoomName})", room.Sid, room.UniqueName);

            return MapRoom(room);
        }
        catch (ApiException ex) when (IsRoomExistsError(ex))
        {
            // Reintento del mismo join-token / sala huérfana en Twilio: devolver
            // la existente (idempotencia real del proveedor). Si no se recupera,
            // se propaga el error original.
            logger.LogInformation(
                "Room Twilio ya existía ({RoomName}); se devuelve la existente. " +
                "Detalle: status={Status} code={Code}",
                request.RoomName, ex.Status, ex.Code);

            var existing = await GetRoomAsync(request.RoomName, ct);
            if (existing is null)
            {
                // La sala no se pudo recuperar: propaga el error original
                // preservando el stack trace (CA2200).
                throw;
            }

            return existing;
        }
    }

    /// <summary>
    /// El SDK lanza "Room exists" con HTTP 409 / código Twilio 20429 (o el
    /// mensaje directo): la sala con ese UniqueName ya existe en el proveedor.
    /// </summary>
    private static bool IsRoomExistsError(ApiException ex)
        => ex.Status == 409
           || ex.Code == 20429
           || ex.Message.Contains("Room exists", StringComparison.OrdinalIgnoreCase);

    public async Task<RoomInfo?> GetRoomAsync(string providerRoomSidOrName, CancellationToken ct)
    {
        EnsureConfigured();
        InitClient();

        try
        {
            var room = await RoomResource.FetchAsync(pathSid: providerRoomSidOrName);
            return MapRoom(room);
        }
        catch (ApiException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task CompleteRoomAsync(string providerRoomSid, CancellationToken ct)
    {
        EnsureConfigured();
        InitClient();

        await RoomResource.UpdateAsync(
            status: RoomResource.RoomStatusEnum.Completed,
            pathSid: providerRoomSid);

        logger.LogInformation("Room Twilio completada: {RoomSid}", providerRoomSid);
    }

    /// <summary>Código Twilio de «Room contains too many Participants» (sala llena).</summary>
    private const int RoomFullErrorCode = 53105;

    public async Task UpdateRoomMaxParticipantsAsync(
        string providerRoomSid,
        int maxParticipants,
        CancellationToken ct)
    {
        EnsureConfigured();
        InitClient();

        // El SDK fijado (Twilio 7.14.9) no expone MaxParticipants en
        // UpdateRoomOptions: se usa el cliente REST del propio SDK (mismas
        // credenciales) contra POST /v1/Rooms/{Sid}. Twilio solo permite el
        // cambio en salas in-progress; una sala completada responde error y el
        // llamador decide (best-effort).
        var request = new Request(
            Twilio.Http.HttpMethod.Post,
            $"https://video.twilio.com/v1/Rooms/{providerRoomSid}");
        request.AddPostParam(
            "MaxParticipants", maxParticipants.ToString(CultureInfo.InvariantCulture));

        try
        {
            await TwilioClient.GetRestClient().RequestAsync(request);

            logger.LogInformation(
                "Capacidad de la sala Twilio {RoomSid} actualizada a {MaxParticipants} participantes.",
                providerRoomSid, maxParticipants);
        }
        catch (ApiException ex) when (ex.Code == RoomFullErrorCode)
        {
            // 53105 se traduce a una regla de negocio clara (409 «sala completa»)
            // en lugar de un error crudo del proveedor.
            throw new BusinessRuleViolationException(
                "La sala alcanzó el máximo de participantes.");
        }
    }

    public Task<string> GenerateAccessTokenAsync(AccessTokenRequest request, CancellationToken ct)
    {
        EnsureConfigured();

        var grant = new VideoGrant { Room = request.RoomName };

        var token = new Token(
            _options.AccountSid,
            _options.ApiKeySid,
            _options.ApiKeySecret,
            identity: request.Identity,
            expiration: DateTime.UtcNow.AddSeconds(request.TtlSeconds),
            grants: [grant]);

        return Task.FromResult(token.ToJwt());
    }

    public async Task<IReadOnlyList<ParticipantInfo>> GetParticipantsAsync(
        string providerRoomSid,
        CancellationToken ct)
    {
        EnsureConfigured();
        InitClient();

        var participants = await ParticipantResource.ReadAsync(pathRoomSid: providerRoomSid);

        return participants
            .Select(p => new ParticipantInfo(
                p.Sid,
                p.Identity,
                IsConnected: p.Status == ParticipantResource.StatusEnum.Connected,
                ConnectedAt: p.StartTime,
                DisconnectedAt: p.EndTime))
            .ToList();
    }

    public Task<bool> ValidateWebhookSignatureAsync(
        WebhookValidationRequest request,
        CancellationToken ct)
    {
        if (!_options.ValidateWebhookSignature)
        {
            logger.LogWarning(
                "Validación de firma de webhook deshabilitada (solo desarrollo). " +
                "Habilitar Twilio:ValidateWebhookSignature en producción.");
            return Task.FromResult(true);
        }

        if (string.IsNullOrWhiteSpace(_options.AuthToken))
        {
            throw new BusinessRuleViolationException(
                "Twilio:AuthToken no configurado; no se puede validar la firma del webhook.");
        }

        var form = new NameValueCollection();
        foreach (var (key, value) in request.FormParams)
        {
            form[key] = value;
        }

        var validator = new RequestValidator(_options.AuthToken);
        var valid = validator.Validate(request.Url, form, request.Signature);

        return Task.FromResult(valid);
    }

    private TwilioOptions _options => options.Value;

    private void InitClient()
    {
        // Con API Keys (Video requiere key región US1), el par username/password
        // es (ApiKeySid, ApiKeySecret) y el accountSid va como tercer argumento.
        // NO usar SetRegion: el host de Video es video.twilio.com (sin sufijo);
        // SetRegion("us1") genera video.us1.twilio.com que no resuelve.
        TwilioClient.Init(_options.ApiKeySid, _options.ApiKeySecret, _options.AccountSid);
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new BusinessRuleViolationException(
                "Twilio no está configurado (AccountSid/ApiKeySid/ApiKeySecret). " +
                "Revisa la sección Twilio de la configuración.");
        }
    }

    private static RoomResource.RoomTypeEnum MapType(VideoRoomType type) => type switch
    {
        VideoRoomType.Group => RoomResource.RoomTypeEnum.Group,
        VideoRoomType.GroupSmall => RoomResource.RoomTypeEnum.GroupSmall,
        VideoRoomType.PeerToPeer => RoomResource.RoomTypeEnum.PeerToPeer,
        _ => RoomResource.RoomTypeEnum.Group
    };

    private static RoomInfo MapRoom(RoomResource room) => new(
        room.Sid,
        room.UniqueName,
        room.Status.ToString(),
        room.DateCreated,
        room.EndTime,
        room.Duration);
}
