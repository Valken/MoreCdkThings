// See https://aka.ms/new-console-template for more information

using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

Console.WriteLine("Hello, World!");

var token = Environment.GetEnvironmentVariable("TASK_TOKEN");
if (token is null)
{
    Console.WriteLine("No task token provided.");
    return;
}

Console.WriteLine($"Task token: {token}");

var amazonStepFunctions = new AmazonStepFunctionsClient();

await Task.Delay(TimeSpan.FromMinutes(1));
var done = await SendTaskSuccessAsync(token, "{ \"result\": \"success\" }");
Console.WriteLine(done ? "Task sent successfully." : "Task failed to send.");

async Task<bool> SendTaskSuccessAsync(string taskToken, string taskResponse)
{
    var response = await amazonStepFunctions.SendTaskSuccessAsync(new SendTaskSuccessRequest
    { TaskToken = taskToken, Output = taskResponse });

    return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
}