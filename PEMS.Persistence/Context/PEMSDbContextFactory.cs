using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PEMS.Persistence.Context;

public class PEMSDbContextFactory : IDesignTimeDbContextFactory<PEMSDbContext>
{
    public PEMSDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PEMSDbContext>();

        optionsBuilder.UseSqlServer(
            "Server=localhost\\SQLEXPRESS;Database=PEMSDB;Trusted_Connection=True;TrustServerCertificate=True;"
        );

        return new PEMSDbContext(optionsBuilder.Options);
    }
}