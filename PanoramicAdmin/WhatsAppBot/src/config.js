'use strict';

function loadConfig(environment = process.env) {
    const apiBaseUrl = (environment.PANORAMIC_API_BASE_URL || '').replace(/\/+$/, '');
    const apiToken = environment.PANORAMIC_API_TOKEN || '';
    const allowedSenders = new Set(
        (environment.WHATSAPP_ALLOWED_SENDERS || '')
            .split(',')
            .map(normalizeWhatsAppId)
            .filter(Boolean)
    );
    const allowedDistricts = new Set(
        (environment.WHATSAPP_ALLOWED_DISTRICTS || 'KPCS')
            .split(',')
            .map((district) => district.trim().toUpperCase())
            .filter(Boolean)
    );
    const n8nAllowedGroupIds = new Set(
        (environment.N8N_ALLOWED_GROUP_ID || '120363412809510667@g.us')
            .split(',')
            .map(normalizeWhatsAppId)
            .filter(Boolean)
    );
    const smareTaskGroupIds = new Set(
        (environment.WHATSAPP_SMARE_TASK_GROUP_IDS || '')
            .split(',')
            .map(normalizeWhatsAppId)
            .filter(Boolean)
    );
    const bridgePort = Number(environment.WHATSAPP_BOT_BRIDGE_PORT || '3100');

    if (!apiBaseUrl || !apiToken) {
        throw new Error('PANORAMIC_API_BASE_URL and PANORAMIC_API_TOKEN must be configured.');
    }

    if (allowedSenders.size === 0) {
        throw new Error('WHATSAPP_ALLOWED_SENDERS must contain at least one approved sender or group ID.');
    }

    if (!Number.isInteger(bridgePort) || bridgePort < 1 || bridgePort > 65535) {
        throw new Error('WHATSAPP_BOT_BRIDGE_PORT must be a valid TCP port.');
    }

    return {
        apiBaseUrl,
        apiToken,
        allowedSenders,
        allowedDistricts,
        n8nWebhookUrl: (environment.N8N_WEBHOOK_URL || '').trim(),
        n8nWebhookToken: (environment.N8N_WEBHOOK_TOKEN || '').trim(),
        n8nAllowedGroupIds,
        smareTaskGroupIds,
        bridgePort,
        sessionId: (environment.WHATSAPP_SESSION_ID || 'panoramic-ft').trim(),
        puppeteerExecutablePath: environment.PUPPETEER_EXECUTABLE_PATH
    };
}

function normalizeWhatsAppId(value) {
    const id = String(value || '').trim();
    return /^\d+$/.test(id) ? `${id}@c.us` : id;
}

module.exports = { loadConfig, normalizeWhatsAppId };
