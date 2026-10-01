namespace SwiftBets.Identity.Domain;

/// <summary>Fine-grained staff permissions carried in the <c>perm</c> claim. Customer tokens carry none (D100).</summary>
public static class Permissions
{
    public const string UsersRead = "identity.users.read";
    public const string UsersStatusWrite = "identity.users.status.write";
}
