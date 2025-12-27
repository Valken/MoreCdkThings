using Amazon.CDK;
using Constructs;

namespace MoreCdkThings;

public class ECSStuff : Stack
{
    public ECSStuff(Construct scope, string id, StackProps? props = null) : base(scope, id, props)
    {
        // Add resources to the stack here
        // ECR Private Registry
_ = new Amazon.CDK.AWS.ECR.Repository(this, "MyEcrRepository", new Amazon.CDK.AWS.ECR.RepositoryProps
{
    RepositoryName = "my-ecr-repo",
    RemovalPolicy = RemovalPolicy.DESTROY,
});
    }
}
