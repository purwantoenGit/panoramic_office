'use strict';

const assert = require('node:assert/strict');
const test = require('node:test');
const {
    extractPhoneNumber,
    formatMyTasksMessages,
    isSmaReMyTasksCommand,
    resolveSenderIdentity
} = require('../src/wa-bot-scrum-smare');

test('matches only the !tugas saya command, ignoring case and outer whitespace', () => {
    assert.equal(isSmaReMyTasksCommand('  !TUGAS saya  '), true);
    assert.equal(isSmaReMyTasksCommand('!tugas saya sekarang'), false);
    assert.equal(isSmaReMyTasksCommand('tugas saya'), false);
});

test('resolves LID to phone when WhatsApp Web provides the mapping', async () => {
    const client = {
        getContactLidAndPhone: async () => [{ lid: '112233@lid', pn: '6281234567890@s.whatsapp.net' }]
    };

    assert.deepEqual(
        await resolveSenderIdentity(client, '112233@lid'),
        { phoneNumber: '+6281234567890', whatsAppId: '112233@lid' }
    );
});

test('keeps LID as a fallback identity rather than treating its digits as a phone', () => {
    assert.equal(extractPhoneNumber('112233@lid'), null);
});

test('only formats returned PBI sprints and splits long output within the WhatsApp limit', () => {
    const tasks = Array.from({ length: 20 }, (_, index) => ({
        code: `TSK-SMARE-${index + 1}`,
        title: `Task ${index + 1} ${'x'.repeat(220)}`,
        status: 'Belum dimulai',
        priority: 'Tinggi',
        estimatedHour: null
    }));

    const messages = formatMyTasksMessages({
        picName: 'PIC Example',
        sprints: [{
            productBacklogItemCode: 'PBI-SMARE-001',
            sprintFocus: 'Validasi data',
            startDate: '2026-11-01T00:00:00',
            endDate: '2026-11-14T00:00:00',
            status: 'Berjalan',
            tasks
        }]
    });

    assert.ok(messages.length > 1);
    assert.ok(messages.every(message => message.length <= 3500));
    assert.ok(messages.join('\n').includes('PBI-SMARE-001'));
    assert.ok(messages.join('\n').includes('01/11/2026'));
    assert.ok(messages.join('\n').includes('📋 *TUGAS SAYA'));
    assert.ok(messages.join('\n').includes('👤 PIC: PIC Example'));
});

test('does not display PBI sprint headings when there are no active sprint boards', () => {
    const messages = formatMyTasksMessages({
        picName: 'PIC Example',
        sprints: []
    });

    assert.equal(messages.length, 1);
    assert.ok(messages[0].includes('Tidak ada tugas Anda'));
    assert.ok(!messages[0].includes('PBI-'));
});
