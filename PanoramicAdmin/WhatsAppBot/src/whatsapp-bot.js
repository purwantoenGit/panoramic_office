'use strict';

const { Client, LocalAuth } = require('whatsapp-web.js');
const qrcode = require('qrcode-terminal');
const { createPanoramicApi } = require('./panoramic-api');
const { createN8nWebhook } = require('./n8n-webhook');
const { normalizeWhatsAppId } = require('./config');
const { createKpcsAutoCnFtHandler } = require('./wa-bot-autocn-kpcs');
const { createBrcbAutoCnHandler } = require('./wa-bot-autocn-brcb');
const { createSmaReScrumTasksHandler } = require('./wa-bot-scrum-smare');
const { createWhatsAppSendBridge } = require('./whatsapp-send-bridge');

function startWhatsAppBot(config) {
    const client = new Client({
        authStrategy: new LocalAuth({ clientId: config.sessionId }),
        puppeteer: {
            ...(config.puppeteerExecutablePath
                ? { executablePath: config.puppeteerExecutablePath }
                : {}),
            headless: true,
            args: ['--disable-dev-shm-usage']
        }
    });
    const panoramicApi = createPanoramicApi(config);
    const n8nWebhook = createN8nWebhook(config);
    const activeRequests = new Set();
    const handleKpcsAutoCnFt = createKpcsAutoCnFtHandler({
        config,
        client,
        panoramicApi,
        activeRequests
    });
    const handleBrcbAutoCn = createBrcbAutoCnHandler({
        config,
        client,
        panoramicApi,
        activeRequests
    });
    const handleSmaReScrumTasks = createSmaReScrumTasksHandler({
        config,
        client,
        panoramicApi,
        activeRequests
    });
    const sendBridge = createWhatsAppSendBridge({ client, apiToken: config.apiToken });

    sendBridge.on('error', (error) => {
        console.error('Unable to start the local WhatsApp send bridge:', error);
        process.exitCode = 1;
    });
    sendBridge.listen(config.bridgePort, '127.0.0.1', () => {
        console.log(`Local WhatsApp send bridge listening on 127.0.0.1:${config.bridgePort}.`);
    });

    client.on('qr', (qr) => {
        console.log('Scan QR ini menggunakan WhatsApp pada perangkat bot:');
        qrcode.generate(qr, { small: true });
    });

    client.on('ready', () => {
        console.log('WhatsApp bot siap menerima perintah.');
    });

    client.on('auth_failure', (message) => {
        console.error('WhatsApp authentication failed:', message);
    });

    client.on('disconnected', (reason) => {
        console.error('WhatsApp client disconnected:', reason);
    });

    client.on('message', (message) => {
        void handleMessage(message).catch((error) => {
            console.error('Failed to process WhatsApp message:', error);
        });
    });

    async function handleMessage(message) {
        if (message.fromMe || message.from === 'status@broadcast') {
            return;
        }

        const body = message.body.trim();
        if (body.toLowerCase() === '!id') {
            if (message.from.endsWith('@g.us')) {
                await message.reply(`ID grup ini: ${message.from}`);
            } else {
                await message.reply('Kirim !id dari dalam grup WhatsApp.');
            }
            return;
        }

        if (isOmpManualCommand(body)) {
            const chatId = normalizeWhatsAppId(message.from);
            if (!config.n8nAllowedGroupIds.has(chatId)) {
                console.warn('Ignored @omp manual command outside the configured WhatsApp groups.');
                return;
            }

            const requestId = `n8n:${chatId}`;
            if (activeRequests.has(requestId)) {
                await message.reply('Permintaan @omp manual sedang diproses.');
                return;
            }

            activeRequests.add(requestId);
            try {
                const result = await n8nWebhook.runOmpManual(message);
                await message.reply(result);
            } catch (error) {
                console.error('Unable to process @omp manual through n8n:', error);
                await message.reply('Gagal memproses @omp manual melalui n8n. Periksa konfigurasi dan workflow.');
            } finally {
                activeRequests.delete(requestId);
            }
            return;
        }

        if (await handleKpcsAutoCnFt(message)) {
            return;
        }

        if (await handleBrcbAutoCn(message)) {
            return;
        }

        await handleSmaReScrumTasks(message);
    }

    void client.initialize().catch((error) => {
        console.error('Failed to initialize WhatsApp client:', error);
        process.exitCode = 1;
    });

    return client;
}

function isOmpManualCommand(body) {
    return /^@omp\s+manual\b/i.test(body);
}

module.exports = { startWhatsAppBot, isOmpManualCommand };
