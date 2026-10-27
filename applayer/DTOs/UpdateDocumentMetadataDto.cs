using System.ComponentModel.DataAnnotations;
using Ardak.Model.Enums;

namespace Ardak.Application.DTOs;

public class UpdateDocumentMetadataDto
{
    [Required]
    public int Id { get; set; }

    [Required(ErrorMessage = "Document type is required.")]
    [Display(Name = "Document Type")]
    public DocumentType Type { get; set; } = DocumentType.Other;

    [StringLength(100, ErrorMessage = "Type label cannot exceed 100 characters.")]
    [Display(Name = "Document Subtype / Label")]
    public string? TypeLabel { get; set; }

    [Required(ErrorMessage = "Document title is required.")]
    [StringLength(200, ErrorMessage = "Document title cannot exceed 200 characters.")]
    [Display(Name = "Document Title")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    [Display(Name = "Description / Summary")]
    public string? Description { get; set; }

    [StringLength(100, ErrorMessage = "Reference number cannot exceed 100 characters.")]
    [Display(Name = "Reference / Certificate / Deed Number")]
    public string? ReferenceNumber { get; set; }

    [Required]
    [Display(Name = "Confidentiality Level")]
    public DocumentConfidentialityLevel ConfidentialityLevel { get; set; } = DocumentConfidentialityLevel.Internal;

    [Display(Name = "Allow Customer Access (Private by default)")]
    public bool IsCustomerVisible { get; set; } = false;

    [StringLength(4000, ErrorMessage = "Internal notes cannot exceed 4000 characters.")]
    [Display(Name = "Internal Staff Notes (Confidential)")]
    public string? InternalNotes { get; set; }
}
