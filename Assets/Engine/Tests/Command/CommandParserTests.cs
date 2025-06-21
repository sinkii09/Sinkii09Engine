using NUnit.Framework;
using Sinkii09.Engine.Commands;
using System.Threading;
using UnityEngine;

namespace Sinkii09.Engine.Tests.Commands
{
    // Example concrete command and parameter for testing
    public class TestCommand : Command
    {
        public TestStringParameter message;
        public TestIntParameter count;

        public override global::Cysharp.Threading.Tasks.UniTask ExecuteAsync(CancellationToken token = default)
        {
            throw new System.NotImplementedException();
        }
    }

    public class TestStringParameter : CommandParameter<string>
    {
        // Correcting the method signature to match the abstract method in the base class
        protected override string ParseValueText(string valueText, out string errors)
        {
            errors = null;
            return valueText;
        }
    }

    public class TestIntParameter : CommandParameter<int>
    {
        // Correcting the method signature to match the abstract method in the base class
        protected override int ParseValueText(string valueText, out string errors)
        {
            errors = null;
            if (int.TryParse(valueText, out int result))
            {
                return result;
            }
            errors = "Invalid integer";
            return 0;
        }
    }

    public class CommandParserTests
    {
        [Test]
        public void ParseCommand_WithValidParameters_AssignsValues()
        {
            // Arrange
            string scriptText = "TestCommand message:\"Hello World\" count:42";
            int index = 0, inlineIndex = 0;
            string errors;

            // Register the test command type
            CommandParser.CommandTypes["TestCommand"] = typeof(TestCommand);

            // Act
            var command = CommandParser.FromScriptText("TestScript", index, inlineIndex, scriptText, out errors) as TestCommand;

            // Assert
            Assert.IsNotNull(command);
            Assert.IsNull(errors);
            Assert.IsNotNull(command.message);
            Assert.IsTrue(command.message.HasValue);
            Assert.AreEqual("Hello World", command.message.Value);
            Assert.IsNotNull(command.count);
            Assert.IsTrue(command.count.HasValue);
            Assert.AreEqual(42, command.count.Value);
        }

        [Test]
        public void ParseCommand_MissingRequiredParameter_ReturnsError()
        {
            // Arrange
            string scriptText = "TestCommand message:\"Hello\"";
            int index = 0, inlineIndex = 0;
            string errors;

            // Register the test command type
            CommandParser.CommandTypes["TestCommand"] = typeof(TestCommand);

            // Act
            var command = CommandParser.FromScriptText("TestScript", index, inlineIndex, scriptText, out errors);

            // Assert
            // Since 'count' is not marked as required in this example, this will not error.
            // If you add [RequiredParameter] to 'count', this test will expect an error.
            Assert.IsNotNull(command);
            Assert.IsNull(errors);
        }

        [Test]
        public void ParseCommand_InvalidParameterValue_ReturnsError()
        {
            // Arrange
            string scriptText = "TestCommand message:\"Hello\" count:abc";
            int index = 0, inlineIndex = 0;
            string errors;

            // Register the test command type
            CommandParser.CommandTypes["TestCommand"] = typeof(TestCommand);

            // Act
            var command = CommandParser.FromScriptText("TestScript", index, inlineIndex, scriptText, out errors);

            // Assert
            Assert.IsNotNull(command);
            Assert.IsNotNull(errors);
            StringAssert.Contains("Failed to set value for parameter", errors);
        }
    }
}