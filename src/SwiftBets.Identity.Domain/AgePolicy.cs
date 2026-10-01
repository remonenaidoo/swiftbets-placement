namespace SwiftBets.Identity.Domain;

/// <summary>The registration age gate (D99: 18 in South Africa). Age is counted in whole birthdays on the customer's date.</summary>
public static class AgePolicy
{
    public const int MinimumAge = 18;

    public static int AgeOn(DateOnly dateOfBirth, DateOnly today)
    {
        var age = today.Year - dateOfBirth.Year;
        return dateOfBirth > today.AddYears(-age) ? age - 1 : age;
    }

    public static bool IsOldEnough(DateOnly dateOfBirth, DateOnly today) =>
        dateOfBirth <= today && AgeOn(dateOfBirth, today) >= MinimumAge;
}
