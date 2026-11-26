# Panoramic FT WhatsApp Bot

The bot replies to `!KPCS Auto CN FT` with a PNG summary for the requested district.
It calls the protected PanoramicAdmin endpoint and renders the summary in a
headless page using the WhatsApp Web browser.

## Configure PanoramicAdmin

Set the same long random API key in PanoramicAdmin and the bot. For local
development, configure `WhatsAppBot__ApiKey` in
`PanoramicAdmin/.env` (see the PanoramicAdmin README). In the bot's `.env`, set
`PANORAMIC_API_TOKEN` to that same value.

For other hosting environments, configure `WhatsAppBot__ApiKey` in the
PanoramicAdmin process environment.

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
- `src/whatsapp-bot.js` handles WhatsApp events and dispatches commands.
- `src/wa-bot-autocn-kpcs.js` handles the `!KPCS Auto CN FT` command.
- `src/wa-bot-autocn-brcb.js` handles the `!BRCB Auto CN` and `!BRCB Auto CN FT` commands.
- `src/wa-bot-scrum-smare.js` handles the `!tugas saya` command for Smart Refueling.
- `src/whatsapp-send-bridge.js` sends authenticated, in-app WhatsApp messages through the connected bot account.
- `src/n8n-webhook.js` calls n8n and parses its reply.
- `src/panoramic-api.js` requests Fuel Truck summary data.
- `src/summary-image.js` renders the PNG and WhatsApp caption.

## Command

```text
!KPCS Auto CN FT
!BRCB Auto CN
!BRCB Auto CN FT
```

Enable each command's district in `WHATSAPP_ALLOWED_DISTRICTS`, separated by
commas (for example, `KPCS,BRCB`).

Send `!id` from a WhatsApp group to have the bot reply with that group's ID.
Use that ID as `WHATSAPP_ALLOWED_SENDERS` to allow every member of that group
to request summaries.

## Smart Refueling Scrum tasks

The command `!tugas saya` returns the sender's tasks across all SMARE PBI
sprints, limited to `Belum dimulai` and `Sedang dikerjakan`. A PBI sprint is
included only when it has at least one matching task. The command is only
enabled in the group IDs listed in `WHATSAPP_SMARE_TASK_GROUP_IDS` (comma-separated).
Send `!id` in the intended group to obtain its ID, then set the variable in the
bot `.env`.

Run `Database/Scripts/Create_MasterPic.sql` against `db_panoramic` before
starting the feature. Add each PIC's WhatsApp number to Master PIC in
international format (for example `+6281234567890`). The bot asks
`whatsapp-web.js` to map a sender's WhatsApp LID to its phone number when
possible. The optional WA ID field is a fallback for accounts whose phone
number cannot be resolved; store the sender ID exactly as shown in WhatsApp,
such as `6281234567890@lid`.

Project Issue's **Kirim WA** button sends the Action Plan directly from the
connected bot account; it does not open a WhatsApp chat. Keep the bot running
and connected. The local send bridge listens on `127.0.0.1` using
`WHATSAPP_BOT_BRIDGE_PORT` (default `3100`) and authenticates with the same
`WhatsAppBot__ApiKey` / `PANORAMIC_API_TOKEN` value. PanoramicAdmin uses
`http://127.0.0.1:3100` by default; override it with
`WhatsAppBot__BridgeUrl` if needed. The bridge is loopback-only by design and
should not be exposed directly to the network.

## n8n command

The configured group can send `@omp manual`. Set the n8n production webhook
URL before starting the bot. `N8N_ALLOWED_GROUP_ID` accepts one or more
comma-separated group IDs:

```bash
export N8N_WEBHOOK_URL='https://your-n8n-host/webhook/whatsapp-omp-manual'
export N8N_WEBHOOK_TOKEN='same-secret-as-the-n8n-header-auth-credential'
export N8N_ALLOWED_GROUP_ID='120363425559351535@g.us,120363412809510667@g.us'
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
