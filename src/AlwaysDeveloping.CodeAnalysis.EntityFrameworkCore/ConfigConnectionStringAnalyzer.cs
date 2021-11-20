using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace AlwaysDeveloping.CodeAnalysis.EntityFrameworkCore
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ConfigConnectionStringAnalyzer : DiagnosticAnalyzer
    {
        public const string Diagnostic002Id = "ADEF002";
        public const string Diagnostic003Id = "ADEF003";

        private static readonly string Title002 = "Appsettings.json file cannot be analyzed";
        private static readonly string MessageFormat002 = "Change the 'Build Action' of appsettings.config to 'C# analyzer additional file'";
        private static readonly string Description002 = "Change the 'Build Action' of appsettings.config to 'C# analyzer additional file'";

        private static readonly string Title003 = "Appsettings.json does not contain database connection string";
        private static readonly string MessageFormat003 = "The appsettings.json file does not contain a database connection string for connection '{0}'";
        private static readonly string Description003 = "The appsettings.json file does not contain a database connection string";

        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor rule002 = new DiagnosticDescriptor(Diagnostic002Id, Title002, MessageFormat002, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description002);
        private static readonly DiagnosticDescriptor rule003 = new DiagnosticDescriptor(Diagnostic003Id, Title003, MessageFormat003, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description003);


        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(rule002, rule003); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeExpression, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeExpression(SyntaxNodeAnalysisContext context)
        {
            var invocationExpr = (InvocationExpressionSyntax)context.Node;

            // invocationExpr.Expression is the expression before "("
            var memberAccessExpr = invocationExpr.Expression as MemberAccessExpressionSyntax;
            if (memberAccessExpr == null)
                return;

            // check if its the specific method we want to analyze
            if (memberAccessExpr.Name.ToString() == "GetConnectionString")
            {
                AnalyzeAddSqlAppConfig(memberAccessExpr, context, invocationExpr.ArgumentList);
            }
        }

        private static void AnalyzeAddSqlAppConfig(MemberAccessExpressionSyntax memberExpression, SyntaxNodeAnalysisContext context, ArgumentListSyntax arguments)
        {
            // if there is no file to query, then do nothing
            if (context.Options.AdditionalFiles == null || !context.Options.AdditionalFiles.Any(x => x.Path.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase)))
            {
                var diagnostic002 = Diagnostic.Create(rule002, Location.None);
                context.ReportDiagnostic(diagnostic002);

                return;
            }
            
            // get the connection string name
            var connectionStringName = (arguments.Arguments.First().Expression as LiteralExpressionSyntax)?.Token.ValueText;

            //if we reach here, it means there was an AddSql<> call
            var appSettingsFile = context.Options.AdditionalFiles.First(x => x.Path.EndsWith("appsettings.json", StringComparison.OrdinalIgnoreCase));
            var appSettingsJson = appSettingsFile.GetText(context.CancellationToken).ToString();

            // if the file doesn't contain the node or the connection string, report diagnostic
            if(!appSettingsJson.Contains($"{"\""}{connectionStringName}{"\""}") || !appSettingsJson.Contains("ConnectionStrings"))
            {
                var diagnostic003 = Diagnostic.Create(rule003, memberExpression.GetLocation(), connectionStringName);
                context.ReportDiagnostic(diagnostic003);
            }
        }
    }
}
