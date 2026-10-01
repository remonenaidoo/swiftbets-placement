using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Identity.Application.Accounts;
using SwiftBets.Identity.Domain;

namespace SwiftBets.Identity.Infrastructure.Tests;

public sealed class SignInTests(SqlServerFixture sql)
{
    private const string Password = "a long passphrase";

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_against_the_right_one_until_it_expires()
    {
        var (db, _) = await RegisteredAsync();

        for (var i = 0; i < User.MaxFailedSignIns; i++)
        {
            (await db.Sessions.PasswordAsync("p@example.com", "wrong password!", CancellationToken.None)).Error!.Code.ShouldBe("invalid_credentials");
        }

        (await db.Sessions.PasswordAsync("p@example.com", Password, CancellationToken.None)).Error!.Code.ShouldBe("account_locked");
        db.Time.Advance(User.LockoutDuration + TimeSpan.FromSeconds(1));
        (await db.Sessions.PasswordAsync("p@example.com", Password, CancellationToken.None)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Unknown_email_gets_the_same_answer_as_a_wrong_password() =>
        (await (await IdentityDatabase.CreateAsync(sql)).Sessions.PasswordAsync("nobody@example.com", Password, CancellationToken.None)).Error!.Code.ShouldBe("invalid_credentials");

    [Fact]
    public async Task Self_excluded_account_is_told_why_only_with_the_right_password_and_its_devices_cannot_renew()
    {
        var (db, userId) = await RegisteredAsync();
        var session = (await db.Sessions.PasswordAsync("p@example.com", Password, CancellationToken.None)).Value;

        (await db.Status.HandleAsync(userId, AccountStatus.SelfExcluded, "customer asked for six months", "ops-1", CancellationToken.None)).IsSuccess.ShouldBeTrue();

        (await db.Sessions.PasswordAsync("p@example.com", "wrong password!", CancellationToken.None)).Error!.Code.ShouldBe("invalid_credentials");
        (await db.Sessions.PasswordAsync("p@example.com", Password, CancellationToken.None)).Error!.Code.ShouldBe("account_self_excluded");
        (await db.Sessions.RefreshAsync(session.RefreshToken!, CancellationToken.None)).Error!.Code.ShouldBe("invalid_refresh_token");
        (await db.Status.HandleAsync(userId, AccountStatus.Active, "changed mind", "ops-1", CancellationToken.None)).Error!.Code.ShouldBe("self_exclusion_locked");
    }

    [Fact]
    public async Task Refresh_rotates_and_reusing_a_spent_token_revokes_the_whole_device()
    {
        var (db, _) = await RegisteredAsync();
        var first = (await db.Sessions.PasswordAsync("p@example.com", Password, CancellationToken.None)).Value;
        var second = (await db.Sessions.RefreshAsync(first.RefreshToken!, CancellationToken.None)).Value;

        (await db.Sessions.RefreshAsync(first.RefreshToken!, CancellationToken.None)).Error!.Code.ShouldBe("refresh_token_reused");
        (await db.Sessions.RefreshAsync(second.RefreshToken!, CancellationToken.None)).Error!.Code.ShouldBe("invalid_refresh_token");
    }

    [Fact]
    public async Task Signing_a_device_out_leaves_the_other_devices_signed_in()
    {
        var (db, _) = await RegisteredAsync();
        var phone = (await db.Sessions.PasswordAsync("p@example.com", Password, CancellationToken.None)).Value;
        var laptop = (await db.Sessions.PasswordAsync("p@example.com", Password, CancellationToken.None)).Value;

        await db.Sessions.RevokeAsync(phone.RefreshToken!);

        (await db.Sessions.RefreshAsync(phone.RefreshToken!, CancellationToken.None)).IsFailure.ShouldBeTrue();
        (await db.Sessions.RefreshAsync(laptop.RefreshToken!, CancellationToken.None)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Password_reset_link_sets_a_new_password_unlocks_and_signs_every_device_out()
    {
        var (db, _) = await RegisteredAsync();
        var device = (await db.Sessions.PasswordAsync("p@example.com", Password, CancellationToken.None)).Value;
        for (var i = 0; i < User.MaxFailedSignIns; i++)
        {
            await db.Sessions.PasswordAsync("p@example.com", "wrong password!", CancellationToken.None);
        }

        await db.Reset.RequestAsync("P@example.com", CancellationToken.None);
        var reset = await db.Reset.ResetAsync(db.Messenger.LastToken("reset"), "a brand new passphrase", CancellationToken.None);

        reset.IsSuccess.ShouldBeTrue();
        (await db.Sessions.RefreshAsync(device.RefreshToken!, CancellationToken.None)).IsFailure.ShouldBeTrue();
        (await db.Sessions.PasswordAsync("p@example.com", Password, CancellationToken.None)).IsFailure.ShouldBeTrue();
        (await db.Sessions.PasswordAsync("p@example.com", "a brand new passphrase", CancellationToken.None)).IsSuccess.ShouldBeTrue();
        (await db.Reset.ResetAsync(db.Messenger.LastToken("reset"), "yet another passphrase", CancellationToken.None)).Error!.Code.ShouldBe("token_invalid");
    }

    [Fact]
    public async Task Reset_request_for_an_unknown_email_succeeds_and_sends_nothing()
    {
        var db = await IdentityDatabase.CreateAsync(sql);

        (await db.Reset.RequestAsync("nobody@example.com", CancellationToken.None)).IsSuccess.ShouldBeTrue();

        db.Messenger.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Staff_tokens_carry_permissions_and_customer_tokens_do_not()
    {
        var db = await IdentityDatabase.CreateAsync(sql);
        var ops = new User(Guid.NewGuid(), "ops@example.com", "ops1", db.Hasher.Hash(Password), null, "swiftbets", "ZA", "ZAR", AccountStatus.Active, null, 0, null, null, [RoleNames.Ops]);
        await db.Users.UpsertSeedUserAsync(ops, db.Time.GetUtcNow());
        var (_, customerId) = await RegisterAsync(db);

        var staff = Claims((await db.Sessions.PasswordAsync("ops1", Password, CancellationToken.None)).Value.AccessToken);
        var customer = Claims((await db.Sessions.PasswordAsync("p@example.com", Password, CancellationToken.None)).Value.AccessToken);

        staff.Where(c => c.Type == "perm").Select(c => c.Value).ShouldBe([Permissions.UsersRead, Permissions.UsersStatusWrite], ignoreOrder: true);
        staff.Where(c => c.Type == "role").Select(c => c.Value).ShouldBe(["Operator", "Ops"], ignoreOrder: true);
        customer.ShouldNotContain(c => c.Type == "perm");
        customer.Single(c => c.Type == "sub").Value.ShouldBe(customerId.ToString());
    }

    [Fact]
    public async Task Service_clients_get_service_tokens_and_wrong_secrets_get_nothing()
    {
        var sessions = (await IdentityDatabase.CreateAsync(sql)).Sessions;

        sessions.ClientCredentials("payout", "payout-secret").IsSuccess.ShouldBeTrue();
        sessions.ClientCredentials("payout", "guess").Error!.Code.ShouldBe("invalid_client");
    }

    private async Task<(IdentityDatabase Db, Guid UserId)> RegisteredAsync()
    {
        var db = await IdentityDatabase.CreateAsync(sql);
        return await RegisterAsync(db);
    }

    private static async Task<(IdentityDatabase Db, Guid UserId)> RegisterAsync(IdentityDatabase db)
    {
        await db.Register.HandleAsync(new RegisterCommand("p@example.com", Password, new DateOnly(1990, 1, 1), "ZA", "ZAR"), CancellationToken.None);
        return (db, (await db.Users.FindByLoginAsync("p@example.com", CancellationToken.None))!.UserId);
    }

    private static IReadOnlyList<System.Security.Claims.Claim> Claims(string token) =>
        [.. new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token).Claims];
}
