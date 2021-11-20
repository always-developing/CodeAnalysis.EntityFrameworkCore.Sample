using Microsoft.EntityFrameworkCore;

namespace AlwaysDeveloping.CodeAnalysis.Sample;
public class SampleContext : DbContext
{
    public SampleContext(DbContextOptions<SampleContext> options) : base(options)
    {
    }

    public DbSet<SampleEntity>? SampleEntity { get; set; }
}
