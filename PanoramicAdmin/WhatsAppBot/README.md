# Panoramic FT WhatsApp Bot

The bot replies to `!KPCS Auto CN FT` with a PNG summary for the requested district.
It calls the protected PanoramicAdmin endpoint and renders the summary in a
headless page using the WhatsApp Web browser.

## Configure PanoramicAdmin

Set the same long random API key on the PanoramicAdmin host and in the bot process:

```powershell
$env:WhatsAppBot__ApiKey = "replace-with-a-long-random-secret"
```

The API endpoint is:

```text
GET /api/whatsapp/ft-summary/{district}?days=7
Header: X-Bot-Api-Key: <same secret>
```

## Start the bot

Use Node.js 18 or newer. From this directory, copy the template and edit `.env`
with the PanoramicAdmin URL, matching API key, approved WhatsApp sender/group
IDs, and any optional n8n settings:

```powershell
Copy-Item .env.example .env
notepad .env
npm install
npm start
```

The bot loads `.env` from this directory on startup. Keep real credentials in
`.env`; it is ignored by Git. Do not commit or share it. Values set in the
process environment take precedence over values in `.env`. The `.env.example`
file contains placeholders only and can be copied again to restore the template.

Set `PUPPETEER_EXECUTABLE_PATH` if Puppeteer's Chromium download is unavailable
and Chrome is already installed on the bot host. Otherwise Puppeteer downloads
its managed browser during `npm install`.

The sender allowlist accepts phone numbers (country code, digits only), WhatsApp
IDs such as `6281234567890@c.us`, or group IDs. In a group, allow the sender's
phone ID to restrict the command to specific users, or allow the group ID to
allow every group member to use it. The bot ignores commands from senders not
on this allowlist.

Scan the QR code printed in the terminal with the WhatsApp account used by the
bot. The `LocalAuth` session is stored in `.wwebjs_auth`; keep that directory
persistent on the bot host and never share it. The bot responds in the same
chat where the command was sent.

## Code layout

- `index.js` loads configuration and starts the bot.
- `src/config.js` reads and validates environment variables.
- `src/whatsapp-bot.js` handles WhatsApp events and commands.
- `src/n8n-webhook.js` calls n8n and parses its reply.
- `src/panoramic-api.js` requests Fuel Truck summary data.
- `src/summary-image.js` renders the PNG and WhatsApp caption.

## Command

```text
!KPCS Auto CN FT
```

More districts can be enabled by comma-separating them in
`WHATSAPP_ALLOWED_DISTRICTS`.

Send `!id` from a WhatsApp group to have the bot reply with that group's ID.
Use that ID as `WHATSAPP_ALLOWED_SENDERS` to allow every member of that group
to request summaries.

## n8n command

The configured group can send `@omp manual`. Set the n8n production webhook
URL before starting the bot:

```bash
export N8N_WEBHOOK_URL='https://your-n8n-host/webhook/whatsapp-omp-manual'
export N8N_WEBHOOK_TOKEN='same-secret-as-the-n8n-header-auth-credential'
export N8N_ALLOWED_GROUP_ID='120363412809510667@g.us'
```

In n8n, configure the Webhook node for `POST`, connect it to the processing
nodes, and finish with a Respond to Webhook node. Set the Webhook response mode
to use that response node. The bot sends JSON containing `command`, `text`,
`chatId`, `senderId`, and `receivedAt`. The response should be JSON with a
`message`, `text`, or `reply` string, for example:

```json
{ "message": "Hasil proses data manual..." }
```

If Header Auth is enabled on the Webhook node, use header name
`X-N8N-Webhook-Token` and configure the same value in `N8N_WEBHOOK_TOKEN`.
The result is sent back to the same group. Responses longer than 3,500
characters are truncated, so use a Code or Set node to return a concise
summary rather than all query rows.

`whatsapp-web.js` automates WhatsApp Web and is not the official WhatsApp
Business API. Keep the bot on a trusted host and account for possible WhatsApp
Web changes or account restrictions.
