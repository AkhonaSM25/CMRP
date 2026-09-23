namespace CMRP.Models;

/// <summary>Role names used by Identity, [Authorize] attributes and the seed routine (no magic strings).</summary>
public static class Roles
{
    public const string Reporter = "Reporter";
    public const string Technician = "Technician";
    public const string Coordinator = "Coordinator";
    public const string Administrator = "Administrator";

    /// <summary>Roles that triage, prioritise and assign work.</summary>
    public const string Managers = Coordinator + "," + Administrator;

    /// <summary>Every role that may work on reports.</summary>
    public const string AllStaff = Coordinator + "," + Administrator + "," + Technician;

    public static readonly string[] All = { Reporter, Technician, Coordinator, Administrator };
}
