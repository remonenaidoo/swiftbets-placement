using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SwiftBets.BuildingBlocks.Testing;

namespace SwiftBets.Identity.Api.Tests;

public sealed class IdentityApiTests(SqlServerFixture sql)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Customer_registers_confirms_the_emailed_link_signs_in_and_reads_their_profile()
    {
        await using var host = await IdentityHost.StartAsync(sql);
        var client = host.CreateClient();

        using var registered = await client.PostAsJsonAsync("/auth/register", new { email = "fan@example.com", password = "a long passphrase", dateOfBirth = "1995-04-12", country = "ZA", currency = "ZAR" }, Cancel);
        registered.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        using var verified = await client.PostAsJsonAsync("/auth/verify-email", new { token = host.Emails.LastToken("verify") }, Cancel);
        verified.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var token = await SignInAsync(client, "fan@example.com", "a long passphrase");
        using var me = await GetAsync(client, "/profile", token);

        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = await me.Content.ReadFromJsonAsync<JsonElement>(Cancel);
        (profile.GetProperty("email").GetString(), profile.GetProperty("emailVerified").GetBoolean()).ShouldBe(("fan@example.com", true));
        profile.TryGetProperty("passwordHash", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Underage_registration_is_refused_with_its_reason()
    {
        await using var host = await IdentityHost.StartAsync(sql);

        using var response = await host.CreateClient().PostAsJsonAsync("/auth/register", new { email = "kid@example.com", password = "a long passphrase", dateOfBirth = DateTime.UtcNow.AddYears(-17).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), country = "ZA", currency = "ZAR" }, Cancel);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("code").GetString().ShouldBe("underage");
    }

    [Fact]
    public async Task Operator_suspends_a_customer_who_then_cannot_sign_in_and_customers_cannot_reach_staff_routes()
    {
        await using var host = await IdentityHost.StartAsync(sql);
        var client = host.CreateClient();
        await client.PostAsJsonAsync("/auth/register", new { email = "risky@example.com", password = "a long passphrase", dateOfBirth = "1980-01-01", country = "ZA", currency = "USD" }, Cancel);
        var customerToken = await SignInAsync(client, "risky@example.com", "a long passphrase");
        var customerId = (await (await GetAsync(client, "/profile", customerToken)).Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("userId").GetString();

        using var forbidden = await GetAsync(client, $"/admin/users/{customerId}", customerToken);
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var operatorToken = await SignInAsync(client, "operator1", IdentityHost.DemoPassword);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/admin/users/{customerId}/status") { Content = JsonContent.Create(new { status = "suspended", reason = "chargeback under review" }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        using var suspended = await client.SendAsync(request, Cancel);
        suspended.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var refused = await client.PostAsJsonAsync("/auth/token", new { grantType = "password", username = "risky@example.com", password = "a long passphrase" }, Cancel);
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await refused.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("code").GetString().ShouldBe("account_suspended");
    }

    [Fact]
    public async Task Password_reset_and_resend_requests_always_answer_accepted()
    {
        await using var host = await IdentityHost.StartAsync(sql);
        var client = host.CreateClient();

        using var reset = await client.PostAsJsonAsync("/auth/password-reset", new { email = "nobody@example.com" }, Cancel);
        using var resend = await client.PostAsJsonAsync("/auth/verify-email/resend", new { email = "nobody@example.com" }, Cancel);

        (reset.StatusCode, resend.StatusCode).ShouldBe((HttpStatusCode.Accepted, HttpStatusCode.Accepted));
        host.Emails.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Published_signing_key_matches_the_key_tokens_are_signed_with()
    {
        await using var host = await IdentityHost.StartAsync(sql);
        var client = host.CreateClient();
        var token = new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(await SignInAsync(client, "punter1", IdentityHost.DemoPassword));

        var jwks = await client.GetFromJsonAsync<JsonElement>("/.well-known/jwks.json", Cancel);

        jwks.GetProperty("keys")[0].GetProperty("kid").GetString().ShouldBe(token.Kid);
        token.Issuer.ShouldBe("http://identity.test");
        token.Claims.Where(c => c.Type == "role").Select(c => c.Value).ShouldBe(["Customer", "Punter"], ignoreOrder: true);
    }

    [Fact]
    public async Task Liveness_and_metrics_are_served()
    {
        await using var host = await IdentityHost.StartAsync(sql);
        var client = host.CreateClient();

        (await client.GetAsync("/health/live", Cancel)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetStringAsync("/metrics", Cancel)).ShouldContain("process_cpu_seconds_total");
    }

    private static async Task<string> SignInAsync(HttpClient client, string login, string password)
    {
        using var response = await client.PostAsJsonAsync("/auth/token", new { grantType = "password", username = login, password }, Cancel);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("accessToken").GetString()!;
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request, Cancel);
    }
}
