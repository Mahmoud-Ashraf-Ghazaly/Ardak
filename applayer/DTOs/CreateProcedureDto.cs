using System.ComponentModel.DataAnnotations;
using Ardak.Model.Enums;

namespace Ardak.Application.DTOs;

public class CreateProcedureDto
{
    [Required]
    public int PropertyId { get; set; }

    [Required(ErrorMessage = "Procedure type is required.")]
    [Display(Name = "Procedure Type")]
    public ProcedureType Type { get; set; } = ProcedureType.Other;

    [StringLength(100, ErrorMessage = "Type label cannot exceed 100 characters.")]
    [Display(Name = "Procedure Label (for Other types)")]
    public string? TypeLabel { get; set; }

    [StringLength(100, ErrorMessage = "Application number cannot exceed 100 characters.")]
    [Display(Name = "Application / Case Reference Number")]
    public string? ApplicationNumber { get; set; }

    [StringLength(200, ErrorMessage = "Authority name cannot exceed 200 characters.")]
    [Display(Name = "Responsible Authority / Department")]
    public string? ResponsibleAuthority { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    [Display(Name = "Current Status")]
    public ProcedureStatus Status { get; set; } = ProcedureStatus.Submitted;

    [DataType(DataType.Date)]
    [Display(Name = "Submission Date")]
    public DateTime? SubmissionDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Next Action Deadline")]
    public DateTime? NextActionDeadline { get; set; }

    [StringLength(500, ErrorMessage = "Next action description cannot exceed 500 characters.")]
    [Display(Name = "Next Action / Required Step")]
    public string? NextActionDescription { get; set; }

    [StringLength(2000, ErrorMessage = "Missing documents field cannot exceed 2000 characters.")]
    [Display(Name = "Missing / Outstanding Documents")]
    public string? MissingDocuments { get; set; }

    [Range(0, 999999999.99, ErrorMessage = "Total fee must be a positive value.")]
    [Display(Name = "Total Fee (SAR)")]
    public decimal? TotalFee { get; set; }

    [Range(0, 999999999.99, ErrorMessage = "Amount paid must be a positive value.")]
    [Display(Name = "Amount Paid (SAR)")]
    public decimal? AmountPaid { get; set; }

    [StringLength(4000, ErrorMessage = "Internal notes cannot exceed 4000 characters.")]
    [Display(Name = "Internal Staff Notes (Confidential)")]
    public string? InternalNotes { get; set; }

    [Display(Name = "Allow Customer to View Procedure Status (Private by default)")]
    public bool IsCustomerVisible { get; set; } = false;

    [StringLength(2000, ErrorMessage = "Customer update notes cannot exceed 2000 characters.")]
    [Display(Name = "Customer-Facing Status Update")]
    public string? CustomerUpdateNotes { get; set; }
}
