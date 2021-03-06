namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;
    using Antlr4.Runtime.Tree;

    /// <summary>
    /// A Visitor that walks an expression parse tree and generates string
    /// table entries, which are provided to a <see
    /// cref="StringTableManager"/>. This string table can then be provided
    /// to future compilation passes, or stored for later use. Call the
    /// <see cref="Visit"/> method to begin generating string table
    /// entries.
    /// </summary>
    internal class StringTableGeneratorVisitor : YarnSpinnerParserBaseVisitor<int>
    {

        private YarnSpinnerParser.NodeContext currentNodeContext;
        private string currentNodeName;
        private string fileName;
        private StringTableManager stringTableManager;

        public StringTableGeneratorVisitor(string fileName, StringTableManager stringTableManager)
        {
            this.fileName = fileName;
            this.stringTableManager = stringTableManager;
        }

        public override int VisitNode(YarnSpinnerParser.NodeContext context)
        {
            currentNodeContext = context;

            List<string> tags = new List<string>();

            foreach (var header in context.header())
            {
                string headerKey = header.header_key.Text;
                string headerValue = header.header_value?.Text ?? string.Empty;

                if (headerKey.Equals("title", StringComparison.InvariantCulture))
                {
                    currentNodeName = header.header_value.Text;
                }

                if (headerKey.Equals("tags", StringComparison.InvariantCulture))
                {
                    // Split the list of tags by spaces, and use that
                    tags = new List<string>(headerValue.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                }
            }

            if (string.IsNullOrEmpty(currentNodeName) == false && tags.Contains("rawText"))
            {
                // This is a raw text node. Use its entire contents as a
                // string and don't use its contents.
                var lineID = Compiler.GetLineIDForNodeName(currentNodeName);
                stringTableManager.RegisterString(context.body().GetText(), fileName, currentNodeName, lineID, context.body().Start.Line, null);
            }
            else
            {
                // This is a regular node
                this.Visit(context.body());
            }

            return 0;
        }

        public override int VisitLine_statement([NotNull] YarnSpinnerParser.Line_statementContext context)
        {
            int lineNumber = context.Start.Line;

            string lineID = Compiler.GetLineID(context.hashtag());

            var hashtagText = GetHashtagTexts(context.hashtag());

            GenerateFormattedText(context.line_formatted_text().children, out var composedString, out var expressionCount);

            string stringID = stringTableManager.RegisterString(
                composedString.ToString(),
                fileName,
                currentNodeName,
                lineID,
                lineNumber,
                hashtagText);

            if (lineID == null)
            {
                var hashtag = new YarnSpinnerParser.HashtagContext(context, 0);
                hashtag.text = new CommonToken(YarnSpinnerLexer.HASHTAG_TEXT, stringID);
                context.AddChild(hashtag);
            }

            return 0;
        }

        private string[] GetHashtagTexts(YarnSpinnerParser.HashtagContext[] hashtags)
        {
            // Add hashtag
            var hashtagText = new List<string>();
            foreach (var tag in hashtags)
            {
                hashtagText.Add(tag.HASHTAG_TEXT().GetText());
            }

            return hashtagText.ToArray();
        }

        private void GenerateFormattedText(IList<IParseTree> nodes, out string outputString, out int expressionCount)
        {
            expressionCount = 0;
            StringBuilder composedString = new StringBuilder();

            // First, visit all of the nodes, which are either terminal
            // text nodes or expressions. if they're expressions, we
            // evaluate them, and inject a positional reference into the
            // final string.
            foreach (var child in nodes)
            {
                if (child is ITerminalNode)
                {
                    composedString.Append(child.GetText());
                }
                else if (child is ParserRuleContext)
                {
                    // Expressions in the final string are denoted as the
                    // index of the expression, surrounded by braces { }.
                    // However, we don't need to write the braces here
                    // ourselves, because the text itself that the parser
                    // captured already has them. So, we just need to write
                    // the expression count.
                    composedString.Append(expressionCount);
                    expressionCount += 1;
                }
            }

            outputString = composedString.ToString().Trim();
        }
    }
}
