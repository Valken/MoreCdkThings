using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;
using ContainerDefinitionOptions = Amazon.CDK.AWS.ECS.ContainerDefinitionOptions;

namespace MoreCdkThings;

// https://docs.aws.amazon.com/AmazonECS/latest/APIReference/API_RunTask.html
// https://github.com/aws-samples/retryable-ecs-run-task-step-functions/blob/main/lib/construct/retryable-run-task.ts
// https://docs.aws.amazon.com/AmazonECS/latest/developerguide/example_task_definitions.html#example_task_definition-ping
// https://docs.aws.amazon.com/step-functions/latest/dg/connect-ecs.html

// Need to use Task Token to get output from an ECS task, so will build a custom container that handles that next

public class EcsStepFunctionStack : Stack
{
    public EcsStepFunctionStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
    {
        var vpc = new Vpc(this, "MyVpc", new VpcProps
        {
            CreateInternetGateway = true,
            DefaultInstanceTenancy = null,
            EnableDnsHostnames = true,
            EnableDnsSupport = true,
            MaxAzs = 1,
            SubnetConfiguration =
            [
                new SubnetConfiguration
                {
                    CidrMask = 24,
                    Name = "Public",
                    SubnetType = SubnetType.PUBLIC
                }
            ],
            NatGateways = 0,
        });
        var cluster = new Cluster(this, "MyCluster", new ClusterProps
        {
            Vpc = vpc,
            //EnableFargateCapacityProviders = true,
            // DefaultCloudMapNamespace = new CloudMapNamespaceOptions
            // {
            //     Name = "ThingNamespace",
            //     Type = NamespaceType.HTTP
            // }
        });

        var fargateTaskDefinition = new FargateTaskDefinition(this, "SampleTaskDefinition");
        fargateTaskDefinition.AddContainer("MyContainer", new ContainerDefinitionOptions()
        {
            Image = ContainerImage.FromRegistry("alpine:3.4"),
            EntryPoint = ["ping"],
            Command = ["-c", "4", "example.com"],
            MemoryLimitMiB = 512,
            Environment = new Dictionary<string, string>
            {
                ["ENV_VAR"] = "value"
            },
            Logging = new AwsLogDriver(new AwsLogDriverProps
            {
                StreamPrefix = "MyContainer",
                LogGroup = new LogGroup(this, "MyLogGroup", new LogGroupProps
                {
                    LogGroupName = "EcsContainerLogs",
                    Retention = RetentionDays.ONE_WEEK,
                    RemovalPolicy = RemovalPolicy.DESTROY
                })
            })
        });

        // TODO: Figure out how to tell the EcsRunTask to use FARGATE_SPOT

        var ecsRunTask = new EcsRunTask(this, "runTask", new EcsRunTaskProps
        {
            StateName = "Run a ping in Fargate",
            Cluster = cluster,
            LaunchTarget = new EcsFargateLaunchTarget(),
            TaskDefinition = fargateTaskDefinition,
            AssignPublicIp = true,
            ContainerOverrides =
            [
                new ContainerOverride
                {
                    ContainerDefinition = fargateTaskDefinition.DefaultContainer!,
                    Command = 
                    [
                        "-c",
                        "10",//JsonPath.StringAt("$.count"),
                        "www.google.com"
                    ]
                }
            ],
            //InputPath = null,
            IntegrationPattern = IntegrationPattern.RUN_JOB,
        });

        _ = new StateMachine(this, "MyStateMachine", new StateMachineProps
        {
            DefinitionBody = DefinitionBody.FromChainable(ecsRunTask)
        });
    }
}