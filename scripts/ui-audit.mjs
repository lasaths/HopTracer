#!/usr/bin/env node
/**
 * HopTracer UI interaction screenshot audit.
 *
 * Serves/drives HopTracer.Web against a real example .gh (default: LCRL_SCREW_Axis.gh),
 * captures every planned interaction state into artifacts/ui-audit/, and writes INDEX.md.
 *
 * Usage:
 *   npm run ui-audit
 *   node scripts/ui-audit.mjs
 *   EXAMPLE_GH="C:\\path\\to\\file.gh" npm run ui-audit
 */
import { chromium } from 'playwright';
import { createServer } from 'http';
import { spawn } from 'child_process';
import {
  createReadStream,
  existsSync,
  mkdirSync,
  readFileSync,
  writeFileSync,
  copyFileSync,
  unlinkSync,
} from 'fs';
import { join, dirname, extname, basename, resolve } from 'path';
import { fileURLToPath } from 'url';
import { createRequire } from 'module';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = resolve(__dirname, '..');
const wwwroot = join(root, 'Source/HopTracer.Web/wwwroot');
const outDir = join(root, 'artifacts/ui-audit');
const fixtureDir = join(root, 'artifacts/ui-audit-fixtures');

const EXAMPLE_GH =
  process.env.EXAMPLE_GH ||
  'C:\\Users\\lasaths\\GitHub\\LCRL_Segmented_Timber_Shell_Roof\\01_Model\\00_Export\\LCRL_SCREW_Axis.gh';

const WEB_PORT = Number(process.env.UI_AUDIT_PORT || 5055);
const WEB_URL = `http://127.0.0.1:${WEB_PORT}`;

const MIME = {
  '.html': 'text/html',
  '.css': 'text/css',
  '.js': 'application/javascript',
  '.json': 'application/json',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.woff2': 'font/woff2',
  '.ico': 'image/x-icon',
};

const shots = [];

function record(id, screen, trigger, expected, note = '') {
  shots.push({ id, screen, trigger, expected, note, issue: '' });
}

function startStaticServer(port) {
  const server = createServer((req, res) => {
    const urlPath = (req.url || '/').split('?')[0];
    let filePath = join(wwwroot, urlPath === '/' ? 'index.html' : decodeURIComponent(urlPath));
    if (!filePath.startsWith(wwwroot) || !existsSync(filePath)) {
      res.writeHead(404);
      res.end('Not found');
      return;
    }
    res.setHeader('Content-Type', MIME[extname(filePath)] || 'application/octet-stream');
    createReadStream(filePath).pipe(res);
  });
  return new Promise((resolvePromise) => {
    server.listen(port, '127.0.0.1', () => resolvePromise(server));
  });
}

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

async function waitForUrl(url, attempts = 60) {
  for (let i = 0; i < attempts; i++) {
    try {
      const res = await fetch(url);
      if (res.ok || res.status === 302 || res.status === 401) return;
    } catch {
      /* retry */
    }
    await sleep(500);
  }
  throw new Error(`Timed out waiting for ${url}`);
}

function startWebHost() {
  const proj = join(root, 'Source/HopTracer.Web/HopTracer.Web.csproj');
  const child = spawn(
    'dotnet',
    ['run', '--project', proj, '-c', 'Release', '--no-build', '--urls', WEB_URL],
    {
      cwd: root,
      env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development' },
      stdio: ['ignore', 'pipe', 'pipe'],
      windowsHide: true,
    }
  );
  let log = '';
  child.stdout.on('data', (d) => {
    log += d.toString();
  });
  child.stderr.on('data', (d) => {
    log += d.toString();
  });
  child.on('exit', (code) => {
    if (code && code !== 0) {
      console.error('HopTracer.Web exited:', code, log.slice(-2000));
    }
  });
  return { child, getLog: () => log };
}

