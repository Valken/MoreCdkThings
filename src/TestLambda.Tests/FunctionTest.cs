using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.TestUtilities;

namespace TestLambda.Tests;

public class FunctionTest
{
    [Fact]
    public async Task TestToUpperFunction()
    {

        // Invoke the lambda function and confirm the string was upper cased.
        var function = new Function();
        var context = new TestLambdaContext();
        var upperCase = await function.FunctionHandler(new DynamoDBEvent(), context);

        Assert.Equal("HELLO WORLD", upperCase);
    }
}
