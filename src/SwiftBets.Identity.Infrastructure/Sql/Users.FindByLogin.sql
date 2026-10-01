SELECT u.UserId, u.Email, u.Username, u.PasswordHash, u.DateOfBirth, u.Brand, u.Country, u.Currency, u.Status, u.EmailVerifiedAt,
       u.FailedSignIns, u.LockedUntil, u.ParentUserId,
       (SELECT STRING_AGG(r.Role, ',') FROM accounts.UserRoles r WHERE r.UserId = u.UserId) AS Roles
FROM accounts.Users u
WHERE u.NormalizedEmail = @Login OR u.Username = @Login;
