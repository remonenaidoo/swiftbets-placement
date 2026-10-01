using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Identity.Application.Accounts;
using SwiftBets.Identity.Domain;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]

namespace SwiftBets.Identity.Infrastructure.Tests;

public sealed class RegistrationTests(SqlServerFixture sql)
{
    private static readonly DateOnly Adult = new(1990, 5, 17);

    [Fact]
    public async Task Adult_registers_verifies_the_emailed_link_and_signs_in()
    {
        var db = await IdentityDatabase.CreateAsync(sql);

        (await db.Register.HandleAsync(new RegisterCommand(" New.Punter@Example.com ", "a long passphrase", Adult, "ZA", "ZAR"), CancellationToken.None)).IsSuccess.ShouldBeTrue();
        (await db.Verification.VerifyAsync(db.Messenger.LastToken("verify"))).IsSuccess.ShouldBeTrue();
        var signedIn = await db.Sessions.PasswordAsync("new.punter@example.com", "a long passphrase", CancellationToken.None);

        signedIn.IsSuccess.ShouldBeTrue();
        var user = (await db.Users.FindByLoginAsync("new.punter@example.com", CancellationToken.None))!;
        (user.EmailVerifiedAt is not null, user.Roles.Single(), user.Currency).ShouldBe((true, RoleNames.Customer, "ZAR"));
    }

    [Fact]
    public async Task Seventeen_year_old_is_refused_and_no_account_exists()
    {
        var db = await IdentityDatabase.CreateAsync(sql);

        var result = await db.Register.HandleAsync(new RegisterCommand("young@example.com", "a long passphrase", new DateOnly(2008, 10, 2), "ZA", "ZAR"), CancellationToken.None);

        result.Error!.Code.ShouldBe("underage");
        (await db.Users.FindByLoginAsync("young@example.com", CancellationToken.None)).ShouldBeNull();
        db.Messenger.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Registering_a_taken_email_answers_the_same_and_tells_only_the_owner()
    {
        var db = await IdentityDatabase.CreateAsync(sql);
        await db.Register.HandleAsync(new RegisterCommand("owner@example.com", "a long passphrase", Adult, "ZA", "ZAR"), CancellationToken.None);

        var second = await db.Register.HandleAsync(new RegisterCommand("OWNER@example.com", "another passphrase", Adult, "ZA", "USD"), CancellationToken.None);

        second.IsSuccess.ShouldBeTrue();
        db.Messenger.Sent.Select(s => s.Kind).ShouldBe(["verify", "already-registered"]);
        (await db.Sessions.PasswordAsync("owner@example.com", "a long passphrase", CancellationToken.None)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Concurrent_registrations_of_one_email_create_one_account()
    {
        var db = await IdentityDatabase.CreateAsync(sql);

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            db.Register.HandleAsync(new RegisterCommand("race@example.com", "a long passphrase", Adult, "ZA", "ZAR"), CancellationToken.None)));

        db.Messenger.Sent.Count(s => s.Kind == "verify").ShouldBe(1);
        db.Messenger.Sent.Count(s => s.Kind == "already-registered").ShouldBe(9);
    }

    [Theory]
    [InlineData("not-an-email", "a long passphrase", "ZA", "ZAR", "email_invalid")]
    [InlineData("a@example.com", "short", "ZA", "ZAR", "password_weak")]
    [InlineData("a@example.com", "a long passphrase", "GB", "ZAR", "country_not_supported")]
    [InlineData("a@example.com", "a long passphrase", "ZA", "EUR", "currency_not_supported")]
    public async Task Invalid_registration_is_refused_with_its_reason(string email, string password, string country, string currency, string code)
    {
        var db = await IdentityDatabase.CreateAsync(sql);

        (await db.Register.HandleAsync(new RegisterCommand(email, password, Adult, country, currency), CancellationToken.None)).Error!.Code.ShouldBe(code);
    }

    [Fact]
    public async Task Verification_link_works_once_and_not_after_it_expires()
    {
        var db = await IdentityDatabase.CreateAsync(sql);
        await db.Register.HandleAsync(new RegisterCommand("once@example.com", "a long passphrase", Adult, "ZA", "ZAR"), CancellationToken.None);
        var token = db.Messenger.LastToken("verify");

        (await db.Verification.VerifyAsync(token)).IsSuccess.ShouldBeTrue();
        (await db.Verification.VerifyAsync(token)).Error!.Code.ShouldBe("token_invalid");

        await db.Verification.ResendAsync("once@example.com", CancellationToken.None);
        db.Messenger.Sent.Count(s => s.Kind == "verify").ShouldBe(1);

        await db.Register.HandleAsync(new RegisterCommand("late@example.com", "a long passphrase", Adult, "ZA", "ZAR"), CancellationToken.None);
        db.Time.Advance(TimeSpan.FromHours(25));
        (await db.Verification.VerifyAsync(db.Messenger.LastToken("verify"))).Error!.Code.ShouldBe("token_invalid");
    }
}
