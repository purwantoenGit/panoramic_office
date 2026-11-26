'use strict';

const { MessageMedia } = require('whatsapp-web.js');
const {
    formatUpdatedAt,
    createSummaryCaption,
    renderSummaryImage
} = require('./summary-image');
const { normalizeWhatsAppId } = require('./config');

function isBrcbAutoCnCommand(body) {
    return /^!brcb\s+auto\s+cn(?:\s+ft)?$/i.test(body);
}

function createBrcbAutoCnHandler({ config, client, panoramicApi, activeRequests }) {
    return async function handleBrcbAutoCn(message) {
        if (!isBrcbAutoCnCommand(message.body.trim())) {
            return false;
        }

        const senderId = normalizeWhatsAppId(message.author || message.from);
        const chatId = normalizeWhatsAppId(message.from);
        if (!config.allowedSenders.has(senderId) && !config.allowedSenders.has(chatId)) {
            console.warn('Ignored BRCB summary command from an unapproved sender.');
            return true;
        }

        if (!config.allowedDistricts.has('BRCB')) {
            await message.reply('District BRCB belum diaktifkan untuk perintah bot ini.');
            return true;
        }

        const requestId = `${chatId}:BRCB`;
        if (activeRequests.has(requestId)) {
            await message.reply('Permintaan Resume FT untuk district ini sedang diproses.');
            return true;
        }

        activeRequests.add(requestId);
        try {
            const summary = await panoramicApi.getFuelTruckSummary('BRCB');
            const updatedAt = formatUpdatedAt(new Date());
            const image = await renderSummaryImage(client.pupBrowser, summary, updatedAt);
            const media = new MessageMedia(
                'image/png',
                image.toString('base64'),
                'resume-ft-BRCB.png'
            );
            await client.sendMessage(message.from, media, {
                caption: createSummaryCaption(summary, updatedAt)
            });
        } catch (error) {
            console.error('Unable to send Resume FT for BRCB:', error);
            await message.reply('Gagal membuat Resume FT. Periksa koneksi aplikasi dan log bot.');
        } finally {
            activeRequests.delete(requestId);
        }

        return true;
    };
}

module.exports = { createBrcbAutoCnHandler, isBrcbAutoCnCommand };
