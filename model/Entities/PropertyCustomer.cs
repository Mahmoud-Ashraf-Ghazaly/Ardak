namespace Ardak.Model.Entities;

public class PropertyCustomer
{
    public int PropertyId { get; set; }
    public Property? Property { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    /// <summary>
    /// Role or ownership relation, e.g. "Primary Owner", "Co-Owner", "Investor", "Representative".
    /// </summary>
    public string OwnershipRole { get; set; } = "Owner";

    /// <summary>
    /// Ownership share percentage when applicable (e.g. 50.00 for 50%).
    /// </summary>
    public decimal? OwnershipPercentage { get; set; }

    public DateTime AssociatedAtUtc { get; set; } = DateTime.UtcNow;
}
