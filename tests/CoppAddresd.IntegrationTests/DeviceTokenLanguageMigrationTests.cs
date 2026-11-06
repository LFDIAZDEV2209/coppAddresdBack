using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CoppAddresd.IntegrationTests;

/// <summary>Creates/drops only a uniquely named ephemeral database, never migrates the configured database.</summary>
public sealed class DeviceTokenLanguageMigrationTests : IAsyncLifetime
{
    private readonly string server = Environment.GetEnvironmentVariable("COP_TEST_DB_CONNECTION") ?? "";
    private string database = "";
    private string connection = "";
    private const string Migration = "20261106142633_AddDeviceTokenLanguage";
    private AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection, pg => pg.EnableRetryOnFailure()).Options);

    public async Task InitializeAsync()
    {
        if (server.Length == 0) return;
        database = $"copp_push_i18n_test_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(server) { Database = "postgres" }.ConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\" TEMPLATE template0", admin)) await command.ExecuteNonQueryAsync();
        connection = new NpgsqlConnectionStringBuilder(server) { Database = database }.ConnectionString;
        await using var db = Context();
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA auth; CREATE TABLE auth.\"Users\" (\"Id\" uuid PRIMARY KEY, \"SecurityStamp\" text NULL);");
    }

    public async Task DisposeAsync()
    {
        if (database.Length == 0) return;
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(server) { Database = "postgres" }.ConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    [NutritionPostgresFact]
    public async Task UpDownUp_defaults_legacy_spanish_preserves_tokens_and_enforces_supported_languages()
    {
        await using var db = Context();
        var previous = db.Database.GetMigrations().TakeWhile(name => name != Migration).Last();
        await db.GetService<IMigrator>().MigrateAsync(previous);
        var user = Guid.NewGuid(); var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.\"Users\" (\"Id\") VALUES ({user})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO app.device_tokens (id,user_id,token,platform) VALUES ({id},{user},'synthetic-token','ios')");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO app.device_tokens (id,user_id,token,platform) SELECT gen_random_uuid(),{user},'legacy-' || g,'android' FROM generate_series(1,2000) g");
        await db.GetService<IMigrator>().MigrateAsync(Migration);
        var repo = new DeviceTokenRepository(db);
        var legacy = (await repo.GetByUserAndTokenAsync(user, "synthetic-token"))!;
        Assert.Equal("es", legacy.Language); Assert.Equal(id, legacy.Id); Assert.Equal("ios", legacy.Platform);
        Assert.Equal(2000, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM app.device_tokens WHERE token LIKE 'legacy-%' AND language='es'").SingleAsync());
        await repo.RegisterForUserAsync(user, "synthetic-token", "ios", "en");
        await repo.RegisterForUserAsync(user, "synthetic-token", "android", null);
        db.ChangeTracker.Clear();
        var english = (await repo.GetByUserAndTokenAsync(user, "synthetic-token"))!;
        Assert.Equal("en", english.Language); Assert.Equal(id, english.Id); Assert.Equal("android", english.Platform);
        var invalid = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE app.device_tokens SET language='fr' WHERE token='synthetic-token'"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);
        await db.GetService<IMigrator>().MigrateAsync(previous);
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM information_schema.columns WHERE table_schema='app' AND table_name='device_tokens' AND column_name='language'").SingleAsync());
        Assert.Equal(2001, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM app.device_tokens").SingleAsync());
        await db.GetService<IMigrator>().MigrateAsync(Migration);
        db.ChangeTracker.Clear();
        Assert.Equal("es", (await repo.GetByUserAndTokenAsync(user, "synthetic-token"))!.Language);
    }

    [NutritionPostgresFact]
    public async Task Account_switch_prunes_old_links_and_concurrent_registration_has_one_final_owner()
    {
        await using var db = Context();
        await db.GetService<IMigrator>().MigrateAsync(Migration);
        var oldUser = Guid.NewGuid(); var newUser = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.\"Users\" (\"Id\") VALUES ({oldUser}),({newUser})");
        var repo = new DeviceTokenRepository(db);
        await repo.RegisterForUserAsync(oldUser, "shared-synthetic-token", "ios", "en");
        await repo.RegisterForUserAsync(oldUser, "other-device", "android", "en");
        await repo.RegisterForUserAsync(newUser, "shared-synthetic-token", "ios", null);
        Assert.Null(await repo.GetByUserAndTokenAsync(oldUser, "shared-synthetic-token"));
        Assert.Equal("es", (await repo.GetByUserAndTokenAsync(newUser, "shared-synthetic-token"))!.Language);
        Assert.False(await repo.UnregisterForUserAsync(oldUser, "shared-synthetic-token"));
        Assert.NotNull(await repo.GetByUserAndTokenAsync(newUser, "shared-synthetic-token"));
        await using var first = Context(); await using var second = Context();
        await Task.WhenAll(new DeviceTokenRepository(first).RegisterForUserAsync(oldUser, "shared-synthetic-token", "ios", "en"),
            new DeviceTokenRepository(second).RegisterForUserAsync(newUser, "shared-synthetic-token", "ios", "es"));
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM app.device_tokens WHERE token='shared-synthetic-token'").SingleAsync());
        Assert.NotNull(await repo.GetByUserAndTokenAsync(oldUser, "other-device"));
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO app.device_tokens (id,user_id,token,platform,language) VALUES (gen_random_uuid(),{oldUser},'legacy-shared','ios','en'),(gen_random_uuid(),{newUser},'legacy-shared','ios','es')");
        await repo.RegisterForUserAsync(newUser, "legacy-shared", "ios", null);
        Assert.Null(await repo.GetByUserAndTokenAsync(oldUser, "legacy-shared"));
        Assert.Equal("es", (await repo.GetByUserAndTokenAsync(newUser, "legacy-shared"))!.Language);
    }
}
