using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Wallet.Application.Ports;
using SwiftBets.Wallet.Application.Reconciliation;
using SwiftBets.Wallet.Domain;

namespace SwiftBets.Wallet.Api.Endpoints;

public static class ReconciliationEndpoints
{
    public static IEndpointRouteBuilder MapReconciliation(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/reconciliation").RequireAuthorization(Roles.Operator);

        group.MapGet("/latest", async (IReconciliationStore store, HttpContext context) =>
            await store.GetLatestAsync(context.RequestAborted) is { } report
                ? Results.Ok(ReportResponse.From(report))
                : Error.NotFound("reconciliation_not_run", "No reconciliation has run yet.").ToHttpResult(context));

        group.MapPost("/runs", async (ReconcileLedgerHandler handler, HttpContext context) =>
            Results.Ok(ReportResponse.From(await handler.HandleAsync(context.RequestAborted))));

        return endpoints;
    }

    public sealed record DriftResponse(string Kind, Guid? AccountId, Guid? PostingId, long Expected, long Actual);

    public sealed record ReportResponse(Guid RunId, DateTimeOffset StartedAt, DateTimeOffset CompletedAt, int AccountsChecked, bool IsClean, IReadOnlyList<DriftResponse> Drifts)
    {
        public static ReportResponse From(ReconciliationReport report) => new(
            report.RunId, report.StartedAt, report.CompletedAt, report.AccountsChecked, report.IsClean,
            [.. report.Drifts.Select(d => new DriftResponse(d.Kind.ToString(), d.AccountId, d.PostingId, d.Expected, d.Actual))]);
    }
}
