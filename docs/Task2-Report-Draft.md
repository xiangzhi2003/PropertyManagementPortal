# CT071-3-3-DDAC — Task #2 Report Draft

Structured to match the Word document headings.
`[SCREENSHOT]` marks where an image goes. `[TEAM]` marks what someone else must supply.

---

# Architecture Blueprint

## Initial Architecture Diagram

**Task #1 — server-based architecture.** No S3, no Lambda, no API Gateway.

```
        ┌──────────┐
        │  Browser │
        └────┬─────┘
             │ HTTPS (photo uploaded through the server)
             ▼
   ┌──────────────────────────┐
   │   Elastic Beanstalk      │
   │   ASP.NET Core MVC 8     │
   │   (EC2 instance)         │
   │                          │
   │   ┌──────────────────┐   │
   │   │ wwwroot/uploads  │   │  ← photos written to the
   │   │ (local disk)     │   │     instance's own disk
   │   └──────────────────┘   │
   └────────────┬─────────────┘
                │ EF Core (Npgsql)
                ▼
        ┌───────────────┐
        │   AWS RDS     │
        │  PostgreSQL   │
        └───────────────┘
```

**Points to make about this design:**

- Everything runs in one process on one EC2 instance — page rendering, business logic,
  database access, and file storage.
- Repair-evidence photos were saved to the instance's **local filesystem** under
  `wwwroot/uploads`, with only the filename recorded in the database.
- Three weaknesses follow from that, and they motivate the whole of Task #2:
  1. **Not durable** — Elastic Beanstalk replaces instances on deploy, scale, or health
     failure. Every uploaded photo is lost with the instance.
  2. **Not scalable** — with more than one instance behind the load balancer, a photo
     written to instance A is invisible to instance B.
  3. **Not decoupled** — upload traffic consumes the same CPU, memory, and disk as
     ordinary page requests.

## Current Architecture Diagram

**Task #2 — hybrid architecture.** S3 replaces local disk, and the upload is authorised
by a serverless function instead of by the web server.

```
        ┌──────────┐
        │  Browser │
        └──┬──┬──┬─┘
           │  │  │
  ① "give  │  │  └── ③ form post (object key only — no file)
     me a  │  │                      │
     link" │  │ ② PUT file           ▼
           │  │  (direct)  ┌──────────────────────────┐
           ▼  │            │   Elastic Beanstalk      │
  ┌──────────────────┐     │   ASP.NET Core MVC 8     │
  │   API Gateway    │     └────────────┬─────────────┘
  │   (prod stage)   │                  │ EF Core
  └────────┬─────────┘                  ▼
           │ proxy integration   ┌───────────────┐
           ▼                     │   AWS RDS     │
  ┌──────────────────┐           │  PostgreSQL   │
  │   AWS Lambda     │           └───────────────┘
  │  presigned-URL   │
  │    generator     │
  └────────┬─────────┘
           │ signs the request
           ▼
     ┌───────────────┐
     │  Amazon S3    │◄────────── ② file lands here directly
     │    bucket     │
     └───────────────┘
```

**Numbered flow:**

1. The browser asks API Gateway for permission to upload. API Gateway invokes Lambda,
   which validates the file type and returns a **presigned S3 URL** valid for 5 minutes.
2. The browser uploads the image **straight to S3**. The bytes never reach the
   Elastic Beanstalk instance.
3. The browser posts only the resulting S3 object key to the MVC app, which saves it to
   RDS against the maintenance update record.

## Discussion of Changes

Four arguments, mapped to what the rubric asks for.

**Durable, decoupled storage.** Photos moved from the EC2 instance's ephemeral local disk
to Amazon S3. Files now survive instance replacement and are equally reachable from every
instance behind the load balancer — the single largest correctness improvement in Task #2,
since the previous design lost uploads whenever Elastic Beanstalk recycled an instance.

**Separation of concerns.** Upload authorisation was extracted from the monolith into a
single-purpose Lambda. That function does exactly one thing — validate a request and issue
a scoped, time-limited URL — and knows nothing about tenancies, payments, or maintenance
workflow. This is the microservices principle of decomposing by capability rather than by
technical layer.

**Independent scaling.** Lambda scales per invocation and S3 absorbs upload bandwidth
directly, so the file path now scales separately from the web tier. Previously, concurrent
uploads competed with page rendering for the same EC2 resources.

**Reduced credential surface and loose coupling.** The web server holds no S3 write
credentials; authorisation is delegated to a narrowly-scoped Lambda execution role, and
clients receive single-object URLs that expire in minutes. The MVC app and the upload
function communicate only through a documented HTTP/JSON contract exposed by API Gateway,
so either side can be redeployed — or the Lambda rewritten in another runtime — without
touching the other.

