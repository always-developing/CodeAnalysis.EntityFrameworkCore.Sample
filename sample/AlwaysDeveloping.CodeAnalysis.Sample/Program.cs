using AlwaysDeveloping.CodeAnalysis.Sample;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = Host.CreateDefaultBuilder()
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((context, services) => services
        .AddDbContext<SampleContext>(x => x.UseSqlite(context.Configuration.GetConnectionString("SampleDatabase")))
    ).Build();


var context = host.Services.GetService<SampleContext>();
context?.Database.Migrate();
