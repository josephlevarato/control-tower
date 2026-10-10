# Control Tower: Saturday briefing

Oct 10, 2026 · @Joseph

## The day at a glance

The order matters: deploying the Calculator first proves the whole pipeline works before you build the last Lambda on top of it.

| # | Part | Time box | Done when |
| --- | --- | --- | --- |
| 1 | Calculator infra | 1–1.5 h | `cdk deploy` succeeds, no top-level `[-]` in the diff |
| 2 | End-to-end test | 45 min | a real POST ends as a `results/` file, and a JFK report ends in the DLQ |
| 3a | StatusLookup decisions | 15 min | the four questions in Part 3 answered in a note |
| 3b | StatusLookup handler and tests | 2–3 h | `dotnet test` green on the whole solution |
| 3c | StatusLookup infra | 45 min | `curl GET /status/{flightId}` returns your calculation |

If a part overruns its time box by more than an hour, stop and move to the next one. Sunday has slack for leftovers.

Commit after each part. Small commits also make the README easier: your history tells the story.

## Part 1: Calculator infra

Everything goes in `infra/lib/infra-stack.ts`, after the Parser block. You already know every piece except the SQS trigger.

- [ ] **Results table.** A second `TableV2`, same keys as the parsed one (`flightId` + `timestamp`, both strings), `RemovalPolicy.DESTROY`. Give it a new construct ID such as `'CalculationResultsTable'`.
- [ ] **The function.** `dotnetFunction(this, 'Calculator', { ... })` with `timeout: cdk.Duration.seconds(10)` and three environment variables, named exactly as your `RequiredEnv` calls: `BUCKET_NAME`, `PARSED_TABLE_NAME`, `RESULTS_TABLE_NAME`.
- [ ] **No `onFailure` here.** On-failure destinations only apply when something invokes a Lambda asynchronously, like S3 does for the Parser. With SQS, the queue retries the message itself, and your DLQ (`maxReceiveCount: 3`) catches what keeps failing. That's already in place.
- [ ] **Four IAM statements**, the same `addToRolePolicy` pattern as the Parser:

| Action | Resource | Why |
| --- | --- | --- |
| `dynamodb:GetItem` | `parsedTable.tableArn` | read the parsed record |
| `s3:GetObject` | `bucket.arnForObjects('attachment/*')` | read the attachment |
| `s3:PutObject` | `bucket.arnForObjects('results/*')` | write the result |
| `dynamodb:PutItem` | `resultsTable.tableArn` | write the index row |

- [ ] **The SQS trigger.** This is the only new concept:

```typescript
import * as eventsources from 'aws-cdk-lib/aws-lambda-event-sources';

calculatorFunction.addEventSource(new eventsources.SqsEventSource(calculationQueue, { batchSize: 1 }));
```

I synthesised this to check it. It also adds one IAM statement by itself: `sqs:ReceiveMessage`, `DeleteMessage`, `ChangeMessageVisibility`, `GetQueueUrl` and `GetQueueAttributes`, scoped to this queue's ARN only. No wildcard, so it's still least privilege. Mention it in the README as the one policy you let CDK write, because the trigger needs exactly those actions.

