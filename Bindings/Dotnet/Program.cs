using System;
using System.Collections.Generic;

namespace TestFramework.Bindings.Dotnet
{
    public class TestFile()
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public string? Path { get; set; }
        public required IList<Test> Tests { get; set; }
        public string? Raw { get; set; }
        public int TestPosition { get; set; } = 0;
        public bool IsFailing { get; set; } = false;
    }

    public class Test()
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public required IList<StepDefinition> Steps { get; set; }
        public required IList<string> RawSteps { get; set; }
        public int StepPosition { get; set; } = 0;
    }

    public class MethodDefinition()
    {
        public required string Id { get; set; }
        public string? AttributeValue { get; set; }
    }

    public class StepDefinition()
    {
        public required string Id { get; set; }
        public required string Name { get; set; }
        public object[]? Arguments { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class GetFilePaths : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ParseFiles : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ParseSteps : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class Step : Attribute
    {
        public Step(string definition)
        {
            Definition = definition;
        }

        public string Definition { get; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class BeforeRun : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class AfterRun : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class BeforeFile : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class AfterFile : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class BeforeTest : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class AfterTest : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class BeforeStep : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class AfterStep : Attribute
    {
    }
}
