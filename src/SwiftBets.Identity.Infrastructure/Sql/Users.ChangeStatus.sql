UPDATE accounts.Users SET Status = @To, UpdatedAt = @Now WHERE UserId = @UserId AND Status = @From;
IF @@ROWCOUNT = 1
BEGIN
    INSERT INTO accounts.StatusHistory (UserId, PreviousStatus, Status, Reason, ChangedBy, ChangedAt)
    VALUES (@UserId, @From, @To, @Reason, @ChangedBy, @Now);
    SELECT 1;
END
ELSE
    SELECT 0;
