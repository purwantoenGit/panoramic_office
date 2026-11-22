'use strict';

const path = require('node:path');
require('dotenv').config({ path: path.join(__dirname, '.env') });

const { loadConfig } = require('./src/config');
const { startWhatsAppBot } = require('./src/whatsapp-bot');

startWhatsAppBot(loadConfig());
