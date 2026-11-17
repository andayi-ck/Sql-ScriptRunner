using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace KabarakScriptRunner.Core.Services
{
    public static class ScriptValidator
    {
        public static (bool IsValid, List<string> Errors) Validate(string sqlText)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(sqlText))
            {
                errors.Add("The script is empty.");
                return (false, errors);
            }

            // Use the SQL Server 2019 parser
            var parser = new TSql150Parser(initialQuotedIdentifiers: true);
            using var reader = new StringReader(sqlText);

            var fragment = parser.Parse(reader, out IList<ParseError> parseErrors);

            // 1. Check for syntax errors
            if (parseErrors.Any())
            {
                foreach (var err in parseErrors)
                {
                    errors.Add($"Syntax Error (Line {err.Line}, Col {err.Column}): {err.Message}");
                }
                return (false, errors);
            }

            // 2. Allow-list check: We only accept a TSqlScript that is purely a SELECT
            if (fragment is not TSqlScript script)
            {
                errors.Add("Could not parse the SQL script.");
                return (false, errors);
            }

            // A script can contain multiple batches. We check every batch.
            foreach (var batch in script.Batches)
            {
                // A batch must contain exactly one statement
                if (batch.Statements.Count != 1)
                {
                    errors.Add("Only a single SELECT statement is allowed.");
                    return (false, errors);
                }

                var statement = batch.Statements[0];

                // Check if the statement is a SelectStatement
                if (statement is not SelectStatement)
                {
                    errors.Add("Only SELECT statements are allowed. DML, DDL, and EXEC are blocked.");
                    return (false, errors);
                }

                // Even if it's a SelectStatement, we check for dangerous sub-features
                var visitor = new SelectOnlyVisitor();
                statement.Accept(visitor);

                if (visitor.HasForbiddenStatement)
                {
                    errors.Add("The query contains forbidden operations (SELECT INTO, OPENROWSET, OPENQUERY, EXEC).");
                    return (false, errors);
                }
            }

            return (true, errors);
        }
    }

    // The Visitor class that walks the AST and flags bad nodes inside a SELECT
    public class SelectOnlyVisitor : TSqlFragmentVisitor
    {
        public bool HasForbiddenStatement { get; private set; }

        public override void Visit(SelectStatement node)
        {
            // Block SELECT INTO (which creates a table)
            if (node.Into != null)
            {
                HasForbiddenStatement = true;
            }

            base.Visit(node);
        }

        // Block OPENROWSET and OPENQUERY (which can connect to external servers)
        public override void Visit(OpenRowsetTableReference node) { HasForbiddenStatement = true; }
        public override void Visit(OpenQueryTableReference node) { HasForbiddenStatement = true; }

        // Block any EXEC or Stored Procedure call (in case someone sneaks it in)
        public override void Visit(ExecuteStatement node) { HasForbiddenStatement = true; }
    }
}