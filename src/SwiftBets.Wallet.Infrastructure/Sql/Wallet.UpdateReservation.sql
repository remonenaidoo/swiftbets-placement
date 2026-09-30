UPDATE wallet.Reservations SET State = @State, UpdatedAt = @Now WHERE ReservationId = @ReservationId;
