using CoppAddresd.Auth.Controllers;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace CoppAddresd.UnitTests.Auth.Controllers;

/// <summary>
/// Cookie de refresh por aplicación (<c>copp_refresh_token_{erp|app}</c>): ERP y
/// app del paciente comparten host en el navegador, así que cada una escribe y
/// lee la suya. La cookie sin sufijo solo se acepta como respaldo de transición
/// para clientes anteriores (builds móviles ya distribuidos).
/// </summary>
public sealed class AuthControllerRefreshCookieTests
{
    private const string Legacy = "copp_refresh_token";
    private const string ErpCookie = "copp_refresh_token_erp";
    private const string AppCookie = "copp_refresh_token_app";

    private readonly IAuthService _auth = Substitute.For<IAuthService>();
    private readonly IOtpService _otp = Substitute.For<IOtpService>();

    private static TokenResult Tokens(string refresh) =>
        new("access-token", refresh, "Bearer", 900);

    private (AuthController Controller, DefaultHttpContext Http) Create(string? cookieHeader = null)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Development);

        var http = new DefaultHttpContext();
        if (cookieHeader is not null)
        {
            http.Request.Headers.Cookie = cookieHeader;
        }

        var controller = new AuthController(_auth, _otp, env)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
        return (controller, http);
    }

    private static List<string> SetCookies(HttpContext http) =>
        http.Response.Headers.SetCookie.Select(v => v ?? string.Empty).ToList();

    /// <summary>Set-Cookie que fija un valor para <paramref name="name"/>, o null.</summary>
    private static string? Written(HttpContext http, string name) =>
        SetCookies(http).FirstOrDefault(c => c.StartsWith(name + "=", StringComparison.Ordinal)
                                             && !c.StartsWith(name + "=;", StringComparison.Ordinal));

    /// <summary>Set-Cookie que retira <paramref name="name"/> (valor vacío), o null.</summary>
    private static string? Cleared(HttpContext http, string name) =>
        SetCookies(http).FirstOrDefault(c => c.StartsWith(name + "=;", StringComparison.Ordinal));

    // ---------- Nombre de la cookie ----------

    [Theory]
    [InlineData("erp", ErpCookie)]
    [InlineData("app", AppCookie)]
    [InlineData("ERP", ErpCookie)]
    [InlineData("  App ", AppCookie)]
    [InlineData(null, Legacy)]
    [InlineData("", Legacy)]
    [InlineData("   ", Legacy)]
    public void RefreshCookieName_SeleccionaLaCookieDeLaAplicacion(string? application, string expected)
    {
        Assert.Equal(expected, AuthController.RefreshCookieName(application));
    }

    [Theory]
    [InlineData("erp; Path=/")]
    [InlineData("er p")]
    [InlineData("erp\r\nSet-Cookie: x=y")]
    [InlineData("a=b")]
    [InlineData("ñandú")]
    public void RefreshCookieName_CodigoInvalido_NoLlegaAlNombreDeLaCookie(string application)
    {
        Assert.Equal(Legacy, AuthController.RefreshCookieName(application));
    }

    [Fact]
    public void RefreshCookieName_CodigoDemasiadoLargo_UsaElNombreHeredado()
    {
        Assert.Equal(Legacy, AuthController.RefreshCookieName(new string('a', 33)));
        Assert.Equal(
            AuthController.RefreshTokenCookiePrefix + new string('a', 32),
            AuthController.RefreshCookieName(new string('a', 32)));
    }

    // ---------- Login ----------

    [Theory]
    [InlineData("erp", ErpCookie)]
    [InlineData("app", AppCookie)]
    [InlineData("APP", AppCookie)]
    public async Task Login_EscribeLaCookieDeLaAplicacion(string application, string expectedCookie)
    {
        _auth.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(Tokens("refresh-1"));
        var (controller, http) = Create();

        var result = await controller.Login(
            new LoginRequest { Email = "a@b.co", Password = "x", Application = application, RememberMe = true },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        var cookie = Written(http, expectedCookie);
        Assert.NotNull(cookie);
        Assert.StartsWith(expectedCookie + "=refresh-1", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", cookie, StringComparison.OrdinalIgnoreCase);
        // Nunca escribe la cookie heredada cuando la aplicación es conocida.
        Assert.Null(Written(http, Legacy));
    }

    [Fact]
    public async Task Login_ErpYApp_UsanCookiesDistintas()
    {
        _auth.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(Tokens("t-erp"), Tokens("t-app"));
        var (erp, erpHttp) = Create();
        var (app, appHttp) = Create();

        await erp.Login(new LoginRequest { Email = "a@b.co", Password = "x", Application = "erp" }, CancellationToken.None);
        await app.Login(new LoginRequest { DocumentNumber = "1", Password = "x", Application = "app" }, CancellationToken.None);

        Assert.NotNull(Written(erpHttp, ErpCookie));
        Assert.Null(Written(erpHttp, AppCookie));
        Assert.NotNull(Written(appHttp, AppCookie));
        Assert.Null(Written(appHttp, ErpCookie));
    }

    [Fact]
    public async Task Login_ApplicationConCaracteresInvalidos_NoInyectaEnElNombreDeLaCookie()
    {
        _auth.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(Tokens("refresh-1"));
        var (controller, http) = Create();

        await controller.Login(
            new LoginRequest { Email = "a@b.co", Password = "x", Application = "erp; Domain=evil.test" },
            CancellationToken.None);

        var cookies = SetCookies(http);
        Assert.Single(cookies);
        Assert.StartsWith(Legacy + "=refresh-1", cookies[0]);
        Assert.DoesNotContain("evil.test", cookies[0]);
    }

    // ---------- Refresh ----------

    [Fact]
    public async Task Refresh_ConCookiePropia_RotaYEscribeLaCookiePropia()
    {
        _auth.GetRefreshTokenApplicationCodeAsync("tok-app", Arg.Any<CancellationToken>()).Returns("app");
        _auth.RefreshAsync("tok-app", Arg.Any<CancellationToken>()).Returns(Tokens("tok-app-2"));
        var (controller, http) = Create($"{AppCookie}=tok-app");

        var result = await controller.Refresh(new RefreshTokenRequest { Application = "app" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.StartsWith(AppCookie + "=tok-app-2", Written(http, AppCookie));
        Assert.Null(Written(http, Legacy));
        Assert.Null(Cleared(http, Legacy));
    }

    [Fact]
    public async Task Refresh_CookieHeredadaDeOtraAplicacion_SeTrataComoAusenteSinRotarNiLimpiar()
    {
        // Caso del bug: el admin del ERP deja copp_refresh_token y la app del
        // paciente la envia al restaurar sesion.
        _auth.GetRefreshTokenApplicationCodeAsync("tok-erp", Arg.Any<CancellationToken>()).Returns("erp");
        var (controller, http) = Create($"{Legacy}=tok-erp");

        var result = await controller.Refresh(new RefreshTokenRequest { Application = "app" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal("missing", http.Response.Headers["X-Refresh-Status"].ToString());
        await _auth.DidNotReceiveWithAnyArgs().RefreshAsync(default!, default);
        // La cookie heredada es la sesion valida del ERP: no se toca.
        Assert.Empty(SetCookies(http));
    }

    [Fact]
    public async Task Refresh_CookieNuevaDeOtraAplicacionEnElCuerpo_SeRechaza()
    {
        _auth.GetRefreshTokenApplicationCodeAsync("tok-erp", Arg.Any<CancellationToken>()).Returns("erp");
        var (controller, http) = Create();

        var result = await controller.Refresh(
            new RefreshTokenRequest { Application = "app", RefreshToken = "tok-erp" },
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        await _auth.DidNotReceiveWithAnyArgs().RefreshAsync(default!, default);
    }

    [Fact]
    public async Task Refresh_CookieHeredadaDeLaMismaAplicacion_MigraALaCookiePropia()
    {
        _auth.GetRefreshTokenApplicationCodeAsync("tok-legacy", Arg.Any<CancellationToken>()).Returns("erp");
        _auth.RefreshAsync("tok-legacy", Arg.Any<CancellationToken>()).Returns(Tokens("tok-erp-1"));
        var (controller, http) = Create($"{Legacy}=tok-legacy");

        var result = await controller.Refresh(new RefreshTokenRequest { Application = "erp" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.StartsWith(ErpCookie + "=tok-erp-1", Written(http, ErpCookie));
        Assert.NotNull(Cleared(http, Legacy));
    }

    [Fact]
    public async Task Refresh_SinApplication_ClienteAnterior_UsaYEscribeLaCookieHeredada()
    {
        _auth.RefreshAsync("tok-old", Arg.Any<CancellationToken>()).Returns(Tokens("tok-old-2"));
        var (controller, http) = Create($"{Legacy}=tok-old");

        var result = await controller.Refresh(null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.StartsWith(Legacy + "=tok-old-2", Written(http, Legacy));
        await _auth.DidNotReceiveWithAnyArgs().GetRefreshTokenApplicationCodeAsync(default!, default);
    }

    [Fact]
    public async Task Refresh_CookiePropiaTienePrecedenciaSobreLaHeredada()
    {
        _auth.GetRefreshTokenApplicationCodeAsync("tok-erp", Arg.Any<CancellationToken>()).Returns("erp");
        _auth.RefreshAsync("tok-erp", Arg.Any<CancellationToken>()).Returns(Tokens("tok-erp-2"));
        var (controller, http) = Create($"{Legacy}=tok-other; {ErpCookie}=tok-erp");

        var result = await controller.Refresh(new RefreshTokenRequest { Application = "erp" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        await _auth.Received(1).RefreshAsync("tok-erp", Arg.Any<CancellationToken>());
        await _auth.DidNotReceive().RefreshAsync("tok-other", Arg.Any<CancellationToken>());
        // La heredada no era la usada: se deja intacta.
        Assert.Null(Cleared(http, Legacy));
    }

    [Fact]
    public async Task Refresh_TokenInvalido_LimpiaSoloLaCookiePropia()
    {
        _auth.GetRefreshTokenApplicationCodeAsync("tok-bad", Arg.Any<CancellationToken>()).Returns((string?)null);
        _auth.RefreshAsync("tok-bad", Arg.Any<CancellationToken>()).Returns((TokenResult?)null);
        var (controller, http) = Create($"{AppCookie}=tok-bad; {Legacy}=tok-erp-valid");

        var result = await controller.Refresh(new RefreshTokenRequest { Application = "app" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal("invalid", http.Response.Headers["X-Refresh-Status"].ToString());
        Assert.NotNull(Cleared(http, AppCookie));
        // La heredada puede ser la sesion del ERP: no se retira.
        Assert.Null(Cleared(http, Legacy));
    }

    [Fact]
    public async Task Refresh_SinCookie_DevuelveMissing()
    {
        var (controller, http) = Create();

        var result = await controller.Refresh(new RefreshTokenRequest { Application = "app" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal("missing", http.Response.Headers["X-Refresh-Status"].ToString());
        await _auth.DidNotReceiveWithAnyArgs().RefreshAsync(default!, default);
    }

    // ---------- Logout ----------

    [Fact]
    public async Task Logout_ConCookiePropia_RevocaYRetiraSoloLaPropia()
    {
        var userId = Guid.NewGuid();
        _auth.GetRefreshTokenApplicationCodeAsync("tok-app", Arg.Any<CancellationToken>()).Returns("app");
        _auth.GetUserIdByRefreshTokenAsync("tok-app", Arg.Any<CancellationToken>()).Returns(userId);
        var (controller, http) = Create($"{AppCookie}=tok-app; {ErpCookie}=tok-erp");

        var result = await controller.Logout(new RefreshTokenRequest { Application = "app" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        await _auth.Received(1).LogoutAsync(userId, Arg.Any<CancellationToken>());
        Assert.NotNull(Cleared(http, AppCookie));
        Assert.Null(Cleared(http, ErpCookie));
        Assert.Null(Cleared(http, Legacy));
    }

    [Fact]
    public async Task Logout_CookieHeredadaDeOtraAplicacion_NoRevocaNiLaRetira()
    {
        _auth.GetRefreshTokenApplicationCodeAsync("tok-erp", Arg.Any<CancellationToken>()).Returns("erp");
        var (controller, http) = Create($"{Legacy}=tok-erp");

        var result = await controller.Logout(new RefreshTokenRequest { Application = "app" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        await _auth.DidNotReceiveWithAnyArgs().LogoutAsync(default, default);
        Assert.Null(Cleared(http, Legacy));
        Assert.NotNull(Cleared(http, AppCookie));
    }

    [Fact]
    public async Task Logout_CookieHeredadaDeLaMismaAplicacion_RevocaYRetiraAmbas()
    {
        var userId = Guid.NewGuid();
        _auth.GetRefreshTokenApplicationCodeAsync("tok-legacy", Arg.Any<CancellationToken>()).Returns("erp");
        _auth.GetUserIdByRefreshTokenAsync("tok-legacy", Arg.Any<CancellationToken>()).Returns(userId);
        var (controller, http) = Create($"{Legacy}=tok-legacy");

        await controller.Logout(new RefreshTokenRequest { Application = "erp" }, CancellationToken.None);

        await _auth.Received(1).LogoutAsync(userId, Arg.Any<CancellationToken>());
        Assert.NotNull(Cleared(http, ErpCookie));
        Assert.NotNull(Cleared(http, Legacy));
    }

    [Fact]
    public async Task Logout_SinCuerpo_ClienteAnterior_RevocaYRetiraLaCookieHeredada()
    {
        var userId = Guid.NewGuid();
        _auth.GetUserIdByRefreshTokenAsync("tok-old", Arg.Any<CancellationToken>()).Returns(userId);
        var (controller, http) = Create($"{Legacy}=tok-old");

        var result = await controller.Logout(null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        await _auth.Received(1).LogoutAsync(userId, Arg.Any<CancellationToken>());
        Assert.NotNull(Cleared(http, Legacy));
    }

    [Fact]
    public async Task Logout_SinCookie_EsIdempotente()
    {
        var (controller, http) = Create();

        var result = await controller.Logout(new RefreshTokenRequest { Application = "app" }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        await _auth.DidNotReceiveWithAnyArgs().LogoutAsync(default, default);
        Assert.NotNull(Cleared(http, AppCookie));
    }
}
