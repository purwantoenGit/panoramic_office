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
        },

        async getSmaReMyTasks(identity) {
            const response = await fetch(
                `${config.apiBaseUrl}/api/whatsapp/smare/my-tasks`,
                {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Bot-Api-Key': config.apiToken
                    },
                    body: JSON.stringify(identity),
                    signal: AbortSignal.timeout(30000)
                }
            );

            if (!response.ok) {
                throw new PanoramicApiError(response.status);
            }

            return response.json();
        }
    };
}

class PanoramicApiError extends Error {
    constructor(status) {
        super(`PanoramicAdmin returned HTTP ${status}.`);
        this.name = 'PanoramicApiError';
        this.status = status;
    }
}

module.exports = { createPanoramicApi, PanoramicApiError };
