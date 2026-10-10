import * as path from 'path';
import { execSync } from 'child_process';
import * as cdk from 'aws-cdk-lib/core';
import * as s3 from 'aws-cdk-lib/aws-s3';
import * as lambda from 'aws-cdk-lib/aws-lambda';
import * as logs from 'aws-cdk-lib/aws-logs';
import * as apigw from 'aws-cdk-lib/aws-apigatewayv2';
import * as iam from 'aws-cdk-lib/aws-iam';
import * as dynamodb from 'aws-cdk-lib/aws-dynamodb';
import * as sqs from 'aws-cdk-lib/aws-sqs';
import * as s3n from 'aws-cdk-lib/aws-s3-notifications';
import * as destinations from 'aws-cdk-lib/aws-lambda-destinations';
import * as eventsources from 'aws-cdk-lib/aws-lambda-event-sources';
import { HttpLambdaIntegration } from 'aws-cdk-lib/aws-apigatewayv2-integrations';
import { Construct } from 'constructs';

interface DotnetFunctionProps {
    timeout: cdk.Duration;
    environment: Record<string, string>;
    onFailure?: sqs.IQueue;
}

const repoRoot = path.join(__dirname, '..', '..')

function dotnetCode(project: string): lambda.Code {
    return lambda.Code.fromAsset(repoRoot, {
        exclude: ['infra', '.git', '**/bin', '**/obj'],
        bundling: {
            image: lambda.Runtime.DOTNET_10.bundlingImage,
            command: ['bash', '-c', `dotnet publish src/${project} -c Release -o /asset-output`],
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

function dotnetFunction(scope: Construct, project: string, props: DotnetFunctionProps): lambda.Function {
    return new lambda.Function(scope, `${project.toLowerCase()}Function`, {
        runtime: lambda.Runtime.DOTNET_10,
        architecture: lambda.Architecture.ARM_64,
        handler: `${project}::${project}.Function::Handler`,
        code: dotnetCode(project),
        memorySize: 512,
        timeout: props.timeout,
        environment: props.environment,
        ...(props.onFailure && {
              retryAttempts: 2,
              onFailure: new destinations.SqsDestination(props.onFailure),
        }),
        logGroup: new logs.LogGroup(scope, `${project}Logs`, {
            retention: logs.RetentionDays.ONE_WEEK,
            removalPolicy: cdk.RemovalPolicy.DESTROY,
        })
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

        const ingestFunction = dotnetFunction(this, 'Ingest', {
            timeout: cdk.Duration.seconds(10),
            environment: { BUCKET_NAME: bucket.bucketName },
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

        const parsedTable = new dynamodb.TableV2(this, 'ParsedReportsTable', {
            partitionKey: { name: 'flightId', type: dynamodb.AttributeType.STRING },
            sortKey: { name: 'timestamp', type: dynamodb.AttributeType.STRING },
            removalPolicy: cdk.RemovalPolicy.DESTROY,
        })

        const calculationDlq = new sqs.Queue(this, 'CalculationDlq', {
            retentionPeriod: cdk.Duration.days(14),
            enforceSSL: true,
        })
        const calculationQueue = new sqs.Queue(this, 'CalculationQueue', {
            visibilityTimeout: cdk.Duration.seconds(60),
            deadLetterQueue: { queue: calculationDlq, maxReceiveCount: 3 },
            enforceSSL: true,
        })

        const parserFailureQueue = new sqs.Queue(this, 'ParserFailureQueue', {
            retentionPeriod: cdk.Duration.days(14),
            enforceSSL: true,
        })

        const parserFunction = dotnetFunction(this, 'Parser', {
            timeout: cdk.Duration.seconds(15),
            environment: { TABLE_NAME: parsedTable.tableName, QUEUE_URL: calculationQueue.queueUrl },
            onFailure: parserFailureQueue,
        })

        const resultsTable = new dynamodb.TableV2(this, 'CalculationResultsTable', {
            partitionKey: { name: 'flightId', type: dynamodb.AttributeType.STRING },
            sortKey: { name: 'timestamp', type: dynamodb.AttributeType.STRING },
            removalPolicy: cdk.RemovalPolicy.DESTROY,
        })

        const calculatorFunction = dotnetFunction(this, 'Calculator', {
            timeout: cdk.Duration.seconds(10),
            environment: {
                BUCKET_NAME: bucket.bucketName,
                PARSED_TABLE_NAME: parsedTable.tableName,
                RESULTS_TABLE_NAME: resultsTable.tableName,
            }
        })

        calculatorFunction.addEventSource(
            new eventsources.SqsEventSource(calculationQueue, { batchSize: 1})
        )

        calculatorFunction.addToRolePolicy(new iam.PolicyStatement({
            actions: ['dynamodb:GetItem'],
            resources: [parsedTable.tableArn],
        }))
        calculatorFunction.addToRolePolicy(new iam.PolicyStatement({
            actions: ['s3:GetObject'],
            resources: [bucket.arnForObjects('attachment/*')],
        }))
        calculatorFunction.addToRolePolicy(new iam.PolicyStatement({
            actions: ['s3:PutObject'],
            resources: [bucket.arnForObjects('results/*')],
        }))
        calculatorFunction.addToRolePolicy(new iam.PolicyStatement({
            actions: ['dynamodb:PutItem'],
            resources: [resultsTable.tableArn],
        }))

        parserFunction.addToRolePolicy(new iam.PolicyStatement({
            actions: ['s3:GetObject'],
            resources: [bucket.arnForObjects('pos/*')],
        }))
        parserFunction.addToRolePolicy(new iam.PolicyStatement({
            actions: ['s3:PutObject'],
            resources: [bucket.arnForObjects('attachment/*')],
        }))
        parserFunction.addToRolePolicy(new iam.PolicyStatement({
            actions: ['dynamodb:PutItem'],
            resources: [parsedTable.tableArn],
        }))
        parserFunction.addToRolePolicy(new iam.PolicyStatement({
            actions: ['sqs:SendMessage'],
            resources: [calculationQueue.queueArn],
        }))

        bucket.addEventNotification(
            s3.EventType.OBJECT_CREATED,
            new s3n.LambdaDestination(parserFunction),
            { prefix: 'pos/' },
        )

        new cdk.CfnOutput(this, 'BucketName', { value: bucket.bucketName })
        new cdk.CfnOutput(this, 'ApiUrl', { value: api.apiEndpoint })
        new cdk.CfnOutput(this, 'TableName', { value: parsedTable.tableName })
        new cdk.CfnOutput(this, 'CalculationQueueUrl', { value: calculationQueue.queueUrl })
        new cdk.CfnOutput(this, 'ParserFailureQueueUrl', { value: parserFailureQueue.queueUrl })
        new cdk.CfnOutput(this, 'ResultsTableName', { value: resultsTable.tableName })
        new cdk.CfnOutput(this, 'CalculationDlqUrl', { value: calculationDlq.queueUrl })
    }
}
