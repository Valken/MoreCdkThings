using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Ecr.Assets;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.Events;
using Amazon.CDK.AWS.Events.Targets;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;
using ContainerDefinitionOptions = Amazon.CDK.AWS.ECS.ContainerDefinitionOptions;
using ContainerOverride = Amazon.CDK.AWS.StepFunctions.Tasks.ContainerOverride;
using LogGroupProps = Amazon.CDK.AWS.Logs.LogGroupProps;
using RuleProps = Amazon.CDK.AWS.Events.RuleProps;
using TaskEnvironmentVariable = Amazon.CDK.AWS.StepFunctions.Tasks.TaskEnvironmentVariable;

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

        // var otherVpc = Vpc.FromLookup(this, "OtherVpc", new VpcLookupOptions
        // {
        //     IsDefault = true
        // });

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
                        "10", //JsonPath.StringAt("$.count"),
                        "www.google.com"
                    ]
                }
            ],
            //InputPath = null,
            IntegrationPattern = IntegrationPattern.RUN_JOB,
            ResultPath = JsonPath.DISCARD
        });

        var workerTaskDefinition = new FargateTaskDefinition(this, "WorkerTaskDefinition");
        workerTaskDefinition.AddContainer("TaskWorker", new ContainerDefinitionOptions
        {
            Image = ContainerImage.FromAsset("src/WorkerTask", new AssetImageProps
            {
                Platform = Platform_.LINUX_AMD64,
                IgnoreMode = IgnoreMode.DOCKER
            }),
            Logging = new AwsLogDriver(new AwsLogDriverProps
            {
                StreamPrefix = "TaskWorker",
                LogGroup = new LogGroup(this, "TaskWorkerLogGroup", new LogGroupProps
                {
                    LogGroupName = "TaskWorkerLogs",
                    Retention = RetentionDays.ONE_WEEK,
                    RemovalPolicy = RemovalPolicy.DESTROY
                })
            })
        });

        var workerRunTask = new EcsRunTask(this, "WorkerRunTask", new EcsRunTaskProps
        {
            StateName = "Run a worker task",
            Cluster = cluster,
            LaunchTarget = new EcsFargateLaunchTarget(),
            TaskDefinition = workerTaskDefinition,
            IntegrationPattern = IntegrationPattern.WAIT_FOR_TASK_TOKEN,
            AssignPublicIp = true,
            ContainerOverrides =
            [
                new ContainerOverride
                {
                    ContainerDefinition = workerTaskDefinition.DefaultContainer!,
                    Environment =
                    [
                        new TaskEnvironmentVariable
                        {
                            Name = "TASK_TOKEN",
                            Value = JsonPath.TaskToken
                        }
                    ],
                }
            ],
            ResultPath = "$.WorkerOutput",
        });

        var parallel = new Parallel(this, "ParallelTasks", new ParallelProps
        {
            StateName = "Run tasks in parallel"
        });
        parallel.Branch(ecsRunTask);
        parallel.Branch(workerRunTask);

        var stateMachine = new StateMachine(this, "MyStateMachine", new StateMachineProps
        {
            DefinitionBody = DefinitionBody.FromChainable(parallel),
        });
        stateMachine.GrantTaskResponse(workerTaskDefinition.TaskRole);

        // How about using EventBridge to invoke an ECS Task and be able pass params from event body?
        var rule = new Rule(this, "EcsRunRule", new RuleProps
        {
            EventPattern = new EventPattern
            {
                DetailType = ["EcsTaskThing"],
            }
        });
        rule.AddTarget(new EcsTask(new EcsTaskProps
        {
            Cluster = cluster,
            TaskDefinition = fargateTaskDefinition,
            TaskCount = 1,
            SubnetSelection = new SubnetSelection
            {
                SubnetType = SubnetType.PUBLIC
            },
            AssignPublicIp = true,
            ContainerOverrides =
            [
                new Amazon.CDK.AWS.Events.Targets.ContainerOverride
                {
                    ContainerName = fargateTaskDefinition.DefaultContainer!.ContainerName,
                    Command = ["-c", $"{EventField.FromPath($"$.detail.count")}", EventField.FromPath("$.detail.url")]
                }
            ]
        }));
    }
}