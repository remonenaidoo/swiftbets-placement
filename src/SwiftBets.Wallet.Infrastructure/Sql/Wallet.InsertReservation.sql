INSERT INTO wallet.Reservations (ReservationId, AccountId, Amount, Currency, Reference, State, CreatedAt, UpdatedAt)
VALUES (@ReservationId, @AccountId, @Amount, @Currency, @Reference, @State, @Now, @Now);
