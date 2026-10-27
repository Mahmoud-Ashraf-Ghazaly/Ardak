namespace Ardak.Application.DTOs;

public class AssociatedCustomerDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string OwnershipRole { get; set; } = "Co-Owner";
    public decimal? OwnershipPercentage { get; set; }
    public DateTime AssociatedAtUtc { get; set; }
}
