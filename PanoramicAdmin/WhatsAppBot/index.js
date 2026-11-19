'use strict';

const { loadConfig } = require('./src/config');
const { startWhatsAppBot } = require('./src/whatsapp-bot');

startWhatsAppBot(loadConfig());
