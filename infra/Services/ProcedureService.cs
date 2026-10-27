using Ardak.Application.DTOs;
using Ardak.Application.Interfaces;
using Ardak.Infrastructure.Data;
using Ardak.Model.Entities;
using Ardak.Model.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ardak.Infrastructure.Services;

public class ProcedureService : IProcedureService
{
    private readonly ArdakDbContext _db;
    private readonly ILogger<ProcedureService> _logger;

    public ProcedureService(ArdakDbContext db, ILogger<ProcedureService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Search ───────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ProcedureSummaryDto>> SearchProceduresAsync(ProcedureSearchFilterDto filter)
    {
        var query = _db.Procedures
            .AsNoTracking()
            .Include(p => p.Property)
            .AsQueryable();

        if (filter.PropertyId.HasValue)
            query = query.Where(p => p.PropertyId == filter.PropertyId.Value);

        if (filter.Type.HasValue)
            query = query.Where(p => p.Type == filter.Type.Value);

        if (filter.Status.HasValue)
            query = query.Where(p => p.Status == filter.Status.Value);

        if (filter.OverdueOnly == true)
        {
            var today = DateTime.UtcNow.Date;
            query = query.Where(p =>
                p.NextActionDeadline.HasValue &&
                p.NextActionDeadline.Value.Date < today);
        }

        return await query
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new ProcedureSummaryDto
            {
                Id = p.Id,
                PropertyId = p.PropertyId,
                PropertyReferenceNumber = p.Property != null ? p.Property.ReferenceNumber : string.Empty,
                PropertyTitle = p.Property != null ? p.Property.Title : string.Empty,
                Type = p.Type,
                TypeLabel = p.TypeLabel ?? p.Type.ToString(),
                ApplicationNumber = p.ApplicationNumber,
                ResponsibleAuthority = p.ResponsibleAuthority,
                Status = p.Status,
                SubmissionDate = p.SubmissionDate,
                NextActionDeadline = p.NextActionDeadline,
                NextActionDescription = p.NextActionDescription,
                CreatedAtUtc = p.CreatedAtUtc
            })
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ProcedureSummaryDto>> GetProceduresByPropertyAsync(int propertyId)
        => await SearchProceduresAsync(new ProcedureSearchFilterDto { PropertyId = propertyId });

    // ── Detail ───────────────────────────────────────────────────────

    public async Task<ProcedureDetailDto?> GetProcedureByIdAsync(int id)
    {
        var procedure = await _db.Procedures
            .AsNoTracking()
            .Include(p => p.Property)
            .Include(p => p.StatusHistory.OrderByDescending(h => h.ChangedAtUtc))
            .FirstOrDefaultAsync(p => p.Id == id);

        if (procedure == null)
            return null;

        return MapToDetailDto(procedure);
    }

    // ── Create ───────────────────────────────────────────────────────

    public async Task<ServiceResult<int>> CreateProcedureAsync(CreateProcedureDto dto, string currentUserId)
    {
        var property = await _db.Properties.FindAsync(dto.PropertyId);
        if (property == null)
            return ServiceResult<int>.Failure("The specified property does not exist.");

        var balanceDue = ComputeBalance(dto.TotalFee, dto.AmountPaid);

        var procedure = new Procedure
        {
            PropertyId = dto.PropertyId,
            Type = dto.Type,
            TypeLabel = string.IsNullOrWhiteSpace(dto.TypeLabel) ? null : dto.TypeLabel.Trim(),
            ApplicationNumber = string.IsNullOrWhiteSpace(dto.ApplicationNumber) ? null : dto.ApplicationNumber.Trim(),
            ResponsibleAuthority = string.IsNullOrWhiteSpace(dto.ResponsibleAuthority) ? null : dto.ResponsibleAuthority.Trim(),
            Status = dto.Status,
            SubmissionDate = dto.SubmissionDate,
            NextActionDeadline = dto.NextActionDeadline,
            NextActionDescription = string.IsNullOrWhiteSpace(dto.NextActionDescription) ? null : dto.NextActionDescription.Trim(),
            MissingDocuments = string.IsNullOrWhiteSpace(dto.MissingDocuments) ? null : dto.MissingDocuments.Trim(),
            TotalFee = dto.TotalFee,
            AmountPaid = dto.AmountPaid,
            BalanceDue = balanceDue,
            InternalNotes = string.IsNullOrWhiteSpace(dto.InternalNotes) ? null : dto.InternalNotes.Trim(),
            IsCustomerVisible = dto.IsCustomerVisible,
            CustomerUpdateNotes = string.IsNullOrWhiteSpace(dto.CustomerUpdateNotes) ? null : dto.CustomerUpdateNotes.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Procedures.Add(procedure);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Staff '{UserId}' created procedure ID {ProcedureId} (Type: {Type}, Status: {Status}) for property ID {PropertyId}.",
            currentUserId, procedure.Id, procedure.Type, procedure.Status, procedure.PropertyId);

        return ServiceResult<int>.Success(procedure.Id);
    }

    // ── Update ───────────────────────────────────────────────────────

    public async Task<ServiceResult> UpdateProcedureAsync(UpdateProcedureDto dto, string currentUserId)
    {
        var procedure = await _db.Procedures
            .Include(p => p.StatusHistory)
            .FirstOrDefaultAsync(p => p.Id == dto.Id);

        if (procedure == null)
            return ServiceResult.Failure("Procedure record not found.");

        var property = await _db.Properties.FindAsync(dto.PropertyId);
        if (property == null)
            return ServiceResult.Failure("The specified property does not exist.");

        // Write status history if status actually changed
        if (procedure.Status != dto.Status)
        {
            procedure.StatusHistory.Add(new ProcedureStatusHistory
            {
                ProcedureId = procedure.Id,
                FromStatus = procedure.Status,
                ToStatus = dto.Status,
                ChangeNote = string.IsNullOrWhiteSpace(dto.StatusChangeNote) ? null : dto.StatusChangeNote.Trim(),
                ChangedByUserId = currentUserId,
                ChangedAtUtc = DateTime.UtcNow
            });

            _logger.LogInformation(
                "Staff '{UserId}' changed procedure ID {ProcedureId} status from {From} to {To}.",
                currentUserId, procedure.Id, procedure.Status, dto.Status);
        }

        procedure.PropertyId = dto.PropertyId;
        procedure.Type = dto.Type;
        procedure.TypeLabel = string.IsNullOrWhiteSpace(dto.TypeLabel) ? null : dto.TypeLabel.Trim();
        procedure.ApplicationNumber = string.IsNullOrWhiteSpace(dto.ApplicationNumber) ? null : dto.ApplicationNumber.Trim();
        procedure.ResponsibleAuthority = string.IsNullOrWhiteSpace(dto.ResponsibleAuthority) ? null : dto.ResponsibleAuthority.Trim();
        procedure.Status = dto.Status;
        procedure.SubmissionDate = dto.SubmissionDate;
        procedure.NextActionDeadline = dto.NextActionDeadline;
        procedure.NextActionDescription = string.IsNullOrWhiteSpace(dto.NextActionDescription) ? null : dto.NextActionDescription.Trim();
        procedure.MissingDocuments = string.IsNullOrWhiteSpace(dto.MissingDocuments) ? null : dto.MissingDocuments.Trim();
        procedure.TotalFee = dto.TotalFee;
        procedure.AmountPaid = dto.AmountPaid;
        procedure.BalanceDue = ComputeBalance(dto.TotalFee, dto.AmountPaid);
        procedure.InternalNotes = string.IsNullOrWhiteSpace(dto.InternalNotes) ? null : dto.InternalNotes.Trim();
        procedure.IsCustomerVisible = dto.IsCustomerVisible;
        procedure.CustomerUpdateNotes = string.IsNullOrWhiteSpace(dto.CustomerUpdateNotes) ? null : dto.CustomerUpdateNotes.Trim();
        procedure.ModifiedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Staff '{UserId}' updated procedure ID {ProcedureId}.",
            currentUserId, procedure.Id);

        return ServiceResult.Success();
    }

