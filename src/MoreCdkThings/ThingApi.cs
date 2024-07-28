using System.Collections.Generic;
using Amazon.CDK.AWS.APIGateway;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Constructs;

namespace MoreCdkThings;

public class ThingApi : Construct
{
    public ThingApi(Construct scope, string id, Table table) : base(scope, id)
    {
        var api = new RestApi(scope, $"{id}-rest", new RestApiProps
        {
            RestApiName = "CdkThingApi",
            Description = "This is a CDK thing API"
        });
        var dynamodbIntegration = new AwsIntegration(new AwsIntegrationProps
        {
            Service = "dynamodb",
            IntegrationHttpMethod = "POST",
            Action = "GetItem",
            Options = new IntegrationOptions
            {
                CredentialsRole = new Role(this, "ApiGatewayDynamoDBRole", new RoleProps
                {
                    AssumedBy = new ServicePrincipal("apigateway.amazonaws.com"),
                    ManagedPolicies =
                    [
                        ManagedPolicy.FromAwsManagedPolicyName("AmazonDynamoDBFullAccess")
                    ]
                }),
                RequestTemplates = new Dictionary<string, string>
                {
                    ["application/json"] = $$"""
                                             {
                                                "TableName": "{{table.TableName}}",
                                                    "Key": {
                                                    "Id": {
                                                        "S": "$input.params('id')"
                                                    },
                                                    "SortKey": {
                                                        "S": "$input.params('sortkey')"
                                                    }
                                                }
                                             }
                                             """
                },
                IntegrationResponses =
                [
                    new IntegrationResponse
                    {
                        StatusCode = "200"
                    }
                ]
            }
        });
        var methodOptions = new MethodOptions {MethodResponses = [new MethodResponse {StatusCode = "200"}]};
        api.Root.ResourceForPath("stuff/{id}/{sortkey}").AddMethod("GET", dynamodbIntegration, methodOptions);
    }
}