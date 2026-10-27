using Ardak.Application.DTOs;

namespace Ardak.Application.Interfaces;

public interface ICustomerPortalService
{
    /// <summary>
    /// Loads complete dashboard data for the authenticated customer.
    /// Strictly verifies customer association and strips all confidential internal notes,
    /// financials, and unapproved procedures or documents.
    /// </summary>
    Task<CustomerPortalDashboardDto?> GetCustomerDashboardAsync(string currentUserId);

    /// <summary>
    /// Returns approved customer profile fields. Returns null if unauthorized or not found.
    /// </summary>
    Task<CustomerProfileDto?> GetApprovedCustomerProfileAsync(int customerId, string currentUserId);

    /// <summary>
    /// Returns all properties owned by or associated with the customer.
    /// </summary>
    Task<IReadOnlyList<CustomerPropertySummaryDto>> GetCustomerPropertiesAsync(string currentUserId);

    /// <summary>
    /// Returns approved property details including visible procedures and documents.
    /// Returns null if the customer is not associated with this property.
    /// </summary>
    Task<CustomerPropertyDetailDto?> GetCustomerPropertyDetailAsync(int propertyId, string currentUserId);

    /// <summary>
    /// Returns approved procedure status details only if the procedure is linked to the customer's
    /// property AND IsCustomerVisible is explicitly true. Returns null otherwise.
    /// </summary>
    Task<CustomerProcedureDto?> GetCustomerProcedureDetailAsync(int procedureId, string currentUserId);

    /// <summary>
    /// Returns all documents approved for customer visibility across the customer's properties.
    /// </summary>
    Task<IReadOnlyList<CustomerDocumentDto>> GetCustomerDocumentsAsync(string currentUserId);
}
