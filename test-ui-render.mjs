#!/usr/bin/env node
/**
 * UI rendering verification test for HopTracer
 * Tests the 5 remaining UI issues at 1440×900 and 1280×800, DPR 1 and 2
 */

import { chromium } from 'playwright';
import { createServer } from 'http';
import { createReadStream, readFileSync, existsSync } from 'fs';
import { join, extname, dirname, resolve } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const wwwroot = join(__dirname, 'Source/HopTracer.Web/wwwroot');
const uploadsDir = '/home/ubuntu/.cursor/projects/agent-repos-HopTracer/uploads';
const m30DataPath = join(uploadsDir, 'm30_b1a9.json');

const MIME_TYPES = {
  '.html': 'text/html',
  '.css': 'text/css',
  '.js': 'application/javascript',
  '.json': 'application/json',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.woff2': 'font/woff2',
};

// Simple static file server
function startServer(port = 8765) {
  const server = createServer((req, res) => {
    let filePath;
    
    // Serve test data
    if (req.url.includes('test-data.json')) {
      // Serve sample diff data for testing
      const testData = join(__dirname, 'Tests/data');
      res.setHeader('Content-Type', 'application/json');
      res.setHeader('Access-Control-Allow-Origin', '*');
      res.writeHead(200);
      res.end(JSON.stringify({ nodes: [], edges: [], groups: [] }));
      return;
    }
    
    // Serve m30 data from uploads
    if (req.url.includes('m30') || req.url.includes('b1a9')) {
      if (existsSync(m30DataPath)) {
        filePath = m30DataPath;
      }
    } else {
      filePath = join(wwwroot, req.url === '/' ? 'diff_viewer.html' : req.url);
    }
    
    const ext = extname(filePath);
    const contentType = MIME_TYPES[ext] || 'application/octet-stream';
    
    res.setHeader('Content-Type', contentType);
    res.setHeader('Access-Control-Allow-Origin', '*');
    
    if (!existsSync(filePath)) {
      res.writeHead(404);
      res.end('Not found');
      return;
    }
    
    createReadStream(filePath).pipe(res);
  });
  
  return new Promise((resolve) => {
    server.listen(port, () => {
      console.log(`Server running at http://localhost:${port}`);
      resolve({ server, port });
    });
  });
}

async function measureUI(url, viewport, deviceScaleFactor) {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport, deviceScaleFactor });
  const page = await context.newPage();
  
  console.log(`\n=== Testing ${viewport.width}×${viewport.height} @ DPR ${deviceScaleFactor} ===\n`);
  
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  
  // Wait for CSS to load
  await page.waitForTimeout(500);
  
  // Check if page loaded
  const loaded = await page.evaluate(() => {
    return {
      hasCSS: !!document.querySelector('link[href*="diff-viewer.css"]'),
      hasTokens: !!document.querySelector('link[href*="tokens.css"]'),
      bodyClass: document.body.className
    };
  });
  
  if (!loaded.hasCSS || !loaded.hasTokens) {
    console.warn('Warning: CSS may not be fully loaded', loaded);
  }
  
  const results = {
    viewport: `${viewport.width}×${viewport.height}`,
    dpr: deviceScaleFactor,
    measurements: {}
  };
  
  // 1. Cluster label visibility
  results.measurements.clusterLabel = await page.evaluate(() => {
    const clusterGlyphs = window.__clusterGlyphs || [];
    const clusterData = clusterGlyphs.find(g => /cluster/i.test(g.name));
    if (!clusterData) return { found: false, message: 'No cluster node found' };
    
    return {
      found: true,
      label: clusterData.label,
      fullName: clusterData.name,
      truncated: clusterData.label.includes('…'),
      zoom: clusterData.zoom,
      gap: clusterData.gap
    };
  });
  
  // 2. Canvas diff tags count
  results.measurements.canvasTags = await page.evaluate(() => {
    const tags = Array.from(document.querySelectorAll('.cv-tag'));
    const visible = tags.filter(t => !t.hidden && !t.classList.contains('is-hidden'));
    const hidden = tags.filter(t => t.hidden || t.classList.contains('is-hidden'));
    
    return {
      total: tags.length,
      visible: visible.length,
      hidden: hidden.length,
      hiddenNodes: hidden.map(t => ({
        id: t.dataset.nodeId,
        avoided: t.dataset.avoided
      }))
    };
  });
  
  // 3. Footer button overlap check
  results.measurements.footerOverlap = await page.evaluate(() => {
    const unchangedTab = document.querySelector('footer [data-filter="same"]');
    const checkBtn = document.getElementById('btn-check-baseline');
    
    if (!unchangedTab || !checkBtn) {
      return { error: 'Elements not found' };
    }
    
    const tabRect = unchangedTab.getBoundingClientRect();
    const btnRect = checkBtn.getBoundingClientRect();
    
    const overlaps = !(tabRect.right <= btnRect.left || 
                      btnRect.right <= tabRect.left ||
                      tabRect.bottom <= btnRect.top ||
                      btnRect.bottom <= tabRect.top);
    
    return {
      overlaps,
      tabRight: tabRect.right,
      btnLeft: btnRect.left,
      gap: btnRect.left - tabRect.right
    };
  });
  
  // 4. Inspector gap between .insp-head and first .insp-sec
  results.measurements.inspectorGap = await page.evaluate(() => {
    const head = document.querySelector('.insp-head');
    const firstSec = document.querySelector('.insp-head + .insp-sec, .insp-sec');
    
    if (!head || !firstSec) {
      return { error: 'Inspector elements not visible (summary mode?)' };
    }
    
    const headRect = head.getBoundingClientRect();
    const secRect = firstSec.getBoundingClientRect();
    const gap = secRect.top - headRect.bottom;
    
    return {
      gap,
      expected: 32,
      matches: Math.abs(gap - 32) < 2
    };
  });
  
  // 5. Small items
  results.measurements.smallItems = await page.evaluate(() => {
    const detailsTab = document.querySelector('.details-tab');
    const detailsTabs = document.querySelector('.details-tabs');
    const btnCollapse = document.getElementById('btn-details-collapse');
    const zoomButtons = [
      document.getElementById('btn-fit'),
      document.getElementById('btn-zoom-in'),
      document.getElementById('btn-zoom-out'),
      document.getElementById('btn-minimap')
    ];
    
    const checks = {};
    
    // Details tab alignment
    if (detailsTab && detailsTabs) {
      const tabRect = detailsTab.getBoundingClientRect();
      const barRect = detailsTabs.getBoundingClientRect();
      checks.detailsTabAlignment = {
        tabBottom: tabRect.bottom,
        barBottom: barRect.bottom,
        offset: barRect.bottom - tabRect.bottom,
        snapped: Math.abs(barRect.bottom - tabRect.bottom) < 1
      };
    }
    
    // Collapse button position
    if (btnCollapse) {
      const rect = btnCollapse.getBoundingClientRect();
      checks.collapseButtonPos = {
        left: rect.left,
        top: rect.top,
        width: rect.width,
        height: rect.height
      };
    }
    
    // Zoom button icons
    checks.zoomButtonIcons = zoomButtons.map(btn => {
      if (!btn) return { found: false };
      const icon = btn.querySelector('.ico use');
      return {
        id: btn.id,
        hasIcon: !!icon,
        href: icon?.getAttribute('href') || 'none',
        visible: btn.offsetWidth > 0 && btn.offsetHeight > 0
      };
    });
    
    // Risk rows spacing
    const riskRows = Array.from(document.querySelectorAll('.risklist button'));
    if (riskRows.length > 1) {
      const first = riskRows[0].getBoundingClientRect();
      const second = riskRows[1].getBoundingClientRect();
      checks.riskRowsSpacing = {
        gap: second.top - first.bottom,
        overlaps: second.top < first.bottom
      };
    }
    
    return checks;
  });
  
  await browser.close();
  return results;
}

