using Ardak.Model.Enums;

namespace Ardak.Application.DTOs;

public class AdminDashboardDto
{
    // ── High Level Totals ───────────────────────────────────────────────────
    public int TotalCustomers { get; set; }
    public int ActiveCustomers { get; set; }

    public int TotalProperties { get; set; }
    public int ActiveProperties { get; set; }

    public int TotalProcedures { get; set; }
    public int PendingProceduresCount { get; set; }
    public int ProceduresWithMissingDocsCount { get; set; }
    public int OverdueDeadlinesCount { get; set; }

    public int TotalDocuments { get; set; }

    // ── Property Groupings ──────────────────────────────────────────────────
    public IReadOnlyDictionary<string, int> PropertiesByType { get; set; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> PropertiesByStatus { get; set; } = new Dictionary<string, int>();

    // ── Procedure Groupings ─────────────────────────────────────────────────
    public IReadOnlyDictionary<string, int> ProceduresByStatus { get; set; } = new Dictionary<string, int>();

    // ── Targeted Lists ──────────────────────────────────────────────────────
    public IReadOnlyList<ProcedureSummaryDto> PendingProcedures { get; set; } = [];
    public IReadOnlyList<ProcedureSummaryDto> ProceduresWithMissingDocuments { get; set; } = [];
    public IReadOnlyList<ProcedureSummaryDto> UpcomingDeadlines { get; set; } = [];

    // ── Recent Activity Feed ────────────────────────────────────────────────
    public IReadOnlyList<DashboardActivityDto> RecentActivities { get; set; } = [];
}

public class DashboardActivityDto
{
    public string ActivityType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? PerformedByUserId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string? LinkController { get; set; }
    public string? LinkAction { get; set; }
    public int? LinkId { get; set; }
    public string BadgeCss { get; set; } = "bg-primary";
}
