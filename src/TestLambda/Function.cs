using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.SQS;
using Amazon.SQS.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TestLambda;

public class Function
{
    static readonly IAmazonSQS SQSClient = new AmazonSQSClient();
    static readonly string QueueUrl = Environment.GetEnvironmentVariable("QUEUE_URL")!;
    
    public async Task<string> FunctionHandler(DynamoDBEvent dynamoDbEvent, ILambdaContext context)
    {
        foreach(var ev in dynamoDbEvent.Records)
        {
            context.Logger.LogLine($"Event ID: {ev.EventID}");
            context.Logger.LogLine($"Event Name: {ev.EventName}");
            context.Logger.LogInformation(JsonSerializer.Serialize(ev));
            
            context.Logger.LogInformation($"Sending message to SQS...");
            var response = await SQSClient.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = QueueUrl,
                MessageBody = ev.EventID,
                MessageGroupId = "blah"
            });
            
            context.Logger.LogInformation($"Message sent to SQS: {response.MessageId}");
        }

        return "Hello World".ToUpper();
    }
}