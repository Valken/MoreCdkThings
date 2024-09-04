using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.AutoScaling;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ServiceDiscovery;
using Amazon.CDK.AWS.StepFunctions;
using Amazon.CDK.AWS.StepFunctions.Tasks;
using Constructs;
using ContainerDefinition = Amazon.CDK.AWS.ECS.ContainerDefinition;
using ContainerDefinitionOptions = Amazon.CDK.AWS.ECS.ContainerDefinitionOptions;

namespace MoreCdkThings;

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
                StreamPrefix = "MyContainer"
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
                        "10",
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