- [ ] **Two outputs:** `ResultsTableName` and `CalculationDlqUrl`. You'll need both in Part 2. Remember the CalculationQueue name collision: an output ID must not reuse a construct ID.
- [ ] **Check the timeouts.** Function 10 s, queue visibility 60 s. The rule is visibility ≥ 6 × the function timeout, so you're exactly on it. Otherwise SQS could hand the same message to a second instance while the first one is still working.
- [ ] `npx cdk diff`. Expect only `[+]` lines, plus `[~]` on the Lambda code assets (every C# change rebuilds all of them). **A top-level `[-]` means stop and read it.**
- [ ] `npx cdk deploy`, then commit.

## Part 2: end-to-end test

The goal is to watch one report travel through every box, then watch a bad one land in the DLQ. Copy the commands and outputs you get into a scratch file: they become the README's testing section on Sunday.

**Get your stack's values once:**

```bash
aws cloudformation describe-stacks --stack-name ControlTowerStack --query "Stacks[0].Outputs" --output table
```

Then put them in shell variables so the commands below work as written: `export API_URL=...`, `BUCKET_NAME`, `RESULTS_TABLE` and `DLQ_URL`.

**The happy path:**

- [ ] POST a report. The day field must be **today's UTC day or earlier**, because of your month-rollback rule. On 10 October, `100800` means day 10 at 08:00.

```bash
curl -X POST "$API_URL/pos-reports" -H 'Content-Type: text/plain' \
  --data 'POS/UL204.FR RGN/TO BKK/100800/N1642.3E09612.5/450/12500/2800'
```

- [ ] Wait a few seconds, then list the bucket. You should see one object under each prefix: `pos/`, `attachment/`, `results/`.

```bash
aws s3 ls "s3://$BUCKET_NAME" --recursive
```

- [ ] Print the result file (`-` means print to the terminal) and compare it with the spec's example: 43 minutes, 10513 kg.

```bash
aws s3 cp "s3://$BUCKET_NAME/results/<the key you saw>" -
```

- [ ] Check the results table has its row: `aws dynamodb scan --table-name "$RESULTS_TABLE"`

**The low-fuel path:**

- [ ] Same POST with `12500` replaced by `1987`. The result should say `"lowFuelWarning":true` with `estimatedFuelAtArrivalKg` at `0`. That's your unit test, live.

**The failure path:**

- [ ] POST with `TO JFK`. Ingest accepts it (any three letters), the Parser processes it, and the Calculator throws.
- [ ] Wait about 3 minutes: 3 attempts, 60 s of visibility timeout between each. Then:

```bash
aws sqs get-queue-attributes --queue-url "$DLQ_URL" --attribute-names ApproximateNumberOfMessages
```

Expect `1`. That single command proves your whole failure-handling story.

**Reading the logs:** list the log group names, then tail the Calculator's.

```bash
aws logs describe-log-groups --query "logGroups[].logGroupName" --output text
aws logs tail <calculator log group name> --since 15m
```

You should see one success line per good report, and three errors for the JFK one. Reading logs in the AWS console is fine: the rule only forbids *creating or configuring* resources there.

## Part 3: StatusLookup, decide first

The spec only shows the response once the calculation exists, so you have four decisions to make before writing code. My recommendation is in each row, but they're yours: write your answers and one-line reasons in a note, because they go straight into the README.

| # | Question | My recommendation | Why |
| --- | --- | --- | --- |
| 1 | What does each stage return? | results row found: `200` with the calculation and a `status` of `CALCULATED` or `LOW_FUEL_WARNING`. Only a parsed row: `200` with `flightId`, `timestamp`, `status: "PARSED"`. Neither: `404`. | The spec says to mark low fuel as a status. `RECEIVED` only exists in S3, and the spec calls a short `404` normal. |
| 2 | Which table wins? | Check the results table first, the parsed table only if it's empty. | The two timestamps can't be compared: the parsed one is the report's time, the results one is the Calculator's clock. |
| 3 | Where does the response record live? | In the StatusLookup project. | Only StatusLookup produces it, so by your own rule it isn't a contract. It *reads* `CalculationResult`, which already is one, in `Shared`. |
| 4 | What does the 404 body look like? | `{"error":"Flight not found"}` | Same shape as Ingest's errors, so a caller handles one format. |

Decision 2 has a consequence to write down: if a new report is parsed but not yet calculated, the lookup briefly returns the previous calculation. The spec explicitly allows "an earlier state", so say that you saw it and accepted it.

## Part 3: StatusLookup, build it

This Lambda is mostly a remix of the other three. It answers HTTP like Ingest, reads DynamoDB and S3 like the Calculator, and only has one new DynamoDB call: `Query`.

### Setup

- [ ] Create `src/StatusLookup` (classlib) and `tests/StatusLookup.Tests` (xunit) with `-o`, add both to the `.sln`, reference `Shared` from the project and the project from the tests. Delete `Class1.cs` and `UnitTest1.cs`.
- [ ] Packages for the project: `Amazon.Lambda.Core`, `Amazon.Lambda.APIGatewayEvents`, `Amazon.Lambda.Serialization.SystemTextJson`, `AWSSDK.DynamoDBv2`, `AWSSDK.S3`. For the tests: `NSubstitute`, `Amazon.Lambda.TestUtilities`.
- [ ] Add `<GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>` to the csproj.

### The handler

Constructor: `(IAmazonS3, IAmazonDynamoDB, string bucketName, string parsedTableName, string resultsTableName)`, plus the parameterless one with `RequiredEnv`. No clock: this Lambda never creates a timestamp. The signature is the same as Ingest's: `Task<APIGatewayHttpApiV2ProxyResponse> Handler(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)`.

The flow, following decisions 1 and 2:

1. Read `flightId` from the path. Missing or blank: `400`.
2. Query the **results** table for the latest row. Found: `GetObject` its `attachment` path, deserialise a `CalculationResult` (same `using` + `DeserializeAsync` as the Calculator), return `200`.
3. Otherwise query the **parsed** table. Found: return `200` with `status: "PARSED"`.
4. Otherwise `404`.

**Reading the path parameter.** I compiled this one. `PathParameters` can be null, and the obvious `GetValueOrDefault` doesn't compile on its type, so use `TryGetValue`:

```csharp
if (request.PathParameters is null
    || !request.PathParameters.TryGetValue("flightId", out string? flightId)
    || string.IsNullOrWhiteSpace(flightId))
{
    // TODO: 400
}
```

**"Latest row for a flight" is a `Query`, not a `GetItem`.** `GetItem` needs the full key (`flightId` **and** `timestamp`), and you don't know the timestamp. `Query` takes the partition key only and returns rows sorted by the sort key. Two settings turn that into "the latest": `ScanIndexForward = false` (newest first) and `Limit = 1`. Your timestamps are ISO strings with a fixed width, so sorting them as text also sorts them in time. Compiled and checked:

```csharp
private async Task<Dictionary<string, AttributeValue>?> LatestItemAsync(string tableName, string flightId)
{
    QueryResponse response = await _dynamo.QueryAsync(new QueryRequest
    {
        TableName = tableName,
        KeyConditionExpression = "flightId = :flightId",
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":flightId"] = new AttributeValue { S = flightId },
        },
        ScanIndexForward = false,
        Limit = 1,
    });

    return response.Items is { Count: > 0 } ? response.Items[0] : null;
}
```

The last line is a *property pattern*: "`Items` is not null and its `Count` is above 0". In this SDK version, an empty result has `Items` set to **null**, not an empty list. I checked.

**The response record (decision 3).** For `PARSED`, the calculation fields don't exist yet. Make them nullable and hide them when null, per property:

```csharp
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public int? RemainingFlightTimeMinutes { get; init; }
```

(`using System.Text.Json.Serialization;`.) Don't change `PosJson.Options` for this: that would silently change the JSON of every other contract too.

**Building responses.** Copy how Ingest builds its `200` and `400` (status code, JSON body with `PosJson.Options`, `Content-Type` header), and add a `NotFound`. Log one line per lookup: the `flightId` and which status you returned.

### The tests

Same skeleton as the Calculator tests. Helpers:

- `RequestFor(string flightId)` builds an `APIGatewayHttpApiV2ProxyRequest` with `PathParameters = new Dictionary<string, string> { ["flightId"] = flightId }`.
- `GivenLatest(string tableName, string attachmentKey)` scripts the query **for one table only**. This is the new idea: both queries go through the same mock, so `Arg.Is` on the table name decides which answer each one gets. Compiled:

```csharp
private void GivenLatest(string tableName, string attachmentKey) =>
    _dynamo.QueryAsync(Arg.Is<QueryRequest>(r => r.TableName == tableName), Arg.Any<CancellationToken>())
        .Returns(new QueryResponse
        {
            Items = [ new Dictionary<string, AttributeValue> { ["attachment"] = new AttributeValue { S = attachmentKey } } ],
        });
```

- `GivenNothingIn(string tableName)`: same shape, returning `new QueryResponse()`. **You need this one**: see the first trap below.
- `GivenStoredResult(CalculationResult result)`: the Calculator's `GivenAttachment`, with a `CalculationResult`.

Tests, one reason to fail each:

- [ ] **Calculated:** results row + stored result → `200`, and the body is exactly the JSON you expect (write it by hand, as for the Calculator).
- [ ] **Low fuel:** same with `LowFuelWarning = true` → `status` is `LOW_FUEL_WARNING`.
- [ ] **Only parsed:** nothing in results, a parsed row → `200` with `PARSED`, and `GetObjectAsync` was **not** called.
- [ ] **Unknown flight:** nothing in either table → `404`.
- [ ] **Missing path parameter:** `new APIGatewayHttpApiV2ProxyRequest()` → `400`, and DynamoDB was never queried.
- [ ] **Asks for the latest:** a mock can't sort, so the "latest" logic can only be tested by checking you *asked* for it: `Received` a `QueryRequest` with `ScanIndexForward == false && Limit == 1`.

### The infra

- [ ] `dotnetFunction(this, 'StatusLookup', { ... })`, 10 s timeout, the three environment variables.
- [ ] IAM: `dynamodb:Query` on **both** table ARNs (one statement, two resources), and `s3:GetObject` on `results/*` only. It never reads `attachment/` or `pos/`.
- [ ] The route, on the API you already have:

```typescript
api.addRoutes({
    path: '/status/{flightId}',
    methods: [apigw.HttpMethod.GET],
    integration: new HttpLambdaIntegration('StatusLookupIntegration', statusLookupFunction),
});
```

- [ ] `cdk diff`, `cdk deploy`, then `curl "$API_URL/status/<the flightId from Part 2>"`. Your flightId ends with the report's date, so for the Part 2 report it's `UL20420261010RGNBKK`. Also try the JFK flight (`PARSED`, since its calculation never succeeded) and a made-up one (`404`).

## Traps I expect you to hit

When something breaks, check this table before anything else.

| Symptom | Cause | Fix |
| --- | --- | --- |
| `NullReferenceException` in a StatusLookup test | A mocked method nobody scripted returns **null**, not an empty response. I checked: an unscripted `QueryAsync` gives a null `QueryResponse`. | Script every table each test touches: `GivenNothingIn(...)` for the empty one. |
| A test passes but checks nothing | `Received(1)` forgotten, so the line is just a call on the mock. | Every assertion on a mock starts with `Received` or `DidNotReceive`. |
| `Assert.Equal` on JSON fails on spaces or field order | Your expected string isn't exactly what `System.Text.Json` writes. | One line, no spaces, camelCase, properties in declaration order. |
| A number shows up as `16,705` | fr-FR culture on a `ToString()`. | `CultureInfo.InvariantCulture`, or let the serialiser write numbers. |
| `400` from Ingest on a valid-looking curl | The day in the report is later than today's UTC day: your rollback rule tries last month. | Use today's day or earlier. |
| `404` on `/status` right after the POST | Normal: the pipeline takes a few seconds. | Wait, retry. The spec says so too. |
| `cdk diff` shows a top-level `[-]` | A construct ID was renamed, so CloudFormation replaces the resource. | Put the old ID back. A nested `[-]` under `[~]` is just an old value, and fine. |
| `There is already a Construct with name ...` | An output ID reuses a construct ID. | Rename the output, as with the `...Url` ones. |
| Deploy fails on the Lambda with a handler error | The handler string doesn't match namespace, class and method. | The helper builds `Project::Project.Function::Handler`, so the namespace must be `StatusLookup` and the class `Function`. |
| `AccessDenied` in a Lambda's logs | An IAM statement is missing or scoped to the wrong prefix. | Compare the denied action and ARN in the log with your statements. Don't widen to `*`: fix the exact one. |

## For Sunday, and when you're stuck

**Collect these for the README as you go**, one line each, so Sunday is writing rather than remembering:

- [ ] `batchSize: 1`: why, and partial batch responses as the higher-volume alternative.
- [ ] Unknown airport: permanent failure, 3 attempts, then the DLQ. Ingest still answers `RECEIVED` for it.
- [ ] Write order in the Calculator: result file first, then the row. A failure in between leaves an orphan file nobody points to, never a row pointing to nothing. Your `ResultsTableDownTest` proves it.
- [ ] A duplicate SQS delivery creates a second result with a new timestamp. Harmless: StatusLookup takes the latest.
- [ ] The one IAM statement CDK writes for you (the SQS trigger), and why it's still least privilege.
- [ ] Your four StatusLookup decisions.
- [ ] The commands and outputs from Part 2.

**When you're stuck:**

1. Read the error message to the end. Most of this week's bugs were named in the last line.
2. Check the traps table.
3. Write the smallest thing that fails: one test, one curl.
4. Give it 30 minutes, then write down the exact error and the file and line, and move to the next checkbox. Bring the note on Sunday and we'll fix it together in five minutes.

You don't need to finish everything today for Sunday to work. Parts 1 and 2 matter most: they prove the pipeline. StatusLookup can spill over.
