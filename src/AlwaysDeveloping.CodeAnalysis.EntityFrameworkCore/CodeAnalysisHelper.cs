using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace AlwaysDeveloping.CodeAnalysis.EntityFrameworkCore
{
    /// <summary>
    /// A code analysis helper class
    /// </summary>
    public static class CodeAnalysisHelper
    {
        /// <summary>
        /// Checks if the directive is valid based on criteria
        /// </summary>
        /// <param name="ifDirective">The if directive to check</param>
        /// <returns>A flag</returns>
        public static bool IsValidIfDirective(SyntaxTrivia? ifDirective)
        {
            if(ifDirective == null)
            {
                return false;
            }

            var directiveString = ifDirective.ToString().ToUpper();
            if ((directiveString.Contains("DEBUG") && !directiveString.Contains("!DEBUG")) ||
                directiveString.Contains("!RELEASE"))
            {
                return true;
            }

            ifDirective = null;
            return false;
        }

        /// <summary>
        /// Get the closest if directive to the memberAccessExpr
        /// </summary>
        /// <param name="memberAccessExpr">The expression</param>
        /// <param name="rootNode">The root node</param>
        /// <returns>The closest If directive</returns>
        public static SyntaxTrivia? GetClosestIfDirective(MemberAccessExpressionSyntax memberAccessExpr, SyntaxNode rootNode)
        {
            // check if there is an #If statement before the method in question
            var closestIfDirectiveList = rootNode
                .DescendantTrivia()
                .Where(dir => dir.IsKind(SyntaxKind.IfDirectiveTrivia))
                .Where(dir => dir.GetLocation().SourceSpan.End < memberAccessExpr.GetLocation().SourceSpan.Start)
                .OrderBy(dir => dir.GetLocation().SourceSpan.End)
                .ToList();

            if (!closestIfDirectiveList.Any())
            {
                return null;
            }

            var closestIfDirective = closestIfDirectiveList.First();

            // check if there is an #EndIf statement between the #if and method in question
            var closestEndIfDirective = rootNode
                .DescendantTrivia()
                .Where(dir => dir.IsKind(SyntaxKind.EndIfDirectiveTrivia))
                .Where(dir => dir.GetLocation().SourceSpan.End < memberAccessExpr.GetLocation().SourceSpan.Start)
                .Where(dir => dir.GetLocation().SourceSpan.End > closestIfDirective.Span.End)
                .OrderBy(dir => dir.GetLocation().SourceSpan.End)
                .ToList();

            if (closestEndIfDirective.Any())
            {
                return null;
            }

            return closestIfDirective;
        }
    }
}
