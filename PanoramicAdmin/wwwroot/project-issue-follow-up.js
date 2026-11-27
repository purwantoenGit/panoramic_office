window.panoramicFollowUp = {
    takeToken: function () {
        const match = window.location.hash.match(/^#token=([A-Za-z0-9_-]+)$/);
        const token = match ? match[1] : null;
        window.history.replaceState(null, document.title, window.location.pathname + window.location.search);
        return token;
    }
};
