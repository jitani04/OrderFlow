namespace OrderFlow.Domain.Identity;

public static class Roles
{
    /// <summary>Manages the catalogue and sees every order.</summary>
    public const string Admin = "Admin";

    /// <summary>Shops, and sees only their own orders.</summary>
    public const string Customer = "Customer";

    public static bool IsKnown(string role) =>
        role is Admin or Customer;
}
