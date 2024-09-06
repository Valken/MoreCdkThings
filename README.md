# Welcome to my CDK and AWS C# messing about project!

This is a project I'm using to test out CDK and assorted AWS services using C#.

The `cdk.json` file tells the CDK Toolkit how to execute your app.

## EcsStepFunctionStack 

This creates a Step Function that invokes Fargate Tasks, one of which uses TaskToken to callback to current step function invocation.

The project for the ECS Task is `WorkerTask`

It uses the [.NET CLI](https://docs.microsoft.com/dotnet/articles/core/) to compile and execute your project.

## MoreCdkThingsStack

This one creates a DynamoDB Table and a SQS Fifo queue. A lambda handles DDB stream events and sends a message to the SQS queue.

There's also an API construct in there that integrates directly with DynamoDB and uses VTL to transform the output

## Useful commands

* `dotnet build src` compile this app
* `cdk deploy`       deploy this stack to your default AWS account/region
* `cdk diff`         compare deployed stack with current state
* `cdk synth`        emits the synthesized CloudFormation template

## Some additional commands

Compile and publish the lambda function and CDK app
```bash
dotnet publish src/TestLambda \
    -c Release --framework "net8.0" \
    /p:GenerateRuntimeConfigurationFiles=true \
    --runtime linux-x64 \
    --self-contained False -o publish/TestLambda
    
 dotnet publish src/MoreCdkThings \
    -c Release \
    -o publish/CDK
``` 

Deploy the CDK app from published directory
```bash
cdk deploy \
  --app 'dotnet publish/CDK/MoreCdkThings.dll' \
  --context artifactpath='./publish/' 
```
