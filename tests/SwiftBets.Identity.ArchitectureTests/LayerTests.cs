using System.Reflection;
using NetArchTest.Rules;

namespace SwiftBets.Identity.ArchitectureTests;

public sealed class LayerTests
{
    private static readonly Assembly Domain = typeof(SwiftBets.Identity.Domain.User).Assembly;
    private static readonly Assembly Application = typeof(SwiftBets.Identity.Application.ApplicationRegistration).Assembly;

    [Fact]
    public void Domain_depends_on_nothing_else_in_the_solution() =>
        Types.InAssembly(Domain).ShouldNot().HaveDependencyOnAny("SwiftBets.Identity.Application", "SwiftBets.Identity.Infrastructure", "SwiftBets.BuildingBlocks", "Microsoft.AspNetCore", "Dapper")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void Application_does_not_depend_on_infrastructure() =>
        Types.InAssembly(Application).ShouldNot().HaveDependencyOnAny("SwiftBets.Identity.Infrastructure", "Dapper", "Microsoft.Data.SqlClient", "Confluent.Kafka", "StackExchange.Redis", "Npgsql")
            .GetResult().IsSuccessful.ShouldBeTrue();

    [Fact]
    public void Domain_assembly_references_no_other_project() =>
        Domain.GetReferencedAssemblies().Select(a => a.Name).ShouldNotContain(n => n!.StartsWith("SwiftBets.", StringComparison.Ordinal));
}
