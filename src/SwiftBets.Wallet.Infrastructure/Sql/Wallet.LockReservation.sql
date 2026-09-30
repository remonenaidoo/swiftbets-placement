SELECT ReservationId, AccountId, Amount, Currency, Reference, State
FROM wallet.Reservations WITH (UPDLOCK, ROWLOCK)
WHERE ReservationId = @ReservationId;
