using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Text.RegularExpressions;
using TestFramework.Bindings.Dotnet;
using TestFramework.Core.Common;

namespace TestFramework.Core.Orchestration
{
    internal static partial class DefaultImplementations
    {
        public static IReadOnlyList<string> GetFilePaths(IEnumerable<string> paths, bool recursive)
        {
            const string FILE_SUFFIX = ".test";

            return paths
                .SelectMany(x => Directory.Exists(x)
                    ? Directory.EnumerateFiles(
                        x,
                        $"*{FILE_SUFFIX}",
                        recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                    : File.Exists(x) && x.EndsWith(FILE_SUFFIX, StringComparison.OrdinalIgnoreCase)
                        ? [x]
                        : throw new ArgumentException($"'{x}' is not valid test file or directory"))
                .Distinct()
                .Order()
                .ToList();
        }

        public static async Task<IReadOnlyList<TestFile>> ParseFilesAsync(IEnumerable<string> paths)
        {
            static string ToPlainText(string path, LeafBlock block)
            {
                if (block.Inline == null)
                {
                    throw new TestFileParseException(path, block.Line, block.Column, "Line should not be empty");
                }
                return string.Concat(
                    block.Inline!
                        .Descendants()
                        .OfType<LiteralInline>()
                        .Select(x => x.Content.ToString()));
            }

            List<TestFile> result = [];

            foreach (var path in paths)
            {
                TestFile? file = null;
                Test? test = null;
                var content = await File.ReadAllTextAsync(path);

                foreach (var block in Markdown.Parse(content))
                {
                    if (block is HeadingBlock heading)
                    {
                        if (heading.Level == 1)
                        {
                            if (file != null)
                            {
                                throw new TestFileParseException(path, heading.Line, heading.Column, "Only one file heading is allowed");
                            }

                            file = new TestFile()
                            {
                                Id = GuidGenerator.Get("FILE"),
                                Name = ToPlainText(path, heading),
                                Path = path,
                                Tests = [],
                                Raw = content,
                            };

                            result.Add(file);
                        }
                        else if (heading.Level == 2)
                        {
                            if (file == null)
                            {
                                throw new TestFileParseException(path, heading.Line, heading.Column, "File heading is missing or misplaced");
                            }

                            test = new Test()
                            {
                                Id = GuidGenerator.Get("TEST"),
                                Name = ToPlainText(path, heading),
                                Steps = [],
                                RawSteps = [],
                            };

                            file!.Tests.Add(test);
                        }
                    }
                    else if (block is ListBlock list)
                    {
                        if (test == null)
                        {
                            throw new TestFileParseException(path, block.Line, block.Column, "At least one test must be specified");
                        }

                        List<string> steps = [];

                        foreach (var item in list)
                        {
                            if (item is ListItemBlock listItem)
                            {
                                if (listItem.FirstOrDefault() is not ParagraphBlock paragraph)
                                {
                                    throw new TestFileParseException(path, listItem.Line, listItem.Column, "First list item must be paragraph");
                                }

                                steps.Add(ToPlainText(path, paragraph));
                            }
                        }

                        if (steps.Count == 0)
                        {
                            throw new TestFileParseException(path, block.Line, block.Column, "At least one step must be specified");
                        }

                        test!.RawSteps = steps;
                    }
                }
            }

            return result;
        }

        public static IReadOnlyList<StepDefinition>ParseSteps(IEnumerable<string> steps, IEnumerable<MethodDefinition> definitions)
        {
            static string Normalize(string name)
            {
                return NormalizedStep().Replace(name, "\"\"");
            }

            List<StepDefinition> result = [];

            var normalizedDefinitions = definitions
                .Select(
                    x => new
                    {
                        Definition = x,
                        Normalized = Normalize(x.AttributeValue!),
                    })
                .ToList();

            foreach (var step in steps)
            {
                var normalizedStep = Normalize(step);

                var definitionMatches = normalizedDefinitions
                    .Where(x => x.Normalized == normalizedStep)
                    .ToList();

                if (definitionMatches.Count == 1)
                {
                    var parameters = Parameters().Matches(step)
                        .Select(x => Regex.Unescape(x.Groups[1].Value))
                        .Cast<object>()
                        .ToArray();

                    result.Add(
                        new StepDefinition()
                        {
                            Id = definitionMatches[0].Definition.Id,
                            Name = step,
                            Arguments = parameters,
                        });
                }
                else if (definitionMatches.Count == 0)
                {
                    throw new InvalidOperationException($"Could not match any step definition against '{step}'");
                }
                else
                {
                    throw new InvalidOperationException($"Step definition '{step}' is ambiguous");
                }
            }

            return result;
        }

        [GeneratedRegex("\"((?:\\\\.|[^\"])*)\"")]
        private static partial Regex Parameters();

        [GeneratedRegex("\"[^\"]*\"")]
        private static partial Regex NormalizedStep();
    }
}
