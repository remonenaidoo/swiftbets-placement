SELECT PostingId, Kind, AccountId, Amount, ReservationId
FROM wallet.Postings WITH (UPDLOCK, HOLDLOCK)
WHERE IdempotencyKey = @IdempotencyKey;
