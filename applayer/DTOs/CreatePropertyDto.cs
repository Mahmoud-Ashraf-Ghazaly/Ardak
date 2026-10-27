using System.ComponentModel.DataAnnotations;
using Ardak.Model.Enums;

namespace Ardak.Application.DTOs;

public class CreatePropertyDto
{
    [Required(ErrorMessage = "Property Reference Number is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Reference number must be between 3 and 50 characters.")]
    [Display(Name = "Reference Number (e.g. PRP-2026-0001)")]
    public string ReferenceNumber { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Property Type")]
    public PropertyType Type { get; set; } = PropertyType.Land;

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
    [Display(Name = "Property Title / Name")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
    [Display(Name = "Description (Optional)")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Governorate / Region is required.")]
    [StringLength(100, ErrorMessage = "Governorate cannot exceed 100 characters.")]
    [Display(Name = "Governorate / Region")]
    public string Governorate { get; set; } = string.Empty;

    [Required(ErrorMessage = "City is required.")]
    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    [Display(Name = "City")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "District / Area is required.")]
    [StringLength(100, ErrorMessage = "District cannot exceed 100 characters.")]
    [Display(Name = "District / Area")]
    public string DistrictOrArea { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Address details cannot exceed 500 characters.")]
    [Display(Name = "Address Details (Street, Plot #, etc.)")]
    public string? AddressDetails { get; set; }

    [Required(ErrorMessage = "Area measurement is required.")]
    [Range(0.01, 100000000.0, ErrorMessage = "Area must be greater than zero.")]
    [Display(Name = "Area Measurement")]
    public decimal AreaMeasurement { get; set; }

    [Required(ErrorMessage = "Area unit is required.")]
    [StringLength(20, ErrorMessage = "Unit cannot exceed 20 characters.")]
    [Display(Name = "Area Unit")]
    public string AreaUnit { get; set; } = "sqm";

    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90 degrees.")]
    [Display(Name = "Latitude (Optional)")]
    public double? Latitude { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180 degrees.")]
    [Display(Name = "Longitude (Optional)")]
    public double? Longitude { get; set; }

    [StringLength(100, ErrorMessage = "Ownership type cannot exceed 100 characters.")]
    [Display(Name = "Ownership Type (Freehold, Leasehold, etc.)")]
    public string? OwnershipType { get; set; }

    [StringLength(100, ErrorMessage = "Deed/Title number cannot exceed 100 characters.")]
    [Display(Name = "Deed / Title / Sukuk Number")]
    public string? DeedOrTitleNumber { get; set; }

    [Required(ErrorMessage = "Primary Customer is required.")]
    [Display(Name = "Primary Customer")]
    public int PrimaryCustomerId { get; set; }

    [StringLength(4000, ErrorMessage = "Internal notes cannot exceed 4000 characters.")]
    [Display(Name = "Internal Staff Notes (Confidential)")]
    public string? InternalNotes { get; set; }
}