async function main() {
  console.log('Starting HopTracer UI rendering tests...\n');
  
  const { server, port } = await startServer();
  const url = `http://localhost:${port}/diff_viewer.html`;
  
  try {
    const testConfigs = [
      { viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 },
      { viewport: { width: 1440, height: 900 }, deviceScaleFactor: 2 },
      { viewport: { width: 1280, height: 800 }, deviceScaleFactor: 1 },
      { viewport: { width: 1280, height: 800 }, deviceScaleFactor: 2 },
    ];
    
    const allResults = [];
    
    for (const config of testConfigs) {
      const results = await measureUI(url, config.viewport, config.deviceScaleFactor);
      allResults.push(results);
      console.log(JSON.stringify(results, null, 2));
    }
    
    console.log('\n=== Summary Report ===\n');
    
    for (const result of allResults) {
      console.log(`\n${result.viewport} @ DPR ${result.dpr}:`);
      
      // 1. Cluster label
      const cluster = result.measurements.clusterLabel;
      if (cluster.found) {
        console.log(`  ✓ Cluster label: "${cluster.label}" (full: "${cluster.fullName}")`);
        if (cluster.truncated) {
          console.log(`    ⚠ Label is truncated with '…'`);
        } else {
          console.log(`    ✓ Full label visible without truncation`);
        }
      }
      
      // 2. Canvas tags
      const tags = result.measurements.canvasTags;
      console.log(`  ✓ Canvas tags: ${tags.visible}/${tags.total} visible`);
      if (tags.hidden > 0) {
        console.log(`    ⚠ ${tags.hidden} tags hidden`);
      }
      
      // 3. Footer overlap
      const footer = result.measurements.footerOverlap;
      if (footer.overlaps) {
        console.log(`  ✗ Footer: Check Baseline overlaps Unchanged tab!`);
      } else {
        console.log(`  ✓ Footer: ${footer.gap}px gap between Unchanged tab and Check Baseline`);
      }
      
      // 4. Inspector gap
      const insp = result.measurements.inspectorGap;
      if (insp.error) {
        console.log(`  ℹ Inspector: ${insp.error}`);
      } else if (insp.matches) {
        console.log(`  ✓ Inspector: ${insp.gap}px gap matches expected 32px`);
      } else {
        console.log(`  ⚠ Inspector: ${insp.gap}px gap (expected 32px)`);
      }
      
      // 5. Small items
      const small = result.measurements.smallItems;
      if (small.detailsTabAlignment) {
        const align = small.detailsTabAlignment;
        if (align.snapped) {
          console.log(`  ✓ Details tab: aligned to bar`);
        } else {
          console.log(`  ⚠ Details tab: ${align.offset}px offset from bar`);
        }
      }
      
      if (small.zoomButtonIcons) {
        const missing = small.zoomButtonIcons.filter(b => b.found !== false && !b.hasIcon);
        if (missing.length === 0) {
          console.log(`  ✓ Zoom buttons: all icons present`);
        } else {
          console.log(`  ✗ Zoom buttons: ${missing.length} missing icons`);
        }
      }
    }
    
  } finally {
    server.close();
    console.log('\nServer closed.');
  }
}

main().catch(console.error);
