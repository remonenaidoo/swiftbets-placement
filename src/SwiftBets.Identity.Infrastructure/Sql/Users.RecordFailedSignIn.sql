UPDATE accounts.Users
SET FailedSignIns = FailedSignIns + 1,
    LockedUntil = CASE WHEN FailedSignIns + 1 >= @MaxFailures THEN @LockedUntil ELSE LockedUntil END,
    UpdatedAt = @Now
WHERE UserId = @UserId;
