using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TestLambda;

public class Function
{
    public string FunctionHandler(DynamoDBEvent dynamoDbEvent, ILambdaContext context)
    {
        foreach(var ev in dynamoDbEvent.Records)
        {
            context.Logger.LogLine($"Event ID: {ev.EventID}");
            context.Logger.LogLine($"Event Name: {ev.EventName}");
            context.Logger.LogInformation(JsonSerializer.Serialize(ev));
        }
        return "Hello World".ToUpper();
    }
}