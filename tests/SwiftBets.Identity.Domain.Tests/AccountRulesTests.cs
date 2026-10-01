namespace SwiftBets.Identity.Domain.Tests;

public sealed class AccountRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 1);

    [Theory]
    [InlineData("2008-10-01", true)]
    [InlineData("2008-09-30", true)]
    [InlineData("2008-10-02", false)]
    [InlineData("2026-10-02", false)]
    public void Age_gate_admits_from_the_eighteenth_birthday(string dateOfBirth, bool admitted) =>
        AgePolicy.IsOldEnough(DateOnly.Parse(dateOfBirth, System.Globalization.CultureInfo.InvariantCulture), Today).ShouldBe(admitted);

    [Fact]
    public void Leap_day_birthday_turns_eighteen_on_the_first_of_march_in_a_common_year() =>
        (AgePolicy.IsOldEnough(new DateOnly(2008, 2, 29), new DateOnly(2026, 2, 28)), AgePolicy.IsOldEnough(new DateOnly(2008, 2, 29), new DateOnly(2026, 3, 1)))
            .ShouldBe((false, true));

    [Theory]
    [InlineData("punter@example.com", true)]
    [InlineData(" Punter@Example.COM ", true)]
    [InlineData("no-at-sign.example.com", false)]
    [InlineData("two@@example.com", false)]
    [InlineData("nodot@example", false)]
    [InlineData("spa ce@example.com", false)]
    public void Email_plausibility(string email, bool plausible) => EmailAddress.IsPlausible(email).ShouldBe(plausible);

    [Fact]
    public void Emails_compare_case_insensitively_after_trimming() =>
        EmailAddress.Normalize(" Punter@Example.COM ").ShouldBe("punter@example.com");

    [Theory]
    [InlineData("short", "a@b.co", false)]
    [InlineData("long enough passphrase", "a@b.co", true)]
    [InlineData("Punter@Example.com", "punter@example.com", false)]
    public void Password_policy(string password, string email, bool accepted) => (PasswordPolicy.Problem(password, email) is null).ShouldBe(accepted);

    [Fact]
    public void Customer_and_staff_tokens_also_carry_the_roles_services_authorise_on_today()
    {
        RoleNames.Claims([RoleNames.Customer]).ShouldBe(["Customer", "Punter"]);
        RoleNames.Claims([RoleNames.Trader]).ShouldBe(["Operator", "Trader"]);
        RoleNames.Claims([RoleNames.Agent]).ShouldBe(["Agent"]);
        RoleNames.Canonical("Punter").ShouldBe(RoleNames.Customer);
    }

    [Theory]
    [InlineData(AccountStatus.Active, null)]
    [InlineData(AccountStatus.Suspended, "account_suspended")]
    [InlineData(AccountStatus.Closed, "account_closed")]
    [InlineData(AccountStatus.SelfExcluded, "account_self_excluded")]
    public void Only_an_active_account_may_sign_in(AccountStatus status, string? refusal) =>
        User(status, lockedUntil: null).SignInRefusal(DateTimeOffset.UnixEpoch).ShouldBe(refusal);

    [Fact]
    public void Lock_refuses_until_it_expires()
    {
        var user = User(AccountStatus.Active, lockedUntil: DateTimeOffset.UnixEpoch.AddMinutes(15));

        (user.SignInRefusal(DateTimeOffset.UnixEpoch), user.SignInRefusal(DateTimeOffset.UnixEpoch.AddMinutes(16))).ShouldBe(("account_locked", (string?)null));
    }

    private static User User(AccountStatus status, DateTimeOffset? lockedUntil) =>
        new(Guid.NewGuid(), "a@b.co", null, "hash", new DateOnly(1990, 1, 1), "swiftbets", "ZA", "ZAR", status, null, 0, lockedUntil, null, [RoleNames.Customer]);
}
