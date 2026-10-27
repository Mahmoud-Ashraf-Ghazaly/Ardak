using Ardak.Application.DTOs;

namespace Ardak.Application.Interfaces;

public interface IDashboardService
{
    /// <summary>
    /// Loads aggregated management dashboard metrics, grouped counts, actionable lists,
    /// and recent administrative activities asynchronously without loading full tables.
    /// </summary>
    Task<AdminDashboardDto> GetDashboardMetricsAsync(int topItems = 8);
}
