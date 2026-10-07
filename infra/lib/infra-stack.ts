import * as cdk from 'aws-cdk-lib/core';
import * as s3 from 'aws-cdk-lib/aws-s3';
import { Construct } from 'constructs';

  export class InfraStack extends cdk.Stack {
    constructor(scope: Construct, id: string, props?: cdk.StackProps) {
      super(scope, id, props);

      const bucket = new s3.Bucket(this, 'PosBucket', {
        blockPublicAccess: s3.BlockPublicAccess.BLOCK_ALL,
        enforceSSL: true,
        removalPolicy: cdk.RemovalPolicy.DESTROY, // assessment only: lets `cdk destroy` remove it
        autoDeleteObjects: true,                  // empties it first, or deletion would fail
      });

      new cdk.CfnOutput(this, 'BucketName', { value: bucket.bucketName });
    }
  }