    // ── Private helpers ──────────────────────────────────────────────

    private static decimal? ComputeBalance(decimal? total, decimal? paid)
    {
        if (total.HasValue)
            return total.Value - (paid ?? 0m);
        return null;
    }

    private static ProcedureDetailDto MapToDetailDto(Procedure p) => new()
    {
        Id = p.Id,
        PropertyId = p.PropertyId,
        PropertyReferenceNumber = p.Property?.ReferenceNumber ?? string.Empty,
        PropertyTitle = p.Property?.Title ?? string.Empty,
        Type = p.Type,
        TypeLabel = p.TypeLabel ?? p.Type.ToString(),
        ApplicationNumber = p.ApplicationNumber,
        ResponsibleAuthority = p.ResponsibleAuthority,
        Status = p.Status,
        SubmissionDate = p.SubmissionDate,
        NextActionDeadline = p.NextActionDeadline,
        NextActionDescription = p.NextActionDescription,
        MissingDocuments = p.MissingDocuments,
        TotalFee = p.TotalFee,
        AmountPaid = p.AmountPaid,
        BalanceDue = p.BalanceDue,
        InternalNotes = p.InternalNotes,
        IsCustomerVisible = p.IsCustomerVisible,
        CustomerUpdateNotes = p.CustomerUpdateNotes,
        CreatedAtUtc = p.CreatedAtUtc,
        ModifiedAtUtc = p.ModifiedAtUtc,
        StatusHistory = p.StatusHistory
            .OrderByDescending(h => h.ChangedAtUtc)
            .Select(h => new ProcedureStatusHistoryDto
            {
                Id = h.Id,
                FromStatus = h.FromStatus,
                ToStatus = h.ToStatus,
                ChangeNote = h.ChangeNote,
                ChangedByUserId = h.ChangedByUserId,
                ChangedAtUtc = h.ChangedAtUtc
            })
            .ToList()
    };
}
