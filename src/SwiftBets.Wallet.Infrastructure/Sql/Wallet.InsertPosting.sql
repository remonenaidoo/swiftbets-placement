INSERT INTO wallet.Postings (PostingId, Kind, IdempotencyKey, AccountId, Amount, ReservationId, Reference, PostedAt)
VALUES (@PostingId, @Kind, @IdempotencyKey, @AccountId, @Amount, @ReservationId, @Reference, @PostedAt);
