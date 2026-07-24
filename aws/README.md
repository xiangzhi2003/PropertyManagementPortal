# AWS Setup — Serverless Photo Upload (Task 2)

Console steps for the API Gateway → Lambda → S3 presigned-upload chain.
**Region for everything here: `ap-southeast-1` (Singapore) — your own AWS account.**

This ONE setup serves every photo in the app: the tenant's problem photo, the
maintenance staff's evidence photo, and the manager's read-only view of both.

## 1. S3 bucket

1. S3 → **Create bucket**
2. Name: `propmanage-photos-<your-student-id>` (must be globally unique)
3. Region: **ap-southeast-1**
4. Leave **Block all public access** ticked — presigned URLs work fine with it
5. Create
6. Open the bucket → **Permissions** tab → **Cross-origin resource sharing (CORS)** →
   Edit → paste the contents of [`s3-cors.json`](s3-cors.json) → Save

> CORS is mandatory. Without it the browser's direct PUT is blocked and fails with
> a vague error that never mentions permissions. Narrow `AllowedOrigins` from `*`
> to your real app URL before final submission.

## 2. Lambda

1. Lambda → **Create function** → Author from scratch
2. Name: `PropertyManagementPhotos`
3. Runtime: **Node.js 22.x**
4. Create
5. **Configuration → Environment variables** → Add: `BUCKET_NAME` = your bucket name
6. **Configuration → Permissions** → click the execution role name (opens IAM) →
   **Add permissions → Attach policies** → attach `AmazonS3FullAccess`
   (`AWSLambdaBasicExecutionRole` for CloudWatch Logs is already attached)
7. **Code** tab → replace `index.mjs` with [`lambda/index.mjs`](lambda/index.mjs) → **Deploy**

### Test before moving on

Test tab → create an event → Test:

```json
{ "action": "getUploadUrl", "fileName": "test.jpg", "contentType": "image/jpeg" }
```

Expect a 200 with `uploadUrl` and `key`.

## 3. API Gateway

1. API Gateway → **Create API** → **REST API** (not HTTP API, not private) → Build
2. Name: `PropertyManagementAPI` → Create
3. **Create resource** → name `photos` → tick **CORS** → Create
4. Select `/photos` → **Create method** → **POST**
   - Integration type: **Lambda function**
   - Tick **Lambda proxy integration** ← essential, or `event.body` arrives empty
   - Region ap-southeast-1, Function: `PropertyManagementPhotos`
   - Create (grant permission when prompted)
5. Select `/photos` → **Enable CORS** → tick POST and OPTIONS → save
6. **Deploy API** → New stage → name `prod` → Deploy
7. Copy the **Invoke URL**. Endpoint is that URL + `/photos`

### Verify the whole chain

```bash
curl -X POST https://<id>.execute-api.ap-southeast-1.amazonaws.com/prod/photos \
  -H "Content-Type: application/json" \
  -d '{"action":"getUploadUrl","fileName":"test.jpg","contentType":"image/jpeg"}'
```

Take the returned `uploadUrl` and push a real file:

```bash
curl -X PUT --upload-file test.jpg -H "Content-Type: image/jpeg" "<paste-uploadUrl>"
```

Confirm the object lands in the bucket under `photos/`. **Do not move on until this
works** — it isolates AWS problems from application problems.

## 4. Wire into the app

Add to `PropertyManagementPortal/appsettings.json`:

```json
"ApiGateway": {
  "S3Endpoint": "https://<id>.execute-api.ap-southeast-1.amazonaws.com/prod/photos"
}
```

`appsettings.json` is gitignored — send this value to teammates privately, not via git.

## Troubleshooting

| Symptom | Cause |
|---|---|
| Browser console shows a CORS error on PUT | Bucket CORS missing or wrong (step 1.6) |
| Lambda receives an empty body | **Lambda proxy integration** not ticked (step 3.4) |
| S3 returns 403 on the PUT | Browser sent a different `Content-Type` than the one used to sign |
| `{"message":"Missing Authentication Token"}` | Wrong URL — missing `/photos`, or not deployed to `prod` |
| Changes to the Lambda have no effect | Forgot to click **Deploy** in the Code tab |
| `NoSuchBucket` | Bucket name/region mismatch, or bucket was deleted |
