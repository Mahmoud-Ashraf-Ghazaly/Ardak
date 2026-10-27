using Ardak.Application.DTOs;

namespace Ardak.Application.Interfaces;

public interface IProcedureService
{
    /// <summary>
    /// Search / filter procedures. Returns summary rows (no confidential data).
    /// </summary>
    Task<IReadOnlyList<ProcedureSummaryDto>> SearchProceduresAsync(ProcedureSearchFilterDto filter);

    /// <summary>
    /// Full internal detail including confidential fields. Returns null if not found.
    /// </summary>
    Task<ProcedureDetailDto?> GetProcedureByIdAsync(int id);

    /// <summary>
    /// Creates a new procedure for the specified property.
    /// </summary>
    Task<ServiceResult<int>> CreateProcedureAsync(CreateProcedureDto dto, string currentUserId);

    /// <summary>
    /// Updates an existing procedure. If the status changed, a history record is written.
    /// </summary>
    Task<ServiceResult> UpdateProcedureAsync(UpdateProcedureDto dto, string currentUserId);

    /// <summary>
    /// Returns all procedures for a given property (summary only).
    /// </summary>
    Task<IReadOnlyList<ProcedureSummaryDto>> GetProceduresByPropertyAsync(int propertyId);
}
