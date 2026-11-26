'use strict';

const { MessageMedia } = require('whatsapp-web.js');
const {
    formatUpdatedAt,
    createSummaryCaption,
    renderSummaryImage
} = require('./summary-image');
const { normalizeWhatsAppId } = require('./config');

function isKpcsAutoCnFtCommand(body) {
    return /^!kpcs\s+Auto\s+CN\s+FT$/i.test(body);
}

function createKpcsAutoCnFtHandler({ config, client, panoramicApi, activeRequests }) {
    return async function handleKpcsAutoCnFt(message) {
        if (!isKpcsAutoCnFtCommand(message.body.trim())) {
            return false;
        }

        const senderId = normalizeWhatsAppId(message.author || message.from);
        const chatId = normalizeWhatsAppId(message.from);
        if (!config.allowedSenders.has(senderId) && !config.allowedSenders.has(chatId)) {
            console.warn('Ignored FT summary command from an unapproved sender.');
            return true;
        }

        if (!config.allowedDistricts.has('KPCS')) {
            await message.reply('District KPCS belum diaktifkan untuk perintah bot ini.');
            return true;
        }

        const requestId = `${chatId}:KPCS`;
        if (activeRequests.has(requestId)) {
            await message.reply('Permintaan Resume FT untuk district ini sedang diproses.');
            return true;
        }

        activeRequests.add(requestId);
        try {
            const summary = await panoramicApi.getFuelTruckSummary('KPCS');
            const updatedAt = formatUpdatedAt(new Date());
            const image = await renderSummaryImage(client.pupBrowser, summary, updatedAt);
            const media = new MessageMedia(
                'image/png',
                image.toString('base64'),
                'resume-ft-KPCS.png'
            );
            await client.sendMessage(message.from, media, {
                caption: createSummaryCaption(summary, updatedAt)
            });
        } catch (error) {
            console.error('Unable to send Resume FT for KPCS:', error);
            await message.reply('Gagal membuat Resume FT. Periksa koneksi aplikasi dan log bot.');
        } finally {
            activeRequests.delete(requestId);
        }

        return true;
    };
}

module.exports = { createKpcsAutoCnFtHandler, isKpcsAutoCnFtCommand };
