using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AlwaysDeveloping.CodeAnalysis.EntityFrameworkCore
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConfigConnectionStringCodeFixProvider)), Shared]
    public class ConfigConnectionStringCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(ConfigConnectionStringAnalyzer.Diagnostic003Id); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    equivalenceKey: ConfigConnectionStringAnalyzer.Diagnostic003Id,
                    title: "Generate configuration settings",
                    createChangedDocument: c => GenerateConfigSettings(context.Document, declaration, c)),
                diagnostic);
        }

        private async Task<Document> GenerateConfigSettings(Document document, InvocationExpressionSyntax invocationExpr, CancellationToken cancellationToken)
        {
            var originalRoot = await document.GetSyntaxRootAsync(cancellationToken);

            // invocationExpr.Expression is the expression before "("
            var memberAccessExpr = invocationExpr.Expression as MemberAccessExpressionSyntax;

            if (memberAccessExpr == null)
                return await Task.FromResult(document);

            // check if its the specific method we want to analyze
            if (memberAccessExpr.Name.ToString() != "GetConnectionString")
            {
                return await Task.FromResult(document);
            }

            var arguments = invocationExpr.ArgumentList;

            // get the root MemberAccessExpressionSyntax for the memberAccessExpr
            var rootMemberAccessList = originalRoot
                .DescendantNodes()
                .Where(gen => gen.IsKind(SyntaxKind.SimpleMemberAccessExpression))
                .Where(gen => (gen as MemberAccessExpressionSyntax).Name.ToString().StartsWith("AddDbContext"))
                .Where(gen => gen.GetLocation().SourceSpan.End < memberAccessExpr.GetLocation().SourceSpan.Start)
                .OrderBy(gen => gen.GetLocation().SourceSpan.End)
                .ToList();

            if(!rootMemberAccessList.Any())
            {
                return await Task.FromResult(document);
            }

            var rootMemberAccess = (MemberAccessExpressionSyntax)rootMemberAccessList.First();

            // add leading trivia to the rootMemberAccess
            var newMemberAccess = rootMemberAccess.OperatorToken.WithLeadingTrivia(GetAppSettingComment(rootMemberAccess, arguments.Arguments.First().ToString()));
            // replace nodes
            var newCommentRoot = originalRoot.ReplaceToken(rootMemberAccess.OperatorToken, newMemberAccess);
            var newDocument = document.WithSyntaxRoot(newCommentRoot);

            return await Task.FromResult(newDocument);
        }

        private SyntaxTriviaList GetAppSettingComment(MemberAccessExpressionSyntax memberAccessExpr, string connectionName)
        {
            var commentTrivia = SyntaxFactory.TriviaList();
            var whitespaceTrivia = SyntaxFactory.Whitespace(String.Empty);

            // work out what the whitespace before the memberAccessExpr
            if (memberAccessExpr.OperatorToken.HasLeadingTrivia)
            {
                foreach (var trivia in memberAccessExpr.OperatorToken.LeadingTrivia)
                {
                    commentTrivia = commentTrivia.Add(trivia);

                    if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                    {
                        whitespaceTrivia = trivia;
                    }
                }
            }

            // Build up a string trivia
            commentTrivia = commentTrivia.Add(SyntaxFactory.Comment("/* Ensure the below JSON snippet exists in appsettings.json."));
            commentTrivia = commentTrivia.Add(SyntaxFactory.CarriageReturnLineFeed);

            var connStringBuilder = new StringBuilder();
            connStringBuilder.AppendLine($"{whitespaceTrivia}{{");
            connStringBuilder.AppendLine($"{whitespaceTrivia}{whitespaceTrivia}\"ConnectionStrings\": {{");
            connStringBuilder.AppendLine($"{whitespaceTrivia}{whitespaceTrivia}{whitespaceTrivia}{connectionName}: \"Data Source=LocalDatabase.db\"");
            connStringBuilder.AppendLine($"{whitespaceTrivia}{whitespaceTrivia}}}");
            connStringBuilder.AppendLine($"{whitespaceTrivia}}}");

            commentTrivia = commentTrivia.Add(SyntaxFactory.Comment(connStringBuilder.ToString()));

            commentTrivia = commentTrivia.Add(SyntaxFactory.Comment($"{whitespaceTrivia}*/"));
            commentTrivia = commentTrivia.Add(SyntaxFactory.CarriageReturnLineFeed);
            commentTrivia = commentTrivia.Add(whitespaceTrivia);

            return commentTrivia;
        }
    }
}
