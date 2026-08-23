# STREAM ON Web deployment

The Unity Web build is written to `public/`. The Pages Function at
`functions/api/stream-on-chat.js` keeps the OpenAI key out of the WebGL client.

## Build from Unity

Use `STREAM ON > Web > Build Release`. This applies the project WebGL settings,
builds every enabled scene into `public/`, and writes the Cloudflare `_headers`
file. `Prepare Project` applies the same settings without starting a build.

## One-time Cloudflare setup

1. Create a Cloudflare Pages project whose root directory is this folder.
2. Add `OPENAI_API_KEY` as an encrypted Pages secret. Never put its value in
   `wrangler.toml`, Unity, Git, or the generated Web build.
3. Deploy the Pages project. The game calls `/api/stream-on-chat` on the same
   origin, so no public API key or custom CORS configuration is required.
4. Add a Cloudflare WAF rate-limiting rule for `/api/stream-on-chat` before a
   public launch. The function also has a lightweight per-isolate safety limit.

For local Pages testing, install Wrangler and run this directory with the
encrypted secret configured in `.dev.vars` (the file is ignored by Git).
