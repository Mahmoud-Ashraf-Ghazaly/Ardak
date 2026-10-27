using Ardak.Application.DTOs;
using Ardak.Model.Enums;

namespace Ardak.Application.Interfaces;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerSummaryDto>> SearchCustomersAsync(string? searchTerm = null, CustomerStatus? status = null);
    Task<CustomerDetailDto?> GetCustomerByIdAsync(int id);
    Task<ServiceResult<int>> CreateCustomerAsync(CreateCustomerDto dto, string currentUserId);
    Task<ServiceResult> UpdateCustomerAsync(UpdateCustomerDto dto, string currentUserId);
    Task<ServiceResult> ArchiveCustomerAsync(int id, string currentUserId);
    Task<ServiceResult> RestoreCustomerAsync(int id, string currentUserId);
}
