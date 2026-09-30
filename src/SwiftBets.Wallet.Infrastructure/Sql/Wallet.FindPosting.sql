SELECT PostingId, Kind, AccountId, Amount, ReservationId
FROM wallet.Postings
WHERE IdempotencyKey = @IdempotencyKey;