async function ensureWebBuilt() {
  await new Promise((resolvePromise, reject) => {
    const build = spawn(
      'dotnet',
      ['build', join(root, 'Source/HopTracer.Web/HopTracer.Web.csproj'), '-c', 'Release', '-v', 'q'],
      { cwd: root, stdio: 'inherit', windowsHide: true }
    );
    build.on('exit', (code) => (code === 0 ? resolvePromise() : reject(new Error(`build failed ${code}`))));
  });
}

function prepareFixtures() {
  mkdirSync(fixtureDir, { recursive: true });
  if (!existsSync(EXAMPLE_GH)) {
    throw new Error(`EXAMPLE_GH not found: ${EXAMPLE_GH}`);
  }
  const live = join(fixtureDir, 'LCRL_SCREW_Axis-live.gh');
  copyFileSync(EXAMPLE_GH, live);

  // Prefer pre-exported old/new if present; else use live for both (same-file empty-ish compare).
  const oldPath = join(fixtureDir, 'LCRL_SCREW_Axis-old.gh');
  const newPath = join(fixtureDir, 'LCRL_SCREW_Axis-new.gh');
  if (!existsSync(oldPath)) copyFileSync(live, oldPath);
  if (!existsSync(newPath)) copyFileSync(live, newPath);

  return { live, oldPath, newPath, exampleName: basename(EXAMPLE_GH) };
}

async function shot(page, id, screen, trigger, expected, note = '') {
  const file = `${id}.png`;
  await page.screenshot({ path: join(outDir, file), fullPage: false });
  record(id, screen, trigger, expected, note);
  console.log(`  ✓ ${file}`);
}

async function getSessionToken() {
  const res = await fetch(`${WEB_URL}/system/session_token`);
  if (!res.ok) throw new Error(`session_token ${res.status}`);
  const data = await res.json();
  return data.token || data.Token || data.sessionToken;
}

async function postCompare(token, pathOld, pathNew) {
  const form = new FormData();
  form.append('_session_token', token);
  form.append('path_old', pathOld);
  form.append('path_new', pathNew);
  const res = await fetch(`${WEB_URL}/compare`, { method: 'POST', body: form });
  const html = await res.text();
  if (!res.ok) {
    throw new Error(`/compare failed ${res.status}: ${html.slice(0, 400)}`);
  }
  return html;
}

function writeIndex(fixtures) {
  const lines = [
    '# HopTracer UI interaction screenshot map',
    '',
    `Generated: ${new Date().toISOString()}`,
    '',
    `Example file: \`${EXAMPLE_GH}\``,
    '',
    `Fixtures: \`${fixtures.oldPath}\` (old) → \`${fixtures.newPath}\` (new); live copy \`${fixtures.live}\``,
    '',
    '| ID | Screen | Trigger | Expected | Issue |',
    '|----|--------|---------|----------|-------|',
  ];
  for (const s of shots) {
    lines.push(
      `| ${s.id} | ${s.screen} | ${s.trigger.replace(/\|/g, '\\|')} | ${s.expected.replace(/\|/g, '\\|')} | ${s.note || s.issue || ''} |`
    );
  }
  lines.push(
    '',
    '## Manual checklist (native / MAUI — capture separately)',
    '',
    '- [ ] MissingDependencyPage (GH_IO missing)',
    '- [ ] Update Available alert',
    '- [ ] Baseline Save prompt/alert',
    '- [ ] Baseline Check prompt/alert',
    '- [ ] Native file picker dialog',
    '',
    '## Notes',
    '',
    '- `#btn-history` is present in diff_viewer.html but has no handler in diff-viewer.js (dead control).',
    '- `selection.html` is legacy and not linked from the live start page.',
    ''
  );
  writeFileSync(join(outDir, 'INDEX.md'), lines.join('\n'), 'utf8');
}

