namespace SwiftBets.Placement.Domain.Sagas;

/// <summary>Wallet idempotency keys derived from the coupon, so every retry of a step hits the same key.</summary>
public static class SagaKeys
{
    public static string Reserve(Guid couponId) => $"{couponId:N}_reserve";

    public static string Capture(Guid couponId) => $"{couponId:N}_capture";

    public static string Release(Guid couponId) => $"{couponId:N}_release";
}