**Limitation worth stating openly.** This is not a full microservices system. The core
application remains a monolith and one capability has been extracted. The accurate term is
a **hybrid architecture** — a server-based system with a serverless component — which is
exactly what the task specifies. Naming this reads as understanding, not as a gap.

---

# Code and Integration

## Services used

| Service | Role |
|---|---|
| Amazon API Gateway | REST endpoint `POST /s3`, `prod` stage — public entry point |
| AWS Lambda | Validates the request and generates presigned S3 URLs |
| Amazon S3 | Durable storage for maintenance repair-evidence photos |

Service chosen from the rubric's required list: **Amazon S3**.

## Screenshots `[TEAM]`

- `[SCREENSHOT]` S3 bucket showing region and uploaded objects
- `[SCREENSHOT]` S3 CORS configuration
- `[SCREENSHOT]` Lambda function code, plus its environment variable
- `[SCREENSHOT]` Lambda IAM execution role and attached policies
- `[SCREENSHOT]` Lambda test invocation returning a presigned URL
- `[SCREENSHOT]` API Gateway resource tree with the POST method
- `[SCREENSHOT]` API Gateway integration screen with Lambda proxy integration enabled
- `[SCREENSHOT]` API Gateway stage and invoke URL
- `[SCREENSHOT]` The C# controller changes and the JavaScript upload logic
- `[SCREENSHOT]` **Browser DevTools Network tab during an upload** — the strongest single
  piece of evidence, because it shows the PUT going to `s3.amazonaws.com` rather than to
  your own server, proving the traffic genuinely bypasses the web tier

## Discussion

Explain *why*, not just *what*:

- Why **Lambda proxy integration** is required — without it the request body never reaches
  the function.
- Why **CORS** must be set on the bucket — the browser now issues a cross-origin PUT,
  which it never did under the old design.
- Why the presigned URL is **short-lived** and scoped to a single object key.
- Why the returned object key is **validated server-side** before being written to the
  database — it arrives from the client and cannot be trusted.
- **Security trade-off, stated openly:** the old design validated file size and type on the
  server. Since the server no longer receives the bytes, that check now runs in the browser
  and again inside the Lambda before any URL is issued.

---

# User Manual

Input/output screenshots stepping through the maintenance staff flow:

1. Log in as maintenance staff
2. Open Assigned Jobs and select a job
3. Click Update Status, choose Completed, add notes, attach a photo
4. Submit — show the success message
5. Show the photo rendered back in the job history
6. Show the same object present in the S3 bucket

---

# Performance Analysis Screenshots

> **Blocked:** requires the application deployed to Elastic Beanstalk. `[TEAM]`

## CloudWatch

- `[SCREENSHOT]` Elastic Beanstalk environment health dashboard
- `[SCREENSHOT]` CPU Utilization graph
- `[SCREENSHOT]` Memory / network metrics
- `[SCREENSHOT]` Custom dashboard combining the above
- `[SCREENSHOT]` Alarm configured at CPU > 80%
- `[SCREENSHOT]` Lambda metrics — invocations, duration, error rate
- `[SCREENSHOT]` Lambda log group showing real invocations
- `[SCREENSHOT]` API Gateway metrics — request count, latency, 4xx/5xx

Lambda and API Gateway publish metrics automatically with no configuration, so these are
the easiest marks in this section.

## X-Ray (optional)

- `[SCREENSHOT]` Service map
- `[SCREENSHOT]` Trace timeline for a single upload

## Analysis to write

- Compare request latency before and after. The upload request to the MVC app should now
  be markedly faster, carrying a short string instead of several megabytes of image data.
- Report Lambda cold-start versus warm-start duration, visible in the log group.
- Comment on cost: Lambda bills per invocation and is effectively free at this volume,
  whereas the old design consumed EC2 CPU, memory, and disk that you pay for continuously.
- Comment on durability: uploads previously lost on instance replacement now persist.

---

# Reflection From Each Member `[TEAM]`

One section per member — what they built, what went wrong, what they learned. Genuinely
useful material from this project: the CORS failure mode, why proxy integration matters,
presigned URL expiry, and the validation trade-off above.

---

# Workload Table Matrix `[TEAM]`

| Member | TP Number | Contribution | % |
|---|---|---|---|

---

# References (APA 7th)

Cite AWS documentation for each service — Lambda, API Gateway, S3 presigned URLs — plus
any tutorial followed. Format: Author. (Year). *Title*. Source. URL
