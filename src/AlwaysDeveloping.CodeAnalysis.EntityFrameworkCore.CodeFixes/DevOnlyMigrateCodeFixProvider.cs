using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AlwaysDeveloping.CodeAnalysis.EntityFrameworkCore
{
    [ExportCodeFixProvider(LanguageNames.CSharp, 
        Name = nameof(DevOnlyMigrateCodeFixProvider)), Shared]
    public class DevOnlyMigrateCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(DevOnlyMigrateAnalyzer.DiagnosticId); }
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
                    equivalenceKey: DevOnlyMigrateAnalyzer.DiagnosticId,
                    title: "Surround with correct #if directive",
                    createChangedDocument: c => InsertIfDirectiveAsync(context.Document, declaration, c)),
                diagnostic);
        }

        private async Task<Document> InsertIfDirectiveAsync(Document document, InvocationExpressionSyntax invocationExpr, CancellationToken cancellationToken)
        {
            var memberAccessExpr = invocationExpr.Expression as MemberAccessExpressionSyntax;
            var originalRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var statement = GetStatement(invocationExpr);

            // get the closest If directive
            var closestIfDirective = CodeAnalysisHelper.GetClosestIfDirective(memberAccessExpr, originalRoot);

            // if there was one
            if (closestIfDirective != null)
            {
                // work out the replacemeent directive and replace 
                var replacementIfDirective = SyntaxFactory.IfDirectiveTrivia(SyntaxFactory.ParseExpression($" DEBUG{Environment.NewLine}"), true, true, true);
                var replacementIfDirectiveList = SyntaxFactory.TriviaList(new SyntaxTrivia[]
                {
                    SyntaxFactory.Trivia(replacementIfDirective)
                }) ;

                var ifDirectiveNode = statement.FindNode(closestIfDirective.Value.Span);

                if(ifDirectiveNode != null && ifDirectiveNode.HasLeadingTrivia)
                {
                    var newIfDirectiveNode = ifDirectiveNode.WithLeadingTrivia(replacementIfDirectiveList);
                    var newReplacementStatement = statement.ReplaceNode(ifDirectiveNode, newIfDirectiveNode);
                    var newReplacementRoot = originalRoot.ReplaceNode(statement, newReplacementStatement);

                    return document.WithSyntaxRoot(newReplacementRoot);
                }

                return document;
            }

            // this branch is if there is no directive
            var statementWithDirective = InsertNewIfDirective(statement);
            var newRootWithEndDirective = originalRoot.ReplaceNode(statement, statementWithDirective);

            return document.WithSyntaxRoot(newRootWithEndDirective);

        }

        private SyntaxNode InsertNewIfDirective(SyntaxNode currentNode)
        {
            // declare the IF directive
            var newIfDirective = SyntaxFactory.IfDirectiveTrivia(SyntaxFactory.ParseExpression($" DEBUG"), true, true, true);
            var newStartTriviaList = SyntaxFactory.TriviaList(new SyntaxTrivia[]
                {
                    SyntaxFactory.Trivia(newIfDirective),
                    SyntaxFactory.CarriageReturnLineFeed
                }
            );

            // if there was any existing trivia, include it as well
            if (currentNode.HasLeadingTrivia)
            {
                foreach (var trivia in currentNode.GetLeadingTrivia())
                {
                    newStartTriviaList = newStartTriviaList.Add(trivia);
                }
            }

            var newCurrentNode = currentNode.WithLeadingTrivia(newStartTriviaList);

            var newEndDirective = SyntaxFactory.EndIfDirectiveTrivia(true);
            var newEndTriviaList = SyntaxFactory.TriviaList(new SyntaxTrivia[]
                {
                    SyntaxFactory.CarriageReturnLineFeed,
                    SyntaxFactory.Trivia(newEndDirective),
                    SyntaxFactory.CarriageReturnLineFeed,
                }
            );

            var newParentWithTrailingDirective = newCurrentNode.WithTrailingTrivia(newEndTriviaList);

            return newParentWithTrailingDirective;
        }

        private Document RenameIfDirectiveAsync(Document document, MemberAccessExpressionSyntax memberAccessExpr, SyntaxNode oldRoot)
        {
            // if it does contain directive make sure its an IF directive
            var existingIfDirective = memberAccessExpr.GetFirstDirective() as IfDirectiveTriviaSyntax;
            var replacementIfDirective = SyntaxFactory.IfDirectiveTrivia(SyntaxFactory.ParseExpression($" DEBUG{Environment.NewLine}"), true, true, true);

            // replace the directive
            var newMemberAccessExpr = memberAccessExpr.ReplaceNode(existingIfDirective, replacementIfDirective);

            // replace the node one level higher
            var newReplacementRoot = oldRoot.ReplaceNode(memberAccessExpr, newMemberAccessExpr);

            return document.WithSyntaxRoot(newReplacementRoot);
        }

        private SyntaxNode GetStatement(SyntaxNode currentNode)
        {
            if (currentNode is GlobalStatementSyntax || currentNode is ExpressionStatementSyntax || currentNode.Parent == null)
                return currentNode;

            return GetStatement(currentNode.Parent);
        }
    }
}
