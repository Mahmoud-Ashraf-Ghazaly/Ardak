using Ardak.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ardak.Application.Interfaces;

public interface IAppDbContext
{
    DbSet<Customer> Customers { get; }
    DbSet<Property> Properties { get; }
    DbSet<PropertyCustomer> PropertyCustomers { get; }
    DbSet<Procedure> Procedures { get; }
    DbSet<ProcedureStatusHistory> ProcedureStatusHistories { get; }
    DbSet<Document> Documents { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
