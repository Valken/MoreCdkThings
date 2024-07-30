# Welcome to your CDK C# project!

This is a blank project for CDK development with C#.

The `cdk.json` file tells the CDK Toolkit how to execute your app.

It uses the [.NET CLI](https://docs.microsoft.com/dotnet/articles/core/) to compile and execute your project.

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
