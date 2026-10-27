using Ardak.Model.Enums;

namespace Ardak.Model.Entities;

public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? NationalIdOrTaxNumber { get; set; }
    
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;
    
    /// <summary>
    /// Confidential internal notes - strictly for internal staff (SuperAdmin/Admin).
    /// Must never be exposed to the customer portal.
    /// </summary>
    public string? InternalNotes { get; set; }
    
    public bool IsActive => Status == CustomerStatus.Active;
    
    /// <summary>
    /// The linked ASP.NET Identity User ID for customer portal authentication.
    /// Null if the customer has not yet been provisioned a login account.
    /// </summary>
    public string? IdentityUserId { get; set; }
    
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAtUtc { get; set; }

    /// <summary>
    /// Primary properties owned or managed by this customer.
    /// </summary>
    public ICollection<Property> Properties { get; set; } = new List<Property>();

    /// <summary>
    /// Property associations (co-ownership, shared interest, etc.).
    /// </summary>
    public ICollection<PropertyCustomer> PropertyAssociations { get; set; } = new List<PropertyCustomer>();

    /// <summary>
    /// Documents associated with this customer.
    /// </summary>
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
