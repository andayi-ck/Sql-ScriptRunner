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

            // Use the SQL Server 2019 parser (or latest)
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

            // 2. Use a Visitor to ensure ONLY SELECT is used
            var visitor = new SelectOnlyVisitor();
            fragment.Accept(visitor);

            if (visitor.HasForbiddenStatement)
            {
                errors.Add("Only SELECT statements are allowed. DML, DDL, and EXEC are blocked.");
                return (false, errors);
            }

            return (true, errors);
        }
    }

    // The Visitor class that walks the AST and flags bad nodes
    public class SelectOnlyVisitor : TSqlFragmentVisitor
    {
        public bool HasForbiddenStatement { get; private set; }

        // We ONLY allow SelectStatement. Everything else is blocked by default.
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

        // Block any EXEC or Stored Procedure call
        public override void Visit(ExecuteStatement node) { HasForbiddenStatement = true; }

        // Block INSERT, UPDATE, DELETE, MERGE
        public override void Visit(InsertStatement node) { HasForbiddenStatement = true; }
        public override void Visit(UpdateStatement node) { HasForbiddenStatement = true; }
        public override void Visit(DeleteStatement node) { HasForbiddenStatement = true; }
        public override void Visit(MergeStatement node) { HasForbiddenStatement = true; }

        // Block DDL (CREATE, DROP, ALTER, TRUNCATE)
        public override void Visit(CreateTableStatement node) { HasForbiddenStatement = true; }
        public override void Visit(DropTableStatement node) { HasForbiddenStatement = true; }
        public override void Visit(AlterTableStatement node) { HasForbiddenStatement = true; }
        public override void Visit(TruncateTableStatement node) { HasForbiddenStatement = true; }
    }
}