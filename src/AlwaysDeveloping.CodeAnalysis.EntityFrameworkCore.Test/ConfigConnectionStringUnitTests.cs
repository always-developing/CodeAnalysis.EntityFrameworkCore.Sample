using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;

namespace AlwaysDeveloping.CodeAnalysis.EntityFrameworkCore.Test
{
    [TestClass]
    public class ConfigConnectionStringUnitTests
    {
        [TestMethod]
        public async Task NoConfig_DebugBuild()
        {
            var buildConfig = "DEBUG";
            var directiveCheck = "";

            string sourceCode = GenerateSourceCode(directiveCheck);
            var analyzerTest = GenerateAnalyzerTest(sourceCode, buildConfig, false);

            analyzerTest.ExpectedDiagnostics.Add(new DiagnosticResult("ADEF002", Microsoft.CodeAnalysis.DiagnosticSeverity.Error).WithNoLocation());

            await analyzerTest.RunAsync();
        }

        [TestMethod]
        public async Task BlankConfig_DebugBuild()
        {
            var buildConfig = "DEBUG";
            var directiveCheck = "";

            string sourceCode = GenerateSourceCode(directiveCheck);
            var analyzerTest = GenerateAnalyzerTest(sourceCode, buildConfig, true, "");

            analyzerTest.ExpectedDiagnostics.Add(new DiagnosticResult("ADEF003", Microsoft.CodeAnalysis.DiagnosticSeverity.Error).WithLocation(14, 63));

            await analyzerTest.RunAsync();
        }

        [TestMethod]
        public async Task ValidConfig_DebugBuild()
        {
            var buildConfig = "DEBUG";
            var directiveCheck = "";

            string sourceCode = GenerateSourceCode(directiveCheck);
            var analyzerTest = GenerateAnalyzerTest(sourceCode, buildConfig, true, "{\"ConnectionStrings\": { \"SampleDatabase\": \"Data Source=LocalDatabase.db\" }}");

            await analyzerTest.RunAsync();
        }

        [TestMethod]
        public async Task InValidConnectionString_DebugBuild_WithFix()
        {
            var buildConfig = "DEBUG";
            var directiveCheck = "";

            string sourceCode = GenerateSourceCode(directiveCheck);
            var analyzerFix = GenerateAnalyzerTest(sourceCode, buildConfig, true, "{\"ConnectionStrings\": { \"DatabaseSample\": \"Data Source=LocalDatabase.db\" }}");

            analyzerFix.ExpectedDiagnostics.Add(new DiagnosticResult("ADEF003", Microsoft.CodeAnalysis.DiagnosticSeverity.Error).WithLocation(14, 63));

            await analyzerFix.RunAsync();
        }

        private static CSharpAnalyzerTest<ConfigConnectionStringAnalyzer, MSTestVerifier> GenerateAnalyzerTest(string sourceCode, string buildConfig, bool addAppSettings = true, string appsettingContent = "", List<DiagnosticResult> expectedResults = null)
        {
            var packages = GenerateRequirePackages();

            var analyzerTest =  new CSharpAnalyzerTest<ConfigConnectionStringAnalyzer, MSTestVerifier>
            {
                TestState =
                {
                    Sources = { sourceCode },
                    ReferenceAssemblies = new ReferenceAssemblies("net6.0", new PackageIdentity("Microsoft.NETCore.App.Ref", "6.0.0"), Path.Combine("ref", "net6.0"))
                        .AddPackages(packages)
                }
            };

            if(addAppSettings)
            {
                analyzerTest.TestState.AdditionalFiles.Add(("appsettings.json", appsettingContent));
            }

            // set the build config
            analyzerTest.SolutionTransforms.Add((s, p) =>
            {
                return s.WithProjectParseOptions(p, new CSharpParseOptions().WithPreprocessorSymbols(buildConfig));
            });

            return analyzerTest;
        }

        private static CSharpCodeFixTest<ConfigConnectionStringAnalyzer, ConfigConnectionStringCodeFixProvider, MSTestVerifier> GenerateFixVerifierTest(string sourceCode, string fixCode, string buildConfig, bool addTestStateAppSettings = true, string testStateAppSettingContent = "", bool addFinalStateAppSettings = true, string finalStateAppSettingContent = "", List<DiagnosticResult> expectedResults = null)
        {
            var packages = GenerateRequirePackages();

            var analyzerFix = new CSharpCodeFixTest<ConfigConnectionStringAnalyzer, ConfigConnectionStringCodeFixProvider, MSTestVerifier>
            {
                TestState =
                {
                    Sources = { sourceCode },
                    ReferenceAssemblies = new ReferenceAssemblies("net6.0", new PackageIdentity("Microsoft.NETCore.App.Ref", "6.0.0"), Path.Combine("ref", "net6.0"))
                        .AddPackages(packages)
                },
                FixedState =
                {
                    Sources = { fixCode },
                    ReferenceAssemblies = new ReferenceAssemblies("net6.0", new PackageIdentity("Microsoft.NETCore.App.Ref", "6.0.0"), Path.Combine("ref", "net6.0"))
                        .AddPackages(packages)
                },
            };

            if (addTestStateAppSettings)
            {
                analyzerFix.TestState.AdditionalFiles.Add(("appsettings.json", testStateAppSettingContent));
            }

            if (addFinalStateAppSettings)
            {
                analyzerFix.FixedState.AdditionalFiles.Add(("appsettings.json", finalStateAppSettingContent));
            }

            analyzerFix.SolutionTransforms.Add((s, p) =>
            {
                return s.WithProjectParseOptions(p, new CSharpParseOptions().WithPreprocessorSymbols(buildConfig));
            });

            return analyzerFix;
        }

        private static ImmutableArray<PackageIdentity> GenerateRequirePackages()
        {
            // include any nuget packages to reduce the number of errors
            return new[] {
                new PackageIdentity("Microsoft.Extensions.Hosting", "6.0.0"),
                new PackageIdentity("Microsoft.Extensions.Configuration", "6.0.0"),
                new PackageIdentity("Microsoft.EntityFrameworkCore", "6.0.0"),
                new PackageIdentity("Microsoft.EntityFrameworkCore.Sqlite", "6.0.0")
            }
            .ToImmutableArray();
        }

        private static string GenerateSourceCode(string directiveCheck, string triva = "")
        {
            var ifDirective = string.IsNullOrEmpty(directiveCheck) ? string.Empty : $"{Environment.NewLine}#if {directiveCheck}";
            var endIfDirective = string.IsNullOrEmpty(directiveCheck) ? string.Empty : $"{Environment.NewLine}#endif";

            var insertTrivia = string.IsNullOrEmpty(triva) ? string.Empty : $"{Environment.NewLine}triva";

            return $@"using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

public class Program
{{   

    public static void Main()
    {{
        IHost host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) => services{triva}
                .AddDbContext<SampleContext>(x => x.UseSqlite(context.Configuration.GetConnectionString(""SampleDatabase"")))
        ).Build();

        var context = host.Services.GetService<SampleContext>();{ifDirective}
        context?.Database.Migrate();{endIfDirective}
    }}
}}

public class SampleEntity
{{
    public Guid Id {{ get; set; }}

    public string? Name {{ get; set; }}

    public string? Code {{ get; set; }}
}}

public class SampleContext : DbContext
{{
    public SampleContext(DbContextOptions<SampleContext> options) : base(options)
    {{
    }}

    public DbSet<SampleEntity>? SampleEntity {{ get; set; }}
}}";
        }
    }
}
