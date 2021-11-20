using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace AlwaysDeveloping.CodeAnalysis.EntityFrameworkCore
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DevOnlyMigrateAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ADEF001";

        private static readonly string Title = "Release build auto-migration";
        private static readonly string MessageFormat = "It is recommended to only run auto-migrations in development environments";
        private static readonly string Description = "It is recommended to only run auto-migrations in development environments";
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor rule001 = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(rule001); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeExpression, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeExpression(SyntaxNodeAnalysisContext context)
        {
            // We know the node is of type InvocationExpressionSyntax as the callback
            // registration was only for SyntaxKind..InvocationExpression
            var invocationExpr = (InvocationExpressionSyntax)context.Node;

            // nvocationExpr.Expression is the method name, the expression before "(". 
            // In our case Database.Migration
            var memberAccessExpr = invocationExpr.Expression as MemberAccessExpressionSyntax;
            if (memberAccessExpr == null)
                return;

            // Get the expression. In our case, Database
            var bindingExpression = memberAccessExpr.Expression as MemberBindingExpressionSyntax;
            if (bindingExpression == null)
                return;

            // Get the memberAccessExpr name of the expression.
            // In our case, Migration
            var expressionName = bindingExpression.Name as IdentifierNameSyntax;
            if (expressionName == null)
                return;

            // If we reach this far, make sure its the Database property
            if (expressionName.Identifier.ToString().ToLower() != "Database".ToLower())
                return;

            // Get the memberAccessExpr name of the expression.
            // In our case, Migration
            var identifierName = memberAccessExpr.Name as IdentifierNameSyntax;
            if (identifierName == null)
                return;

            // check if its the specific method we want to analyze
            if (identifierName.Identifier.ToString().ToLower() == "Migrate".ToLower())
            {
                var closestIfDirective = CodeAnalysisHelper.GetClosestIfDirective(memberAccessExpr, context.SemanticModel.SyntaxTree.GetRoot());
                if (closestIfDirective != null)
                {
                    if(CodeAnalysisHelper.IsValidIfDirective(closestIfDirective))
                    {
                        return;
                    }
                }

                // report the error if we found the method and it didn't have the directives expected
                var diagnostic001 = Diagnostic.Create(rule001, identifierName.GetLocation());
                context.ReportDiagnostic(diagnostic001);
            }
        }

    }
}
