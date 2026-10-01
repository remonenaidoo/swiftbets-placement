UPDATE accounts.Users SET EmailVerifiedAt = COALESCE(EmailVerifiedAt, @Now), UpdatedAt = @Now WHERE UserId = @UserId;
