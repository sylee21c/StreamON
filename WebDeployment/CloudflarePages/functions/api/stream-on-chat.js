const ALLOWED_FORMATS = new Set(["runner_chat_batch", "wit_interaction"]);
const localRateBuckets = new Map();

export async function onRequest(context) {
  const { request, env } = context;
  const url = new URL(request.url);
  const origin = request.headers.get("Origin") || url.origin;
  const cors = corsHeaders(origin);

  if (!isAllowedOrigin(origin, url.origin, env.ALLOWED_ORIGINS)) {
    return json({ error: { message: "Origin is not allowed." } }, 403, cors);
  }
  if (request.method === "OPTIONS") {
    return new Response(null, { status: 204, headers: cors });
  }
  if (request.method !== "POST") {
    return json({ error: { message: "Only POST is supported." } }, 405, cors);
  }
  if (!env.OPENAI_API_KEY) {
    return json({ error: { message: "The AI relay secret is not configured." } }, 503, cors);
  }

  const clientKey = request.headers.get("CF-Connecting-IP") || "unknown";
  if (!consumeLocalRateLimit(clientKey, Number(env.REQUESTS_PER_MINUTE || 30))) {
    return json({ error: { message: "Too many AI chat requests." } }, 429, cors);
  }
  if (env.AI_RATE_LIMITER && typeof env.AI_RATE_LIMITER.limit === "function") {
    const outcome = await env.AI_RATE_LIMITER.limit({ key: clientKey });
    if (!outcome.success) return json({ error: { message: "Too many AI chat requests." } }, 429, cors);
  }

  const declaredLength = Number(request.headers.get("Content-Length") || 0);
  const maximumBytes = Number(env.MAXIMUM_REQUEST_BYTES || 100000);
  if (declaredLength > maximumBytes) {
    return json({ error: { message: "AI request is too large." } }, 413, cors);
  }

  let bodyText;
  let payload;
  try {
    bodyText = await request.text();
    if (new TextEncoder().encode(bodyText).length > maximumBytes) throw new Error("request_too_large");
    payload = JSON.parse(bodyText);
  } catch (error) {
    const status = error?.message === "request_too_large" ? 413 : 400;
    return json({ error: { message: status === 413 ? "AI request is too large." : "Invalid JSON body." } }, status, cors);
  }

  const formatName = payload?.text?.format?.name;
  if (!ALLOWED_FORMATS.has(formatName) || !Array.isArray(payload?.input)) {
    return json({ error: { message: "Unsupported STREAM ON AI request." } }, 400, cors);
  }

  // Cost- and capability-sensitive options are always controlled server-side.
  payload.model = env.OPENAI_MODEL || "gpt-5.6-luna";
  payload.store = false;
  payload.max_output_tokens = formatName === "wit_interaction" ? 350 : 500;
  payload.tools = undefined;
  payload.background = undefined;

  let upstream;
  try {
    upstream = await fetch("https://api.openai.com/v1/responses", {
      method: "POST",
      headers: {
        "Authorization": `Bearer ${env.OPENAI_API_KEY}`,
        "Content-Type": "application/json"
      },
      body: JSON.stringify(payload)
    });
  } catch {
    return json({ error: { message: "The AI service is temporarily unreachable." } }, 502, cors);
  }

  const headers = new Headers(cors);
  headers.set("Content-Type", upstream.headers.get("Content-Type") || "application/json; charset=utf-8");
  headers.set("Cache-Control", "no-store");
  const requestId = upstream.headers.get("x-request-id");
  if (requestId) headers.set("x-request-id", requestId);
  return new Response(upstream.body, { status: upstream.status, headers });
}

function isAllowedOrigin(origin, sameOrigin, configuredOrigins) {
  if (origin === sameOrigin) return true;
  const allowed = String(configuredOrigins || "").split(",").map(value => value.trim()).filter(Boolean);
  return allowed.includes(origin);
}

function corsHeaders(origin) {
  return {
    "Access-Control-Allow-Origin": origin,
    "Access-Control-Allow-Methods": "POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type",
    "Vary": "Origin",
    "X-Content-Type-Options": "nosniff",
    "Referrer-Policy": "same-origin"
  };
}

function json(value, status, headers) {
  const responseHeaders = new Headers(headers);
  responseHeaders.set("Content-Type", "application/json; charset=utf-8");
  responseHeaders.set("Cache-Control", "no-store");
  return new Response(JSON.stringify(value), { status, headers: responseHeaders });
}

function consumeLocalRateLimit(key, limit) {
  const safeLimit = Number.isFinite(limit) ? Math.max(1, Math.min(120, limit)) : 30;
  const minute = Math.floor(Date.now() / 60000);
  const previous = localRateBuckets.get(key);
  const bucket = previous && previous.minute === minute ? previous : { minute, count: 0 };
  bucket.count += 1;
  localRateBuckets.set(key, bucket);
  if (localRateBuckets.size > 5000) localRateBuckets.clear();
  return bucket.count <= safeLimit;
}
