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
    public class DevOnlyMigrateUnitTests
    {

        [TestMethod]
        public async Task NoDirective_DebugBuild()
        {
            var buildConfig = "DEBUG";
            var directiveCheck = "";

            string sourceCode = GenerateSourceCode(directiveCheck);
            var analyzerTest = GenerateAnalyzerTest(sourceCode, buildConfig);

            analyzerTest.ExpectedDiagnostics.Add(new DiagnosticResult("ADEF001", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning).WithLocation(18, 27));

            await analyzerTest.RunAsync();
        }

        [TestMethod]
        public async Task NoDirective_ReleaseBuild()
        {
            var buildConfig = "RELEASE";
            var directiveCheck = "";

            string sourceCode = GenerateSourceCode(directiveCheck);
            var analyzerTest = GenerateAnalyzerTest(sourceCode, buildConfig);

            analyzerTest.ExpectedDiagnostics.Add(new DiagnosticResult("ADEF001", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning).WithLocation(18, 27));

            await analyzerTest.RunAsync();
        }

        [TestMethod]
        public async Task DebugDirective_DebugBuild()
        {
            var buildConfig = "DEBUG";
            var directiveCheck = "DEBUG";

            string sourceCode = GenerateSourceCode(directiveCheck);
            var analyzerTest = GenerateAnalyzerTest(sourceCode, buildConfig);

            await analyzerTest.RunAsync();
        }

        [TestMethod]
        public async Task DebugDirective_ReleaseBuild()
        {
            var buildConfig = "RELEASE";
            var directiveCheck = "DEBUG";

            string sourceCode = GenerateSourceCode(directiveCheck);
            var analyzerTest = GenerateAnalyzerTest(sourceCode, buildConfig);

            await analyzerTest.RunAsync();
        }

        [TestMethod]
        public async Task ReleaseDirective_DebugBuild()
        {
            var buildConfig = "DEBUG";
            var directiveCheck = "RELEASE";

            string sourceCode = GenerateSourceCode(directiveCheck);
            var analyzerTest = GenerateAnalyzerTest(sourceCode, buildConfig);

            await analyzerTest.RunAsync();
        }

        [TestMethod]
        public async Task ReleaseDirective_ReleaseBuild()
        {
            var buildConfig = "RELEASE";
            var directiveCheck = "RELEASE";

            string sourceCode = GenerateSourceCode(directiveCheck);
            var analyzerTest = GenerateAnalyzerTest(sourceCode, buildConfig);

            analyzerTest.ExpectedDiagnostics.Add(new DiagnosticResult("ADEF001", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning).WithLocation(19, 27));

            await analyzerTest.RunAsync();
        }

        [TestMethod]
        public async Task NotLocalDirective_DebugBuild()
        {
            var buildConfig = "DEBUG";
            var directiveCheck = "!LOCAL";

            string sourceCode = GenerateSourceCode(directiveCheck);
            var analyzerTest = GenerateAnalyzerTest(sourceCode, buildConfig);

            analyzerTest.ExpectedDiagnostics.Add(new DiagnosticResult("ADEF001", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning).WithLocation(19, 27));

            await analyzerTest.RunAsync();
        }

        [TestMethod]
        public async Task NoDirective_DebugBuild_WithFix()
        {
            var buildConfig = "DEBUG";
            var directiveCheck = "";

            string sourceCode = GenerateSourceCode(directiveCheck);
            string resultCode = GenerateSourceCode("DEBUG");
            var analyzerTest = GenerateFixVerifierTest(sourceCode, resultCode, buildConfig);

            analyzerTest.ExpectedDiagnostics.Add(new DiagnosticResult("ADEF001", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning).WithLocation(18, 27));

            await analyzerTest.RunAsync();
        }

        [TestMethod]
        public async Task NoDirective_ReleaseBuild_WithFix()
        {
            var buildConfig = "RELEASE";
            var directiveCheck = "";

            string sourceCode = GenerateSourceCode(directiveCheck);
            string resultCode = GenerateSourceCode("DEBUG");

            var analyzerTest = GenerateFixVerifierTest(sourceCode, resultCode, buildConfig);

            // This tells test to just compare the text
            analyzerTest.CodeActionValidationMode = CodeActionValidationMode.None;

            analyzerTest.ExpectedDiagnostics.Add(new DiagnosticResult("ADEF001", Microsoft.CodeAnalysis.DiagnosticSeverity.Warning).WithLocation(18, 27));

            await analyzerTest.RunAsync();
        }

        [TestMethod]
        public async Task DebugDirective_DebugBuild_WithFix()
        {
            var buildConfig = "DEBUG";
            var directiveCheck = "DEBUG";

            string sourceCode = GenerateSourceCode(directiveCheck);
            string resultCode = GenerateSourceCode("DEBUG");
            var analyzerTest = GenerateFixVerifierTest(sourceCode, resultCode, buildConfig);

            await analyzerTest.RunAsync();
        }

        [TestMethod]
        public async Task DebugDirective_ReleaseBuild_WithFix()
        {
            var buildConfig = "RELEASE";
            var directiveCheck = "DEBUG";

            string sourceCode = GenerateSourceCode(directiveCheck);
            string resultCode = GenerateSourceCode("DEBUG");
            var analyzerTest = GenerateAnalyzerTest(sourceCode, buildConfig);

            await analyzerTest.RunAsync();
        }

        private static CSharpAnalyzerTest<DevOnlyMigrateAnalyzer, MSTestVerifier> GenerateAnalyzerTest(string sourceCode, string buildConfig, List<DiagnosticResult> expectedResults = null)
        {
            var packages = GenerateRequirePackages();

            var analyzerTest =  new CSharpAnalyzerTest<DevOnlyMigrateAnalyzer, MSTestVerifier>
            {
                TestState =
                {
                    Sources = { sourceCode },
                    AdditionalFiles =
                    {
                        ("appsettings.json", "{}"),
                    },
                    ReferenceAssemblies = new ReferenceAssemblies("net6.0", new PackageIdentity("Microsoft.NETCore.App.Ref", "6.0.0"), Path.Combine("ref", "net6.0"))
                        .AddPackages(packages)
                }
            };

            analyzerTest.SolutionTransforms.Add((s, p) =>
            {
                return s.WithProjectParseOptions(p, new CSharpParseOptions().WithPreprocessorSymbols(buildConfig));
            });

            return analyzerTest;
        }

        private static CSharpCodeFixTest<DevOnlyMigrateAnalyzer, DevOnlyMigrateCodeFixProvider, MSTestVerifier> GenerateFixVerifierTest(string sourceCode, string fixCode, string buildConfig, List<DiagnosticResult> expectedResults = null)
        {
            var packages = GenerateRequirePackages();

            var analyzerFix = new CSharpCodeFixTest<DevOnlyMigrateAnalyzer, DevOnlyMigrateCodeFixProvider, MSTestVerifier>
            {
                TestState =
                {
                    Sources = { sourceCode },
                    AdditionalFiles =
                    {
                        ("appsettings.json", "{}"),
                    },
                    ReferenceAssemblies = new ReferenceAssemblies("net6.0", new PackageIdentity("Microsoft.NETCore.App.Ref", "6.0.0"), Path.Combine("ref", "net6.0"))
                        .AddPackages(packages)
                },
                FixedState =
                {
                    Sources = { fixCode },
                    AdditionalFiles =
                    {
                        ("appsettings.json", "{}"),
                    },
                    ReferenceAssemblies = new ReferenceAssemblies("net6.0", new PackageIdentity("Microsoft.NETCore.App.Ref", "6.0.0"), Path.Combine("ref", "net6.0"))
                        .AddPackages(packages)
                },
            };

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

        private static string GenerateSourceCode(string directiveCheck)
        {
            var ifDirective = string.IsNullOrEmpty(directiveCheck) ? string.Empty : $"{Environment.NewLine}#if {directiveCheck}";
            var endIfDirective = string.IsNullOrEmpty(directiveCheck) ? string.Empty : $"{Environment.NewLine}#endif";

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
            .ConfigureServices((context, services) => services
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

        //    //    //Diagnostic and CodeFix both triggered and checked for
        //    [TestMethod]
        //    public async Task TestMethod2()
        //    {
        //        var test = @"
        //using System;
        //using System.Collections.Generic;
        //using System.Linq;
        //using System.Text;
        //using System.Threading.Tasks;
        //using System.Diagnostics;

        //namespace ConsoleApplication1
        //{
        //    class {|#0:TypeName|}
        //    {   
        //    }
        //}";

        //        var fixtest = @"
        //using System;
        //using System.Collections.Generic;
        //using System.Linq;
        //using System.Text;
        //using System.Threading.Tasks;
        //using System.Diagnostics;

        //namespace ConsoleApplication1
        //{
        //    class TYPENAME
        //    {   
        //    }
        //}";

        //        var expected = VerifyCS.Diagnostic("AlwaysDevelopingCodeAnalysisEntityFrameworkCore").WithLocation(0).WithArguments("TypeName");
        //        await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
        //    }
    }
}
