import * as path from 'path';
import { execSync } from 'child_process';
import * as cdk from 'aws-cdk-lib/core';
import * as s3 from 'aws-cdk-lib/aws-s3';
import * as lambda from 'aws-cdk-lib/aws-lambda';
import * as logs from 'aws-cdk-lib/aws-logs';
import * as apigw from 'aws-cdk-lib/aws-apigatewayv2';
import * as iam from 'aws-cdk-lib/aws-iam';
import { HttpLambdaIntegration } from 'aws-cdk-lib/aws-apigatewayv2-integrations';
import { Construct } from 'constructs';

const repoRoot = path.join(__dirname, '..', '..')

function dotnetCode(project: string): lambda.Code {
    return lambda.Code.fromAsset(repoRoot, {
        exclude: ['infra', '.git', '**/bin', '**/obj'],
        bundling: {
            image: lambda.Runtime.DOTNET_10.bundlingImage,
            local: {
                tryBundle(outputDir: string) {
                    execSync(`dotnet publish src/${project} -c Release -o "${outputDir}"`, {
                        cwd: repoRoot,
                        stdio: 'inherit',
                    })
                    return true;
                }
            }
        }
    })
}

export class InfraStack extends cdk.Stack {
    constructor(scope: Construct, id: string, props?: cdk.StackProps) {
        super(scope, id, props)

        const bucket = new s3.Bucket(this, 'posBucket', {
            blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
            enforceSSL: true,
            removalPolicy: cdk.RemovalPolicy.DESTROY,
            autoDeleteObjects: true,
        })

        const ingestFunction = new lambda.Function(this, 'ingestFunction', {
            runtime: lambda.Runtime.DOTNET_10,
            architecture: lambda.Architecture.ARM_64,
            handler: 'Ingest::Ingest.Function::Handler',
            code: dotnetCode('Ingest'),
            memorySize: 512,
            timeout: cdk.Duration.seconds(10),
            environment: { BUCKET_NAME: bucket.bucketName },
            logGroup: new logs.LogGroup(this, 'IngestLogs', {
                retention: logs.RetentionDays.ONE_WEEK,
                removalPolicy: cdk.RemovalPolicy.DESTROY,
            }),
        })

        ingestFunction.addToRolePolicy(new iam.PolicyStatement({
            actions: ['s3:PutObject'],
            resources: [bucket.arnForObjects('pos/*')],
        }));

        const api = new apigw.HttpApi(this, 'PosApi')
        api.addRoutes({
            path: '/pos-reports',
            methods: [apigw.HttpMethod.POST],
            integration: new HttpLambdaIntegration('IngestIntegration', ingestFunction),
        })

        new cdk.CfnOutput(this, 'BucketName', { value: bucket.bucketName })
        new cdk.CfnOutput(this, 'ApiUrl', { value: api.apiEndpoint })
    }
}
