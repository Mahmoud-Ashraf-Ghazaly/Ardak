using Ardak.Application.DTOs;

namespace Ardak.Application.Interfaces;

public interface IPropertyService
{
    Task<IReadOnlyList<PropertySummaryDto>> SearchPropertiesAsync(PropertySearchFilterDto filter);
    Task<PropertyDetailDto?> GetPropertyByIdAsync(int id);
    Task<PropertyDetailDto?> GetPropertyByReferenceAsync(string referenceNumber);
    Task<ServiceResult<int>> CreatePropertyAsync(CreatePropertyDto dto, string currentUserId);
    Task<ServiceResult> UpdatePropertyAsync(UpdatePropertyDto dto, string currentUserId);
    Task<ServiceResult> ArchivePropertyAsync(int id, string currentUserId);
    Task<ServiceResult> RestorePropertyAsync(int id, string currentUserId);
    Task<ServiceResult> AssociateCustomerAsync(int propertyId, int customerId, string role, decimal? ownershipPercentage, string currentUserId);
    Task<ServiceResult> RemoveCustomerAssociationAsync(int propertyId, int customerId, string currentUserId);
}
