SELECT ReservationId, AccountId, Amount, Currency, Reference, State
FROM wallet.Reservations
WHERE ReservationId = @ReservationId;
