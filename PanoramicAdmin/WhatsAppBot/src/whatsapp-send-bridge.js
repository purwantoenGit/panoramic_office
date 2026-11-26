'use strict';

const http = require('node:http');
const { timingSafeEqual } = require('node:crypto');

const MAX_BODY_BYTES = 32 * 1024;
const MAX_MESSAGE_LENGTH = 10000;

function createWhatsAppSendBridge({ client, apiToken }) {
    return http.createServer((request, response) => {
        void handleRequest(request, response, client, apiToken).catch(error => {
            if (error instanceof BridgeRequestError) {
                sendJson(response, error.statusCode, { error: error.message });
                return;
            }

            console.error('WhatsApp send bridge failed:', error);
            sendJson(response, 502, { error: 'WhatsApp message could not be sent.' });
        });
    });
}

async function handleRequest(request, response, client, apiToken) {
    const pathname = new URL(request.url || '/', 'http://127.0.0.1').pathname;
    if (pathname !== '/api/send-message') {
        sendJson(response, 404, { error: 'Endpoint not found.' });
        return;
    }

    if (request.method !== 'POST') {
        response.setHeader('Allow', 'POST');
        sendJson(response, 405, { error: 'Method not allowed.' });
        return;
    }

    if (!isAuthorized(request.headers['x-bot-api-key'], apiToken)) {
        sendJson(response, 401, { error: 'Unauthorized.' });
        return;
    }

    if (!String(request.headers['content-type'] || '').toLowerCase().startsWith('application/json')) {
        sendJson(response, 415, { error: 'Content-Type must be application/json.' });
        return;
    }

    const body = await readJsonBody(request);
    const phoneNumber = body?.phoneNumber;
    const message = body?.message;
    if (typeof phoneNumber !== 'string' ||
        !/^\+[1-9]\d{7,14}$/.test(phoneNumber) ||
        typeof message !== 'string' ||
        message.trim().length === 0 ||
        message.length > MAX_MESSAGE_LENGTH) {
        throw new BridgeRequestError(400, 'Phone number or message is invalid.');
    }

    if (!client.info) {
        throw new BridgeRequestError(503, 'WhatsApp client is not connected.');
    }

    const digits = phoneNumber.slice(1);
    const numberId = await client.getNumberId(digits);
    if (!numberId) {
        throw new BridgeRequestError(422, 'The phone number is not registered on WhatsApp.');
    }

    const recipientId = numberId._serialized || numberId.$1 || `${digits}@c.us`;
    const sentMessage = await client.sendMessage(recipientId, message);
    if (!sentMessage) {
        throw new BridgeRequestError(502, 'WhatsApp did not confirm the message.');
    }

    sendJson(response, 200, { sent: true });
}

function readJsonBody(request) {
    return new Promise((resolve, reject) => {
        const chunks = [];
        let bodyBytes = 0;
        let tooLarge = false;

        request.on('data', chunk => {
            bodyBytes += chunk.length;
            if (bodyBytes > MAX_BODY_BYTES) {
                tooLarge = true;
                reject(new BridgeRequestError(413, 'Request body is too large.'));
                return;
            }

            chunks.push(chunk);
        });

        request.on('end', () => {
            if (tooLarge) {
                return;
            }

            try {
                resolve(JSON.parse(Buffer.concat(chunks).toString('utf8')));
            } catch {
                reject(new BridgeRequestError(400, 'Request body must be valid JSON.'));
            }
        });

        request.on('error', reject);
    });
}

function isAuthorized(providedValue, expectedValue) {
    if (typeof providedValue !== 'string' ||
        typeof expectedValue !== 'string' ||
        expectedValue.length === 0) {
        return false;
    }

    const provided = Buffer.from(providedValue, 'utf8');
    const expected = Buffer.from(expectedValue, 'utf8');
    return provided.length === expected.length && timingSafeEqual(provided, expected);
}

function sendJson(response, statusCode, payload) {
    if (response.headersSent || response.destroyed) {
        return;
    }

    response.writeHead(statusCode, {
        'Cache-Control': 'no-store',
        'Content-Type': 'application/json; charset=utf-8'
    });
    response.end(JSON.stringify(payload));
}

class BridgeRequestError extends Error {
    constructor(statusCode, message) {
        super(message);
        this.statusCode = statusCode;
    }
}

module.exports = { createWhatsAppSendBridge };
