'use strict';

const { Client, LocalAuth, MessageMedia } = require('whatsapp-web.js');
const qrcode = require('qrcode-terminal');
const { createPanoramicApi } = require('./panoramic-api');
const { createN8nWebhook } = require('./n8n-webhook');
const { formatUpdatedAt, createSummaryCaption, renderSummaryImage } = require('./summary-image');
const { normalizeWhatsAppId } = require('./config');

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

        if (/^@omp\s+manual$/i.test(body)) {
            const chatId = normalizeWhatsAppId(message.from);
            if (chatId !== config.n8nAllowedGroupId) {
                console.warn('Ignored @omp manual command outside the configured WhatsApp group.');
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

        const match = body.match(/^!([a-z0-9]{2,10})\s+Auto\s+CN\s+FT$/i);
        if (!match) {
            return;
        }

        const senderId = normalizeWhatsAppId(message.author || message.from);
        const chatId = normalizeWhatsAppId(message.from);
        if (!config.allowedSenders.has(senderId) && !config.allowedSenders.has(chatId)) {
            console.warn('Ignored FT summary command from an unapproved sender.');
            return;
        }

        const district = match[1].toUpperCase();
        if (!config.allowedDistricts.has(district)) {
            await message.reply(`District ${district} belum diaktifkan untuk perintah bot ini.`);
            return;
        }

        const requestId = `${chatId}:${district}`;
        if (activeRequests.has(requestId)) {
            await message.reply('Permintaan Resume FT untuk district ini sedang diproses.');
            return;
        }

        activeRequests.add(requestId);
        try {
            const summary = await panoramicApi.getFuelTruckSummary(district);
            const updatedAt = formatUpdatedAt(new Date());
            const image = await renderSummaryImage(client.pupBrowser, summary, updatedAt);
            const media = new MessageMedia(
                'image/png',
                image.toString('base64'),
                `resume-ft-${district}.png`
            );
            await client.sendMessage(message.from, media, {
                caption: createSummaryCaption(summary, updatedAt)
            });
        } catch (error) {
            console.error(`Unable to send Resume FT for ${district}:`, error);
            await message.reply('Gagal membuat Resume FT. Periksa koneksi aplikasi dan log bot.');
        } finally {
            activeRequests.delete(requestId);
        }
    }

    void client.initialize().catch((error) => {
        console.error('Failed to initialize WhatsApp client:', error);
        process.exitCode = 1;
    });

    return client;
}

module.exports = { startWhatsAppBot };
