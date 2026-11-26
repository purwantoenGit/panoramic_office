'use strict';

const assert = require('node:assert/strict');
const { once } = require('node:events');
const test = require('node:test');
const { createWhatsAppSendBridge } = require('../src/whatsapp-send-bridge');

async function startBridge(t, client, apiToken = 'test-secret') {
    const server = createWhatsAppSendBridge({ client, apiToken });
    server.listen(0, '127.0.0.1');
    await once(server, 'listening');
    t.after(() => new Promise(resolve => server.close(resolve)));
    return `http://127.0.0.1:${server.address().port}/api/send-message`;
}

test('authenticated request sends a direct WhatsApp message to a resolved number', async (t) => {
    const sent = [];
    const client = {
        info: { wid: 'bot@c.us' },
        getNumberId: async number => ({ _serialized: `${number}@c.us` }),
        sendMessage: async (recipient, message) => {
            sent.push({ recipient, message });
            return { id: 'message-id' };
        }
    };
    const endpoint = await startBridge(t, client);
    const response = await fetch(endpoint, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Bot-Api-Key': 'test-secret'
        },
        body: JSON.stringify({
            phoneNumber: '+6281234567890',
            message: 'Action Plan detail'
        })
    });

    assert.equal(response.status, 200);
    assert.deepEqual(await response.json(), { sent: true });
    assert.deepEqual(sent, [{
        recipient: '6281234567890@c.us',
        message: 'Action Plan detail'
    }]);
});

test('rejects unauthorized requests before sending', async (t) => {
    const client = {
        info: { wid: 'bot@c.us' },
        getNumberId: async () => assert.fail('Must not resolve a recipient'),
        sendMessage: async () => assert.fail('Must not send a message')
    };
    const endpoint = await startBridge(t, client);
    const response = await fetch(endpoint, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            phoneNumber: '+6281234567890',
            message: 'Action Plan detail'
        })
    });

    assert.equal(response.status, 401);
});

test('rejects the send when WhatsApp is disconnected', async (t) => {
    const client = {
        info: null,
        getNumberId: async () => assert.fail('Must not resolve a recipient'),
        sendMessage: async () => assert.fail('Must not send a message')
    };
    const endpoint = await startBridge(t, client);
    const response = await fetch(endpoint, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'X-Bot-Api-Key': 'test-secret'
        },
        body: JSON.stringify({
            phoneNumber: '+6281234567890',
            message: 'Action Plan detail'
        })
    });

    assert.equal(response.status, 503);
});
