'use strict';

const { normalizeWhatsAppId } = require('./config');

function createSmaReScrumTasksHandler({ config, client, panoramicApi, activeRequests }) {
    return async function handleSmaReScrumTasks(message) {
        if (!isSmaReMyTasksCommand(message.body)) {
            return false;
        }

        if (!String(message.from || '').endsWith('@g.us')) {
            await message.reply('Kirim perintah !tugas saya dari dalam grup WhatsApp.');
            return true;
        }

        const chatId = normalizeWhatsAppId(message.from);
        if (!config.smareTaskGroupIds?.has(chatId)) {
            await message.reply('Perintah tugas SMARE belum diaktifkan untuk grup ini. Hubungi admin bot.');
            return true;
        }

        const senderId = message.author;
        if (!senderId) {
            await message.reply('Pengirim tidak dapat diidentifikasi. Pastikan perintah dikirim dari akun PIC.');
            return true;
        }

        const requestId = `smare-tasks:${chatId}:${senderId}`;
        if (activeRequests.has(requestId)) {
            await message.reply('Permintaan tugas Anda sedang diproses.');
            return true;
        }

        activeRequests.add(requestId);
        try {
            const identity = await resolveSenderIdentity(client, senderId);
            const result = await panoramicApi.getSmaReMyTasks(identity);
            const replies = formatMyTasksMessages(result);
            for (const reply of replies) {
                await client.sendMessage(message.from, reply);
            }
        } catch (error) {
            if (error.status === 404) {
                await message.reply(
                    'Nomor WhatsApp/WA ID Anda belum terdaftar di Master PIC. Minta admin mengisi kontak PIC Anda.'
                );
            } else if (error.status === 409) {
                await message.reply(
                    'Identitas WhatsApp cocok dengan lebih dari satu PIC. Minta admin memeriksa Master PIC.'
                );
            } else {
                console.error('Unable to get Smart Refueling Scrum tasks:', error);
                await message.reply('Gagal memuat tugas SMARE. Periksa koneksi aplikasi dan log bot.');
            }
        } finally {
            activeRequests.delete(requestId);
        }

        return true;
    };
}

function isSmaReMyTasksCommand(body) {
    return /^!tugas\s+saya$/i.test(String(body || '').trim());
}

async function resolveSenderIdentity(client, senderId) {
    const whatsAppId = normalizeWhatsAppId(senderId);
    let phoneNumber = extractPhoneNumber(whatsAppId);

    if (typeof client.getContactLidAndPhone === 'function') {
        try {
            const [mapping] = await client.getContactLidAndPhone([senderId]);
            phoneNumber = extractPhoneNumber(mapping?.pn) || phoneNumber;
        } catch (error) {
            console.warn(
                'Unable to resolve the WhatsApp sender phone; trying the saved WhatsApp ID.',
                error
            );
        }
    }

    return { phoneNumber, whatsAppId };
}

function extractPhoneNumber(whatsAppId) {
    const normalizedId = normalizeWhatsAppId(whatsAppId);
    const match = normalizedId.match(/^(\d+)@(c\.us|s\.whatsapp\.net)$/i);
    return match ? `+${match[1]}` : null;
}

function formatMyTasksMessages(result, maxLength = 3500) {
    const sprints = Array.isArray(result.sprints) ? result.sprints : [];
    const lines = [
        '📋 *TUGAS SAYA - SMART REFUELING*',
        `👤 PIC: ${result.picName || '-'}`,
        ''
    ];

    if (sprints.length === 0) {
        lines.push(
            'Tidak ada tugas Anda berstatus Belum dimulai atau Sedang dikerjakan di seluruh Sprint PBI.'
        );
    } else {
        for (const sprint of sprints) {
            const sprintStatusIcon = sprint.status === 'Berjalan' ? '🟢' : '⚪';
            lines.push(
                `🧩 *${sprint.productBacklogItemCode}* · ${sprint.sprintFocus} · ` +
                `📅 ${formatSprintDate(sprint.startDate)}–${formatSprintDate(sprint.endDate)} · ` +
                `${sprintStatusIcon} ${sprint.status}`
            );
            const tasks = Array.isArray(sprint.tasks) ? sprint.tasks : [];
            for (const status of ['Sedang dikerjakan', 'Belum dimulai']) {
                const statusTasks = tasks.filter(task => task.status === status);
                if (statusTasks.length > 0) {
                    lines.push(`${status === 'Sedang dikerjakan' ? '🔄' : '⏳'} ${status}`);
                }
                for (const task of statusTasks) {
                    const estimate = task.estimatedHour == null
                        ? ''
                        : ` · ⏱️ ${task.estimatedHour} jam`;
                    lines.push(`• ${task.code} · ${task.title} · 🎯 ${task.priority}${estimate}`);
                }
            }
            lines.push('');
        }
    }

    return splitMessage(lines, maxLength);
}

function formatSprintDate(value) {
    const match = String(value || '').match(/^(\d{4})-(\d{2})-(\d{2})/);
    return match ? `${match[3]}/${match[2]}/${match[1]}` : '-';
}

function splitMessage(lines, maxLength) {
    const messages = [];
    let current = '';

    for (const line of lines) {
        const next = current ? `${current}\n${line}` : line;
        if (next.length > maxLength && current) {
            messages.push(current);
            current = line;
        } else {
            current = next;
        }
    }

    if (current) {
        messages.push(current);
    }

    return messages;
}

module.exports = {
    createSmaReScrumTasksHandler,
    extractPhoneNumber,
    formatMyTasksMessages,
    formatSprintDate,
    isSmaReMyTasksCommand,
    resolveSenderIdentity
};
