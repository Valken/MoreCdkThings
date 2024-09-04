using System;
using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SQS;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;

namespace MoreCdkThings
{
    public sealed class MoreCdkThingsStack : Stack
    {
        internal MoreCdkThingsStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            // The code that defines your stack goes here
            var queue = new Queue(this, "MyFirstQueue", new QueueProps
            {
                VisibilityTimeout = Duration.Seconds(300),
                Fifo = true,
                ContentBasedDeduplication = true
            });

            var table = new Table(this, "MyFirstTable", new TableProps
            {
                PartitionKey = new Attribute
                {
                    Name = "Id",
                    Type = AttributeType.STRING
                },
                SortKey = new Attribute
                {
                    Name = "SortKey",
                    Type = AttributeType.STRING
                },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                Stream = StreamViewType.NEW_AND_OLD_IMAGES
            });

            // Import a DynamoDB table from a CloudFormation export
            var tableArn = Fn.ImportValue("JobManager-JobTableArn");
            var importedTable = Table.FromTableArn(this, "ImportedTable", tableArn);

            // Define a Lambda function
            var artifactPath = (string)Node.TryGetContext("artifactpath");
            Console.WriteLine($"Artifact path is {artifactPath}");
            var lambdaFunction = new Function(this, "MyFirstLambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Handler = "TestLambda::TestLambda.Function::FunctionHandler",
                Code = Code.FromAsset(string.IsNullOrEmpty(artifactPath)
                    ? "src/TestLambda/bin/Debug/net8.0"
                    : $"{artifactPath}/TestLambda"),
                MemorySize = 256,
                Timeout = Duration.Seconds(30),
                Environment = new Dictionary<string, string>()
                {
                    ["QUEUE_URL"] = queue.QueueUrl
                }
            });
            queue.GrantSendMessages(lambdaFunction);
            lambdaFunction.AddEventSource(new DynamoEventSource(table, new DynamoEventSourceProps
            {
                StartingPosition = StartingPosition.TRIM_HORIZON
            }));

            var thingApi = new ThingApi(this, "ThingApi", table);

            _ = new StateMachine(this, "DoesNothingStateMachine", new StateMachineProps
            {
                DefinitionBody = DefinitionBody.FromChainable(new Pass(this, "Hello", new PassProps
                {
                    Result = new Result(new Dictionary<string, object>
                    {
                        ["Hello"] = "World"
                    })
                }).Next(new Wait(this, "Wait", new WaitProps
                {
                    Time = WaitTime.Duration(Duration.Seconds(10))
                })).Next(new Succeed(this, "Succeed"))
                ),
            });

            var convertToSeconds = new EvaluateExpression(this, "Convert to seconds", new EvaluateExpressionProps
            {
                Expression = "$.waitMilliseconds / 1000",
                ResultPath = "$.waitSeconds"
            }).AddCatch(new Fail(this, "Fail", new FailProps
            {
                //Cause = null,
                // CausePath = null,
                // Comment = null,
                // Error = null,
                // ErrorPath = null,
                // StateName = null
            }));

            var createMessage = new EvaluateExpression(this, "Create message", new EvaluateExpressionProps
            {
                // Note: this is a string inside a string.
                Expression = "`Now waiting ${$.waitSeconds} seconds...`",
                Runtime = Runtime.NODEJS_LATEST,
                ResultPath = "$.message"
            });

            var publishMessage = new SnsPublish(this, "Publish message", new SnsPublishProps
            {
                Topic = new Topic(this, "cool-topic"),
                Message = TaskInput.FromJsonPathAt("$.message"),
                ResultPath = "$.sns"
            });

            var wait = new Wait(this, "WaitFromPath", new WaitProps
            {
                Time = WaitTime.SecondsPath("$.waitSeconds")
            });

            var ddbPut = new DynamoPutItem(this, "PutItem", new DynamoPutItemProps
            {
                Table = table,
                Item = new Dictionary<string, DynamoAttributeValue>
                {
                    ["Id"] = DynamoAttributeValue.FromString("123"),
                    ["SortKey"] = DynamoAttributeValue.FromString("abc"),
                    ["Message"] = DynamoAttributeValue.FromString(JsonPath.StringAt("$.message"))
                },
                ResultPath = "$.ddb"
            });

            var publishSuccess = new Choice(this, "Is this a success?")
                .When(Condition.NumberEquals("$.sns.SdkHttpMetadata.HttpStatusCode", 200), wait)
                .Otherwise(new Fail(this, "FailPublish", new FailProps
                {
                    Cause = "The message was not published",
                    Error = "MessageNotPublished"
                }));

            _ = new StateMachine(this, "AnotherStateMachine", new StateMachineProps
            {
                DefinitionBody = DefinitionBody.FromChainable(
                    convertToSeconds
                        .Next(createMessage)
                        .Next(publishMessage)
                        .Next(ddbPut)
                        .Next(publishSuccess)) // Need to figure out how to model choice states a bit nicer here.
                                               //.Next(wait))
            });
        }
    }
}