async function captureStaticPages(browser, fixtures) {
  const staticPort = WEB_PORT + 1;
  const server = await startStaticServer(staticPort);
  const base = `http://127.0.0.1:${staticPort}`;
  const context = await browser.newContext({
    viewport: { width: 1600, height: 1000 },
    deviceScaleFactor: 1,
  });
  const page = await context.newPage();

  console.log('\n[static] index / error / selection');
  await page.goto(`${base}/index.html`, { waitUntil: 'networkidle' });
  await shot(page, '01-index-empty', 'index', 'Initial load', 'Empty drop zones, Compare disabled');

  await page.evaluate((p) => {
    const name = p.split(/[\\/]/).pop();
    document.getElementById('pathOld').value = p;
    document.getElementById('nameOld').textContent = name;
    document.getElementById('dropOld').style.borderColor = 'var(--accent)';
  }, fixtures.live);
  await shot(page, '02-index-old-selected', 'index', `Set pathOld=${fixtures.exampleName}`, 'OLD zone filled');

  await page.evaluate((p) => {
    const name = p.split(/[\\/]/).pop();
    document.getElementById('pathNew').value = p;
    document.getElementById('nameNew').textContent = name;
    document.getElementById('dropNew').style.borderColor = 'var(--accent)';
    const btn = document.getElementById('compareBtn');
    if (btn) btn.disabled = false;
  }, fixtures.newPath);
  await shot(page, '03-index-both-ready', 'index', 'Set pathNew + enable Compare', 'Both zones ready, Compare enabled');

  await page.locator('#dropOld').evaluate((el) => el.classList.add('dragover'));
  await shot(page, '04-index-dragover', 'index', 'Force dragover class on OLD', 'Drag-over styling');
  await page.locator('#dropOld').evaluate((el) => el.classList.remove('dragover'));

  await page.evaluate(() => {
    const ov = document.getElementById('loadingOverlay');
    if (ov) {
      ov.style.display = 'flex';
      ov.hidden = false;
    }
    const t = document.getElementById('loadingTitle');
    if (t) t.textContent = 'Converting & Comparing...';
    const p = document.getElementById('loadingPercent');
    if (p) p.textContent = '42%';
  });
  await shot(page, '05-index-loading-overlay', 'index', 'Show loading overlay', 'Compare progress overlay');

  await page.goto(`${base}/error.html`.replace('error.html', 'error.html'), { waitUntil: 'domcontentloaded' });
  // error.html uses placeholder; inject short/long
  await page.evaluate(() => {
    const el = document.querySelector('.error-message') || document.body;
    if (el) el.textContent = 'Failed to open file.';
  });
  // Serve error with replace via evaluate on raw file content page
  await page.setContent(
    readFileSync(join(wwwroot, 'error.html'), 'utf8').replace(
      '{error_message}',
      'Failed to open file.'
    ),
    { waitUntil: 'domcontentloaded' }
  );
  // Fix relative assets when using setContent
  await page.addStyleTag({ url: `${base}/css/tokens.css` }).catch(() => {});
  await shot(page, '06-error-short', 'error', 'Short error message', 'Compact error page');

  await page.setContent(
    readFileSync(join(wwwroot, 'error.html'), 'utf8').replace(
      '{error_message}',
      `Could not convert ${EXAMPLE_GH} — path validation failed after staging a very long diagnostic string for layout wrapping tests.`.repeat(2)
    ),
    { waitUntil: 'domcontentloaded' }
  );
  await shot(page, '07-error-long', 'error', 'Long error message', 'Word-break / wrap on long path');

  await page.goto(`${base}/selection.html`, { waitUntil: 'networkidle' });
  await shot(page, '08-selection-legacy', 'selection', 'Open selection.html', 'Legacy start page (not linked from index)');

  await context.close();
  server.close();
}

