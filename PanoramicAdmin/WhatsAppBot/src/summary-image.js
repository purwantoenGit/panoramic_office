'use strict';

async function renderSummaryImage(browser, summary, updatedAt) {
    if (!browser) {
        throw new Error('WhatsApp browser is not ready.');
    }

    const page = await browser.newPage();
    try {
        await page.setViewport({ width: 1200, height: 900, deviceScaleFactor: 1 });
        await page.setContent(createSummaryHtml(summary, updatedAt), { waitUntil: 'domcontentloaded' });
        const summaryElement = await page.$('#summary');
        if (!summaryElement) {
            throw new Error('Summary image content was not rendered.');
        }

        return await summaryElement.screenshot({ type: 'png' });
    } finally {
        await page.close();
    }
}

function createSummaryHtml(summary, updatedAt) {
    const rfidCount = Number(summary.rfidWarehouseCount) || 0;
    const totalCount = Number(summary.totalWarehouseCount) || 0;
    const percentage = Math.min(100, Math.max(0, Number(summary.rfidSuccessPercentage) || 0));
    const rfidUnits = renderUnitList(summary.rfidWarehouseIds, 'Tidak ada FT dengan RFID pada periode ini.');
    const manualUnits = renderUnitList(summary.manualWarehouseIds, 'Tidak ada FT manual pada periode ini.');

    return `<!doctype html>
<html lang="id">
<head>
  <meta charset="utf-8">
  <style>
    * { box-sizing: border-box; }
    body { margin: 0; padding: 28px; color: #333; font-family: Arial, sans-serif; background: #fff; }
    #summary { width: 1144px; padding: 0; }
    h1 { margin: 0 0 30px; font-size: 26px; font-weight: 500; }
    .updated { margin: -20px 0 24px; color: #667085; font-size: 14px; }
    .period { display: flex; align-items: center; min-height: 100px; padding: 22px 26px; border: 1px solid #dce7f5; border-radius: 17px; background: #f4f8fd; }
    .period-label, .card-label { color: #666; font-size: 14px; letter-spacing: 1px; }
    .period-value { margin-top: 12px; color: #224b83; font-size: 20px; font-weight: 700; }
    .rate { margin-top: 22px; padding: 30px 26px 24px; border: 1px solid #cce6d3; border-radius: 18px; background: linear-gradient(135deg, #f3fbf5, #fff); }
    .rate-heading, .card-heading { display: flex; align-items: center; justify-content: space-between; gap: 20px; }
    .rate-value { margin-top: 12px; color: #267343; font-size: 60px; line-height: 1; font-weight: 800; }
    .badge { padding: 10px 16px; border: 1px solid #00c853; border-radius: 24px; color: #00b84a; font-size: 16px; white-space: nowrap; }
    .track { height: 11px; margin-top: 22px; overflow: hidden; border-radius: 999px; background: #dcebe0; }
    .track > span { display: block; height: 100%; border-radius: inherit; background: linear-gradient(90deg, #50a96b, #267343); }
    .cards { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-top: 30px; }
    .card { min-height: 220px; padding: 25px; border: 1px solid; border-radius: 18px; }
    .card.success { border-color: #cce6d3; }
    .card.manual { border-color: #f3d9b2; }
    .card-count { margin-top: 14px; font-size: 28px; }
    .units { display: flex; flex-wrap: wrap; gap: 12px; margin-top: 20px; }
    .unit { padding: 7px 12px; border: 1px solid; border-radius: 20px; font-size: 14px; }
    .success .unit { border-color: #00c853; color: #00b84a; }
    .manual .unit { border-color: #ff9800; color: #f18700; }
    .empty { color: #777; font-size: 14px; }
  </style>
</head>
<body>
  <main id="summary">
    <h1>RESUME DATA RFID FT - ${escapeHtml(summary.district)}</h1>
    <p class="updated">🕒 Update: ${escapeHtml(updatedAt)}</p>
    <section class="period">
      <div>
        <div class="period-label">PERIODE DATA</div>
        <div class="period-value">${Number(summary.selectedDays) || 7} hari terakhir</div>
      </div>
    </section>
    <section class="rate">
      <div class="rate-heading">
        <div>
          <div class="card-label">RFID FT Success Install</div>
          <div class="rate-value">${formatPercentage(percentage)}%</div>
        </div>
        <div class="badge">${rfidCount} dari ${totalCount} FT</div>
      </div>
      <div class="track"><span style="width:${percentage}%"></span></div>
    </section>
    <section class="cards">
      <article class="card success">
        <div class="card-heading">
          <div>
            <div class="card-label">FT SUDAH RFID</div>
            <div class="card-count">${rfidCount}</div>
          </div>
          <div style="font-size:30px;color:#00c853">✓</div>
        </div>
        <div class="units">${rfidUnits}</div>
      </article>
      <article class="card manual">
        <div class="card-heading">
          <div>
            <div class="card-label">FT BELUM RFID (MANUAL)</div>
            <div class="card-count">${(Array.isArray(summary.manualWarehouseIds) ? summary.manualWarehouseIds.length : 0)}</div>
          </div>
          <div style="font-size:28px;color:#ff9800">◷</div>
        </div>
        <div class="units">${manualUnits}</div>
      </article>
    </section>
  </main>
</body>
</html>`;
}

