const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
  "Cache-Control": "no-store",
};

function jsonResponse(data, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: {
      ...corsHeaders,
      "Content-Type": "application/json; charset=utf-8",
    },
  });
}

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    // WebGL 브라우저의 CORS 사전 요청
    if (request.method === "OPTIONS") {
      return new Response(null, {
        status: 204,
        headers: corsHeaders,
      });
    }

    // 지정한 채팅 경로만 허용
    if (url.pathname !== "/api/stream-on-chat") {
      return jsonResponse({ error: "Not found" }, 404);
    }

    if (request.method !== "POST") {
      return jsonResponse({ error: "POST requests only" }, 405);
    }

    if (!env.OPENAI_API_KEY) {
  return jsonResponse(
    { error: "OPENAI_API_KEY is not configured" },
    500
  );
}

const apiKey = String(env.OPENAI_API_KEY)
  .replace(/[\u0000-\u0020\u007F]/g, "")
  .replace(/^Bearer\s+/i, "")
  .replace(/^["']|["']$/g, "");

try {
  const bodyText = await request.text();

      // 비정상적으로 큰 요청 차단
      if (new TextEncoder().encode(bodyText).length > 32768) {
        return jsonResponse({ error: "Request is too large" }, 413);
      }

      let payload;

      try {
        payload = JSON.parse(bodyText);
      } catch {
        return jsonResponse({ error: "Invalid JSON" }, 400);
      }

      if (!payload || typeof payload !== "object" || Array.isArray(payload)) {
        return jsonResponse({ error: "Invalid request body" }, 400);
      }

      // 브라우저가 임의로 고가 모델을 지정하지 못하게 서버에서 고정
      payload.model = "gpt-5.6-luna";

      // 응답 길이 제한
      const requestedTokens = Number(payload.max_output_tokens) || 160;
      payload.max_output_tokens = Math.min(
        Math.max(requestedTokens, 32),
        300
      );

      // 현재 게임 채팅에는 외부 도구가 필요하지 않음
      delete payload.tools;

      const upstream = await fetch(
        "https://api.openai.com/v1/responses",
        {
          method: "POST",
          headers: {
            "Authorization": `Bearer ${apiKey}`,
            "Content-Type": "application/json",
          },
          body: JSON.stringify(payload),
        }
      );

      const responseHeaders = new Headers(corsHeaders);
      responseHeaders.set(
        "Content-Type",
        upstream.headers.get("Content-Type") ??
          "application/json; charset=utf-8"
      );

      const upstreamText = await upstream.text();

if (!upstream.ok) {
  const diagnostic = {
    error: "OpenAI upstream rejected request",
    status: upstream.status,
    statusText: upstream.statusText,
    responseBody: upstreamText || "(empty)",
    requestId: upstream.headers.get("x-request-id") || "(none)",
    keyFormat: {
      length: apiKey.length,
      startsWithSk: apiKey.startsWith("sk-"),
      containsWhitespace: /\s/.test(apiKey)
    }
  };

  console.error(
    "OPENAI_DIAGNOSTIC " + JSON.stringify(diagnostic)
  );

  return jsonResponse(diagnostic, upstream.status);
}

return new Response(upstreamText, {
  status: upstream.status,
  headers: responseHeaders,
});
    } catch {
      return jsonResponse({ error: "Proxy request failed" }, 502);
    }
  },
};