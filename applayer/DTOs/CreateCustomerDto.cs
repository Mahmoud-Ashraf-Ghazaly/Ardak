using System.ComponentModel.DataAnnotations;

namespace Ardak.Application.DTOs;

public class CreateCustomerDto
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Full Name must be between 2 and 150 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone Number is required.")]
    [Phone(ErrorMessage = "Invalid phone number format.")]
    [StringLength(50, ErrorMessage = "Phone number cannot exceed 50 characters.")]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(200, ErrorMessage = "Email cannot exceed 200 characters.")]
    [Display(Name = "Email Address (Optional)")]
    public string? Email { get; set; }

    [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters.")]
    [Display(Name = "Physical / Business Address (Optional)")]
    public string? Address { get; set; }

    [StringLength(50, ErrorMessage = "National ID / Tax Number cannot exceed 50 characters.")]
    [Display(Name = "National ID / Tax Number (Optional)")]
    public string? NationalIdOrTaxNumber { get; set; }

    [StringLength(2000, ErrorMessage = "Internal notes cannot exceed 2000 characters.")]
    [Display(Name = "Internal Staff Notes (Confidential)")]
    public string? InternalNotes { get; set; }
}
