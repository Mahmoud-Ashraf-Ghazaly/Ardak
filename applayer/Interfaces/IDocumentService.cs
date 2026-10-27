using Ardak.Application.DTOs;

namespace Ardak.Application.Interfaces;

public interface IDocumentService
{
    /// <summary>
    /// Validates file, stores stream securely, and persists document metadata.
    /// Documents are private by default (IsCustomerVisible = false).
    /// </summary>
    Task<ServiceResult<int>> UploadDocumentAsync(
        UploadDocumentDto dto,
        Stream fileStream,
        string fileName,
        string? contentType,
        long fileLength,
        string currentUserId);

    /// <summary>
    /// Retrieves full document detail for authorized users.
    /// Returns null if not found or unauthorized.
    /// </summary>
    Task<DocumentDetailDto?> GetDocumentByIdAsync(int id, string currentUserId);

    /// <summary>
    /// Searches and filters documents based on user permissions.
    /// Staff can view all; customers can only view visible documents for their properties/profile.
    /// </summary>
    Task<IReadOnlyList<DocumentSummaryDto>> SearchDocumentsAsync(DocumentSearchFilterDto filter, string currentUserId);

    Task<IReadOnlyList<DocumentSummaryDto>> GetDocumentsByCustomerAsync(int customerId, string currentUserId);
    Task<IReadOnlyList<DocumentSummaryDto>> GetDocumentsByPropertyAsync(int propertyId, string currentUserId);
    Task<IReadOnlyList<DocumentSummaryDto>> GetDocumentsByProcedureAsync(int procedureId, string currentUserId);

    /// <summary>
    /// Updates document metadata and customer visibility setting.
    /// </summary>
    Task<ServiceResult> UpdateDocumentMetadataAsync(UpdateDocumentMetadataDto dto, string currentUserId);

    /// <summary>
    /// Explicitly updates customer visibility on a document.
    /// </summary>
    Task<ServiceResult> SetCustomerVisibilityAsync(int documentId, bool isVisible, string currentUserId);

    /// <summary>
    /// Archives a document to preserve historical records without physical deletion.
    /// </summary>
    Task<ServiceResult> ArchiveDocumentAsync(int documentId, string currentUserId);

    /// <summary>
    /// Validates server-side authorization before opening file stream for download.
    /// Physical storage paths and credentials are never exposed.
    /// </summary>
    Task<ServiceResult<DocumentDownloadResult>> GetDocumentDownloadAsync(int documentId, string currentUserId);

    /// <summary>
    /// Validates whether the given user has permission to access (view or download) the document.
    /// </summary>
    Task<bool> CanUserAccessDocumentAsync(int documentId, string currentUserId, bool isDownload);
}
