'use strict';

const MAX_REPLY_LENGTH = 3500;

function createN8nWebhook(config) {
    return {
        async runOmpManual(message) {
            if (!config.n8nWebhookUrl) {
                throw new Error('N8N_WEBHOOK_URL is not configured.');
            }

            const headers = { 'Content-Type': 'application/json' };
            if (config.n8nWebhookToken) {
                headers['X-N8N-Webhook-Token'] = config.n8nWebhookToken;
            }

            const response = await fetch(config.n8nWebhookUrl, {
                method: 'POST',
                headers,
                body: JSON.stringify({
                    command: '@omp manual',
                    text: message.body,
                    chatId: message.from,
                    senderId: message.author || message.from,
                    receivedAt: new Date().toISOString()
                }),
                signal: AbortSignal.timeout(120000)
            });
            const body = await response.text();

            if (!response.ok) {
                throw new Error(`n8n returned HTTP ${response.status}.`);
            }

            const reply = parseN8nReply(body, response.headers.get('content-type'));
            if (!reply) {
                throw new Error('n8n returned an empty response.');
            }

            return reply.length > MAX_REPLY_LENGTH
                ? `${reply.slice(0, MAX_REPLY_LENGTH)}\n\n(Hasil dipotong; batasi atau ringkas output di workflow n8n.)`
                : reply;
        }
    };
}

function parseN8nReply(body, contentType) {
    if (!body.trim()) {
        return '';
    }

    if (!contentType?.includes('application/json')) {
        return body.trim();
    }

    const result = JSON.parse(body);
    if (typeof result === 'string') {
        return result.trim();
    }

    if (result && typeof result === 'object' && !Array.isArray(result)) {
        for (const key of ['message', 'text', 'reply']) {
            if (typeof result[key] === 'string' && result[key].trim()) {
                return result[key].trim();
            }
        }
    }

    return JSON.stringify(result, null, 2);
}

module.exports = { createN8nWebhook, parseN8nReply };
