// AWS Lambda — PropertyManagementPhotos
// Runtime: Node.js 22.x   Region: ap-southeast-1
//
// Paste into the Lambda console inline editor (index.mjs) and click Deploy.
// Required environment variable:  BUCKET_NAME = <your S3 bucket>
//
// Used by every photo upload in the app — tenant's problem photo, staff's
// evidence photo, and the manager's read-only view of both. One Lambda,
// one bucket, one API Gateway endpoint serves all three.
//
// Two actions, both POSTed to the same resource (/photos):
//   { "action": "getUploadUrl", "fileName": "x.jpg", "contentType": "image/jpeg" }
//   { "action": "getViewUrl",   "key": "photos/uuid-x.jpg" }
//
// Both SDK packages below ship with the Node 22 runtime — no npm install needed.

import { S3Client, PutObjectCommand, GetObjectCommand } from "@aws-sdk/client-s3";
import { getSignedUrl } from "@aws-sdk/s3-request-presigner";
import { randomUUID } from "crypto";

const s3 = new S3Client({});
const BUCKET = process.env.BUCKET_NAME;

const KEY_PREFIX = "photos/";
const ALLOWED_EXTENSIONS = [".jpg", ".jpeg", ".png", ".webp"];
const UPLOAD_URL_EXPIRY_SECONDS = 300;   // 5 minutes — just long enough to upload
const VIEW_URL_EXPIRY_SECONDS = 3600;    // 1 hour

const CORS_HEADERS = {
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Headers": "Content-Type",
    "Access-Control-Allow-Methods": "OPTIONS,POST",
    "Content-Type": "application/json"
};

const reply = (statusCode, payload) => ({
    statusCode,
    headers: CORS_HEADERS,
    body: JSON.stringify(payload)
});

// API Gateway proxy delivers the body as a JSON *string*. The console's Test tab
// delivers a plain object. Accept both so the function can be tested either way.
function parseBody(event) {
    if (!event) return {};
    if (typeof event.body === "string") return JSON.parse(event.body);
    if (event.body && typeof event.body === "object") return event.body;
    return event;
}

// Strips any directory component a client might inject and keeps the extension.
function safeFileName(name) {
    return String(name).split(/[\\/]/).pop().replace(/[^\w.\-]/g, "_");
}

export const handler = async (event) => {
    if (!BUCKET) {
        console.error("BUCKET_NAME environment variable is not set");
        return reply(500, { error: "Server is not configured" });
    }

    let body;
    try {
        body = parseBody(event);
    } catch (err) {
        console.error("Malformed JSON body", err);
        return reply(400, { error: "Request body is not valid JSON" });
    }

    const action = body.action;
    console.log("Action requested:", action);

    try {
        if (action === "getUploadUrl") {
            const fileName = safeFileName(body.fileName || "");
            const contentType = String(body.contentType || "");

            const extension = fileName.slice(fileName.lastIndexOf(".")).toLowerCase();
            if (!ALLOWED_EXTENSIONS.includes(extension)) {
                return reply(400, { error: "Only JPG, PNG and WEBP images are accepted" });
            }
            if (!contentType.startsWith("image/")) {
                return reply(400, { error: "The file is not an image" });
            }

            const key = `${KEY_PREFIX}${randomUUID()}-${fileName}`;

            // The signature is bound to this exact ContentType — the browser MUST
            // send an identical Content-Type header on the PUT or S3 returns 403.
            const uploadUrl = await getSignedUrl(
                s3,
                new PutObjectCommand({ Bucket: BUCKET, Key: key, ContentType: contentType }),
                { expiresIn: UPLOAD_URL_EXPIRY_SECONDS }
            );

            console.log("Issued upload URL for key:", key);
            return reply(200, { uploadUrl, key });
        }

        if (action === "getViewUrl") {
            const key = String(body.key || "");

            // Only ever hand out URLs for objects this function could have created.
            if (!key.startsWith(KEY_PREFIX) || key.includes("..")) {
                return reply(400, { error: "Invalid object key" });
            }

            const viewUrl = await getSignedUrl(
                s3,
                new GetObjectCommand({ Bucket: BUCKET, Key: key }),
                { expiresIn: VIEW_URL_EXPIRY_SECONDS }
            );

            return reply(200, { viewUrl });
        }

        return reply(400, { error: "Unknown action. Use 'getUploadUrl' or 'getViewUrl'." });
    } catch (err) {
        console.error("Unhandled error:", err);
        return reply(500, { error: "Could not process the request" });
    }
};