async function captureLiveApp(browser, fixtures) {
  console.log('\n[live] HopTracer.Web + real compare');
  const token = await getSessionToken();
  console.log('  session ok');

  let compareHtml;
  try {
    compareHtml = await postCompare(token, fixtures.oldPath, fixtures.newPath);
    console.log(`  /compare ok (${compareHtml.length} bytes)`);
  } catch (err) {
    console.warn('  /compare with fixtures failed:', err.message);
    console.warn('  Falling back to mock cluster fixture injection');
    compareHtml = null;
  }

  const context = await browser.newContext({
    viewport: { width: 1600, height: 1000 },
    deviceScaleFactor: 1,
  });
  const page = await context.newPage();

  // Index against live server with real example path
  await page.goto(`${WEB_URL}/index.html`, { waitUntil: 'networkidle' });
  await page.evaluate((p) => {
    const name = p.split(/[\\/]/).pop();
    document.getElementById('pathOld').value = p;
    document.getElementById('nameOld').textContent = name;
  }, fixtures.live);
  await shot(page, '09-index-live-example-path', 'index', `pathOld=${EXAMPLE_GH}`, 'Start page with real example path');

  // Git modal states via API stubs when possible
  const gitBtn = page.locator('.git-btn');
  if (await gitBtn.count()) {
    await gitBtn.first().click();
    await sleep(400);
    await shot(page, '10-git-modal-open', 'index/git', 'Click git button', 'Git modal open');
  } else {
    await page.evaluate(() => {
      const m = document.getElementById('gitModal');
      if (m) {
        m.style.display = 'flex';
        m.hidden = false;
      }
    });
    await shot(page, '10-git-modal-open', 'index/git', 'Force-show git modal', 'Git modal open (forced)');
  }

  // Load commits for the live example if in a git repo
  try {
    await page.evaluate(async (pathVal) => {
      const list = document.getElementById('commitList');
      if (list) list.innerHTML = '<div style="padding:20px">Loading commits…</div>';
      const res = await fetch('/git/commits', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-HopTracer-Session': window.__auditToken || '' },
        body: JSON.stringify({ path: pathVal }),
      });
      return res.status;
    }, fixtures.live);
  } catch {
    /* ignore */
  }

  // Inject token into page for subsequent fetches
  await page.evaluate((t) => {
    window.__auditToken = t;
  }, token);

  await page.evaluate(async (pathVal) => {
    const list = document.getElementById('commitList');
    const status = document.getElementById('gitStatusText') || document.getElementById('gitStatus');
    try {
      const res = await fetch('/git/commits', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-HopTracer-Session': window.__auditToken,
        },
        body: JSON.stringify({ path: pathVal }),
      });
      const data = await res.json();
      if (!res.ok || data.error) {
        if (list) {
          list.innerHTML = `<div style="padding:40px;color:#ff6b6b">Error: ${data.error || res.status}</div>`;
        }
        if (status) status.textContent = data.error || 'Failed';
        return;
      }
      const commits = data.commits || [];
      if (!commits.length) {
        if (list) list.innerHTML = '<div style="padding:40px">No commits found</div>';
        return;
      }
      if (list) {
        list.innerHTML = commits
          .slice(0, 12)
          .map(
            (c) =>
              `<div class="commit-row" style="padding:10px;border-bottom:1px solid #333;cursor:pointer"><div style="font-weight:600">${c.message || ''}</div><div style="font-size:11px;opacity:.7">${c.hash} · ${c.author} · ${c.date}</div></div>`
          )
          .join('');
      }
      if (status) status.textContent = `Found ${commits.length} commits`;
    } catch (e) {
      if (list) list.innerHTML = `<div style="padding:40px;color:#ff6b6b">${e.message}</div>`;
    }
  }, fixtures.live);
  await sleep(500);
  await shot(page, '11-git-modal-commits', 'index/git', 'POST /git/commits for example .gh', 'Commit list for LCRL_SCREW_Axis.gh');

  await page.evaluate(() => {
    const list = document.getElementById('commitList');
    if (list) list.innerHTML = '<div style="padding:40px;color:#ff6b6b"><strong>Error</strong><br>Simulated fetch failure</div>';
  });
  await shot(page, '12-git-modal-error', 'index/git', 'Inject error state', 'Git modal error');

  await page.evaluate(() => {
    const list = document.getElementById('commitList');
    if (list) list.innerHTML = '<div style="padding:40px">No commits found for this file.</div>';
  });
  await shot(page, '13-git-modal-empty', 'index/git', 'Inject empty state', 'Git modal empty');

  // Diff viewer: fulfill saved compare HTML (form re-POST blanks in headless on large payloads)
  let diffHtml = null;
  let usedRealCompare = false;
  if (compareHtml && compareHtml.includes('diff-viewer') && compareHtml.includes('{') ) {
    writeFileSync(join(outDir, '_last-compare.html'), compareHtml, 'utf8');
    diffHtml = compareHtml;
    usedRealCompare = true;
  } else {
    const mock = JSON.parse(
      readFileSync(join(root, 'Tests/data/cluster-interaction/mock_cluster_diff.json'), 'utf8')
    );
    diffHtml = readFileSync(join(wwwroot, 'diff_viewer.html'), 'utf8').replace(
      '{json_data}',
      JSON.stringify(mock)
    );
    writeFileSync(join(outDir, '_mock-diff.html'), diffHtml, 'utf8');
  }

  const consoleErrors = [];
  page.on('pageerror', (err) => consoleErrors.push(String(err)));
  page.on('console', (msg) => {
    if (msg.type() === 'error') consoleErrors.push(msg.text());
  });

  await page.unroute('**/__audit_diff.html').catch(() => {});
  await page.route('**/__audit_diff.html', async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'text/html; charset=utf-8',
      body: Buffer.from(diffHtml, 'utf8'),
    });
  });
  await page.goto(`${WEB_URL}/__audit_diff.html`, { waitUntil: 'domcontentloaded', timeout: 120000 });
  // Wait for viewer chrome; large LCRL diffs can take a while to hydrate
  await page.locator('#change-list, #graph-canvas, .top').first().waitFor({ state: 'attached', timeout: 60000 }).catch(() => {});
  await page.locator('#change-list .change-item, #change-list [data-id], #change-list button').first()
    .waitFor({ state: 'visible', timeout: 90000 })
    .catch(() => {});
  await sleep(usedRealCompare ? 4000 : 1500);
  if (consoleErrors.length) {
    console.log('  page errors:', consoleErrors.slice(0, 5).join(' | '));
  }
  const bodyText = await page.locator('body').innerText().catch(() => '');
  if (!bodyText.trim()) {
    console.log('  WARN: blank body after diff load; falling back to cluster mock');
    const mock = JSON.parse(
      readFileSync(join(root, 'Tests/data/cluster-interaction/mock_cluster_diff.json'), 'utf8')
    );
    diffHtml = readFileSync(join(wwwroot, 'diff_viewer.html'), 'utf8').replace(
      '{json_data}',
      JSON.stringify(mock)
    );
    await page.unroute('**/__audit_diff.html').catch(() => {});
    await page.route('**/__audit_diff.html', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'text/html; charset=utf-8',
        body: Buffer.from(diffHtml, 'utf8'),
      });
    });
    await page.goto(`${WEB_URL}/__audit_diff.html`, { waitUntil: 'networkidle' });
    await sleep(2000);
    usedRealCompare = false;
  }

  await shot(
    page,
    '14-diff-overview',
    'diff',
    usedRealCompare ? 'Fulfilled real /compare HTML (LCRL fixtures)' : 'Cluster mock fulfilled',
    'Diff overview with graph + queue'
  );

  // Empty-ish: filter to see empty queue messaging if possible
  const filterBtns = page.locator('[data-filter]');
  const filterCount = await filterBtns.count();
  for (let i = 0; i < filterCount; i++) {
    const btn = filterBtns.nth(i);
    const key = await btn.getAttribute('data-filter');
    await btn.click();
    await sleep(300);
    await shot(page, `15-filter-${key}`, 'diff/footer', `Click filter ${key}`, `Footer filter: ${key}`);
  }

  // Queue sort menu
  const sortBtn = page.locator('#queue-sort-btn');
  if (await sortBtn.count()) {
    await sortBtn.click();
    await sleep(200);
    await shot(page, '16-queue-sort-open', 'diff/queue', 'Open sort menu', 'Sort menu open');
    const options = page.locator('#queue-sort-menu button');
    const n = await options.count();
    for (let i = 0; i < n; i++) {
      const label = (await options.nth(i).innerText()).trim().replace(/\s+/g, '-').toLowerCase();
      await options.nth(i).click();
      await sleep(250);
      await shot(page, `17-queue-sort-${label}`, 'diff/queue', `Select sort ${label}`, `Queue sorted by ${label}`);
      if (i < n - 1) await sortBtn.click();
    }
  }

  // Select first change row
  const row = page.locator('#change-list [data-id], #change-list .change-row, #change-list button, #change-list > div').first();
  if (await row.count()) {
    await row.click({ timeout: 2000 }).catch(() => {});
    await sleep(400);
    await shot(page, '18-queue-row-selected', 'diff/queue', 'Click first change row', 'Node selected + inspector');
  }

  // Alarm band if visible
  const band = page.locator('#alarm-band');
  if (await band.isVisible().catch(() => false)) {
    await shot(page, '19-alarm-band', 'diff/alarm', 'High-risk band visible', 'Alarm band shown');
    const inspect = page.locator('#btn-band-inspect');
    if (await inspect.isVisible().catch(() => false)) {
      await inspect.click();
      await sleep(400);
      await shot(page, '20-alarm-inspect', 'diff/alarm', 'Click Inspect', 'Alarm inspect action');
    }
  } else {
    record('19-alarm-band', 'diff/alarm', 'n/a', 'Band hidden for this fixture', 'skipped');
  }

  // Spotlight — open via launcher (more reliable than Ctrl+K in headless)
  const launcher = page.locator('#search-launcher');
  if (await launcher.count()) {
    await launcher.click();
  } else {
    await page.keyboard.press('Control+K');
  }
  await page.locator('#spotlight-backdrop.open, #spotlight-backdrop[aria-hidden="false"]').waitFor({
    state: 'visible',
    timeout: 5000,
  }).catch(() => {});
  await sleep(300);
  await shot(page, '21-spotlight-empty', 'diff/spotlight', 'Open spotlight', 'Spotlight empty state');
  const spotlightInput = page.locator('#spotlight-input');
  if (await spotlightInput.isVisible().catch(() => false)) {
    await spotlightInput.fill('a', { timeout: 5000 }).catch(async () => {
      await spotlightInput.click({ force: true });
      await page.keyboard.type('a');
    });
    await sleep(400);
    await shot(page, '22-spotlight-results', 'diff/spotlight', 'Type search query', 'Spotlight results or no matches');
  } else {
    record('22-spotlight-results', 'diff/spotlight', 'n/a', 'Spotlight input not visible', 'skipped');
  }
  await page.keyboard.press('Escape');
  await sleep(200);
  await shot(page, '23-spotlight-closed', 'diff/spotlight', 'Esc', 'Spotlight closed');

  // Source modal via breadcrumb
  try {
    const crumb = page.locator('#meta-file-old');
    if (await crumb.count()) {
      await crumb.click();
      await sleep(400);
      await shot(page, '24-source-modal', 'diff/source', 'Click old breadcrumb', 'Change Source modal');
      const commitMode = page.locator('#source-mode-commit');
      if (await commitMode.count()) {
        await page.evaluate(() => {
          const btn = document.getElementById('source-mode-commit');
          if (btn) btn.click();
        });
        await sleep(300);
        await shot(page, '25-source-modal-commit', 'diff/source', 'Click #source-mode-commit via DOM', 'Source modal commit mode');
      } else {
        record('25-source-modal-commit', 'diff/source', 'n/a', 'Git Commit mode control missing', 'skipped');
      }
      await page.keyboard.press('Escape');
      await page.locator('#source-modal-cancel, #btn-source-cancel, button:has-text("Cancel")').first().click({ timeout: 1000 }).catch(() => {});
      await sleep(200);
    }
  } catch (err) {
    record('25-source-modal-commit', 'diff/source', String(err.message || err).slice(0, 120), 'Source modal commit mode', 'failed');
  }

  // Script tab / modal
  try {
    const scriptTab = page.locator('#details-tab-script');
    if (await scriptTab.isVisible().catch(() => false)) {
      await scriptTab.click();
      await sleep(300);
      await shot(page, '26-inspector-script-tab', 'diff/inspector', 'Open Script tab', 'Script inspector pane');
      const expand = page.locator('[data-script-expand], #data-script-expand, button:has-text("Expand")');
      if (await expand.count()) {
        await expand.first().click();
        await sleep(400);
        await shot(page, '27-script-modal', 'diff/script', 'Expand script', 'Fullscreen script modal');
        await page.keyboard.press('Escape');
      }
    } else {
      record('26-inspector-script-tab', 'diff/inspector', 'n/a', 'No script tab for selection', 'skipped');
    }
  } catch (err) {
    record('26-inspector-script-tab', 'diff/inspector', 'error', String(err.message || err), 'failed');
  }

  // Canvas controls
  for (const [id, sel] of [
    ['28-zoom-fit', '#btn-fit'],
    ['29-zoom-in', '#btn-zoom-in'],
    ['30-minimap-toggle', '#btn-minimap'],
  ]) {
    try {
      const el = page.locator(sel);
      if (await el.count()) {
        await el.click({ timeout: 3000 });
        await sleep(250);
        await shot(page, id, 'diff/canvas', `Click ${sel}`, `Canvas control ${sel}`);
      }
    } catch (err) {
      record(id, 'diff/canvas', 'error', String(err.message || err), 'failed');
    }
  }

  // Sliders — set to extremes
  for (const [id, sel, value] of [
    ['31-wire-slider-max', '#wire-slider', '100'],
    ['32-wire-slider-min', '#wire-slider', '0'],
    ['33-version-slider-old', '#version-slider', '0'],
    ['34-version-slider-new', '#version-slider', '2'],
    ['35-tag-slider-off', '#tag-slider', '2'],
    ['36-tag-slider-all', '#tag-slider', '0'],
  ]) {
    try {
      const el = page.locator(sel);
      if (await el.count()) {
        await el.evaluate((node, v) => {
          node.value = v;
          node.dispatchEvent(new Event('input', { bubbles: true }));
          node.dispatchEvent(new Event('change', { bubbles: true }));
        }, value);
        await sleep(250);
        await shot(page, id, 'diff/canvas', `Set ${sel}=${value}`, `Slider ${sel}`);
      }
    } catch (err) {
      record(id, 'diff/canvas', 'error', String(err.message || err), 'failed');
    }
  }

  // Hover tooltip on canvas center
  try {
    const canvas = page.locator('#graph-canvas');
    if (await canvas.count()) {
      const box = await canvas.boundingBox();
      if (box) {
        await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
        await sleep(300);
        await shot(page, '37-canvas-hover', 'diff/canvas', 'Mouse over canvas center', 'Hover tooltip if node under cursor');
      }
    }
  } catch (err) {
    record('37-canvas-hover', 'diff/canvas', 'error', String(err.message || err), 'failed');
  }

  // Graph tabs
  try {
    const tabs = page.locator('#graph-tabs .graph-tab-btn, #graph-tabs button');
    const tabCount = await tabs.count();
    await shot(page, '38-graph-tabs', 'diff/tabs', 'Tab bar state', `Graph tabs count=${tabCount}`);
    if (tabCount > 1) {
      await tabs.nth(1).click();
      await sleep(400);
      await shot(page, '39-graph-tab-secondary', 'diff/tabs', 'Click second graph tab', 'Cluster/secondary graph tab');
    }
  } catch (err) {
    record('38-graph-tabs', 'diff/tabs', 'error', String(err.message || err), 'failed');
  }

  // History dead control
  try {
    const hist = page.locator('#btn-history');
    if (await hist.count()) {
      await hist.click();
      await sleep(200);
      await shot(page, '40-btn-history-dead', 'diff/top', 'Click #btn-history', 'Dead control — no handler', 'DEAD: no JS handler');
    }
  } catch (err) {
    record('40-btn-history-dead', 'diff/top', 'error', String(err.message || err), 'failed');
  }

  // Loading overlay on diff
  await page.evaluate(() => {
    const ov = document.getElementById('loadingOverlay');
    if (ov) {
      ov.hidden = false;
      ov.style.display = 'flex';
    }
  });
  await shot(page, '41-diff-loading', 'diff', 'Show loading overlay', 'Diff loading overlay');
  await page.evaluate(() => {
    const ov = document.getElementById('loadingOverlay');
    if (ov) {
      ov.hidden = true;
      ov.style.display = 'none';
    }
  });

  // Toast
  await page.evaluate(() => {
    if (typeof showPerf === 'function') showPerf('Audit toast sample');
    else {
      const t = document.getElementById('viewer-toast');
      const m = document.getElementById('viewer-toast-msg');
      if (m) m.textContent = 'Audit toast sample';
      if (t) t.hidden = false;
    }
  });
  await sleep(200);
  await shot(page, '42-viewer-toast', 'diff/toast', 'showPerf / force toast', 'Viewer toast visible');

  // Narrow viewport
  await page.setViewportSize({ width: 1280, height: 800 });
  await sleep(400);
  await shot(page, '43-narrow-1280', 'diff/layout', 'Resize 1280×800', 'Narrow layout');
  await page.setViewportSize({ width: 900, height: 800 });
  await sleep(400);
  await shot(page, '44-narrow-900', 'diff/layout', 'Resize 900×800', 'Very narrow / collapsed panels');

  // Cluster mock dedicated page for cluster interactions
  try {
    const mock = JSON.parse(
      readFileSync(join(root, 'Tests/data/cluster-interaction/mock_cluster_diff.json'), 'utf8')
    );
    let clusterHtml = readFileSync(join(wwwroot, 'diff_viewer.html'), 'utf8');
    clusterHtml = clusterHtml.replace('{json_data}', JSON.stringify(mock));
    await page.setViewportSize({ width: 1600, height: 1000 });
    await page.unroute('**/diff_viewer.html').catch(() => {});
    await page.route('**/diff_viewer.html', async (route) => {
      await route.fulfill({ status: 200, contentType: 'text/html', body: clusterHtml });
    });
    await page.goto(`${WEB_URL}/diff_viewer.html`, { waitUntil: 'networkidle' });
    await sleep(2000);
    await shot(page, '45-cluster-fixture-overview', 'diff/cluster', 'Inject cluster mock fixture', 'Cluster interaction fixture overview');
    await page.keyboard.press('Enter');
    await sleep(500);
    await shot(page, '46-cluster-after-enter', 'diff/cluster', 'Press Enter on selection', 'Possible cluster tab open');
  } catch (err) {
    record('45-cluster-fixture-overview', 'diff/cluster', 'error', String(err.message || err), 'failed');
  }

  await context.close();
}

async function main() {
  mkdirSync(outDir, { recursive: true });
  const fixtures = prepareFixtures();
  console.log('Example:', EXAMPLE_GH);
  console.log('Output:', outDir);

  await ensureWebBuilt();
  const host = startWebHost();
  try {
    await waitForUrl(`${WEB_URL}/health`);
  } catch (e) {
    // health may not exist before middleware; try index
    await waitForUrl(`${WEB_URL}/index.html`);
  }
  console.log('Web host ready at', WEB_URL);

  const require = createRequire(import.meta.url);
  // ensure playwright resolves
  require.resolve('playwright');

  const browser = await chromium.launch({ headless: true });
  try {
    await captureStaticPages(browser, fixtures);
    await captureLiveApp(browser, fixtures);
  } finally {
    await browser.close();
    host.child.kill();
  }

  writeIndex(fixtures);
  console.log(`\nDone. ${shots.length} catalog entries → ${join(outDir, 'INDEX.md')}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