function renderUnitList(units, emptyText) {
    if (!Array.isArray(units) || units.length === 0) {
        return `<span class="empty">${escapeHtml(emptyText)}</span>`;
    }

    return units.map((unit) => `<span class="unit">${escapeHtml(unit)}</span>`).join('');
}

function createSummaryCaption(summary, updatedAt) {
    const rfidUnits = Array.isArray(summary.rfidWarehouseIds) ? summary.rfidWarehouseIds : [];
    const manualUnits = Array.isArray(summary.manualWarehouseIds) ? summary.manualWarehouseIds : [];
    const lines = [
        `🚛 *RESUME DATA RFID FT - ${summary.district}*`,
        `🕒 Update: ${updatedAt}`,
        `📅 Periode data: ${summary.selectedDays} hari terakhir`,
        `🎯 RFID Success: *${formatPercentage(Number(summary.rfidSuccessPercentage) || 0)}%* (${summary.rfidWarehouseCount} dari ${summary.totalWarehouseCount} FT)`,
        `✅ FT *sudah RFID (${rfidUnits.length})*: ${formatUnitNames(rfidUnits)}`,
        `🟠 FT *Manual (${manualUnits.length})*: ${formatUnitNames(manualUnits)}`,
        '📝 Note : Data di atas adalah Instalasi FT berhasil berdasarkan 1 unit saja yang berhasil RFID bukan merupakan Ach RFID dari total transaksi!!!'
    ];

    return lines.join('\n\n');
}

function formatUnitNames(units) {
    if (units.length === 0) {
        return 'Tidak ada';
    }

    const names = units.join(', ');
    return names.length > 280 ? `${names.slice(0, 265)}… (lihat gambar)` : names;
}

function formatUpdatedAt(date) {
    const dateParts = new Intl.DateTimeFormat('en-GB', {
        timeZone: 'Asia/Jakarta',
        day: 'numeric',
        month: 'long',
        year: 'numeric'
    }).formatToParts(date);
    const day = dateParts.find((part) => part.type === 'day').value;
    const month = dateParts.find((part) => part.type === 'month').value.slice(0, 3);
    const year = dateParts.find((part) => part.type === 'year').value;
    const time = new Intl.DateTimeFormat('en-GB', {
        timeZone: 'Asia/Jakarta',
        hour: '2-digit',
        minute: '2-digit',
        hourCycle: 'h23'
    }).format(date).replace(':', '.');

    return `${day} ${month}'${year} (${time} WIB)`;
}

function escapeHtml(value) {
    return String(value)
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#39;');
}

function formatPercentage(value) {
    return new Intl.NumberFormat('id-ID', { maximumFractionDigits: 2 }).format(value);
}

module.exports = {
    createSummaryCaption,
    formatUpdatedAt,
    renderSummaryImage
};
