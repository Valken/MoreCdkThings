using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.SQS;
using Constructs;

namespace MoreCdkThings
{
    public class MoreCdkThingsStack : Stack
    {
        internal MoreCdkThingsStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            // The code that defines your stack goes here
            _ = new Queue(this, "MyFirstQueue", new QueueProps
            {
                VisibilityTimeout = Duration.Seconds(300),
                Fifo = true
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
            var lambdaFunction = new Function(this, "MyFirstLambda", new FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Handler = "TestLambda::TestLambda.Function::FunctionHandler",
                Code = Code.FromAsset("src/TestLambda/bin/Debug/net8.0")
            });
            lambdaFunction.AddEventSource(new DynamoEventSource(table, new DynamoEventSourceProps
            {
                StartingPosition = StartingPosition.TRIM_HORIZON
            }));
        }
    }
}
