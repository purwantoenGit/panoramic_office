'use strict';

function createPanoramicApi(config) {
    return {
        async getFuelTruckSummary(district) {
            const url = `${config.apiBaseUrl}/api/whatsapp/ft-summary/${encodeURIComponent(district)}?days=7`;
            const response = await fetch(url, {
                headers: { 'X-Bot-Api-Key': config.apiToken },
                signal: AbortSignal.timeout(30000)
            });

            if (!response.ok) {
                throw new Error(`PanoramicAdmin returned HTTP ${response.status}.`);
            }

            return response.json();
        }
    };
}

module.exports = { createPanoramicApi };
