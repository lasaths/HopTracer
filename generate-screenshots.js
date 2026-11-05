const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

// Mock diff data with realistic changes
const mockDiffData = {
    nodes: [
        {
            id: "node-1",
            name: "Number Slider",
            nickname: "X Value",
            x: 100,
            y: 100,
            w: 120,
            h: 50,
            status: "same",
            riskScore: 0,
            riskReasons: [],
            properties: {},
            inputs: [],
            outputs: [
                { id: "out-1", name: "Number", nickname: "N", kind: "output", status: "same" }
            ]
        },
        {
            id: "node-2",
            name: "Addition",
            nickname: "Add",
            x: 300,
            y: 100,
            w: 120,
            h: 50,
            status: "modified",
            riskScore: 45,
            riskReasons: ["Parameter value changed"],
            properties: { Value: "10" },
            propertiesOld: { Value: "5" },
            inputs: [
                { id: "in-2-a", name: "A", nickname: "A", kind: "input", status: "same" },
                { id: "in-2-b", name: "B", nickname: "B", kind: "input", status: "same" }
            ],
            outputs: [
                { id: "out-2", name: "Result", nickname: "R", kind: "output", status: "same" }
            ]
        },
        {
            id: "node-3",
            name: "Python",
            nickname: "Process",
            x: 500,
            y: 100,
            w: 120,
            h: 50,
            status: "modified",
            riskScore: 75,
            riskReasons: ["Script content changed"],
            properties: {
                Code: "import rhinoscriptsyntax as rs\nprint('Updated code')"
            },
            propertiesOld: {
                Code: "import rhinoscriptsyntax as rs\nprint('Original code')"
            },
            inputs: [
                { id: "in-3", name: "x", nickname: "x", kind: "input", status: "same" }
            ],
            outputs: [
                { id: "out-3", name: "a", nickname: "a", kind: "output", status: "same" }
            ]
        },
        {
            id: "node-4",
            name: "Panel",
            nickname: "Output",
            x: 700,
            y: 100,
            w: 120,
            h: 50,
            status: "added",
            riskScore: 25,
            riskReasons: ["Component added"],
            properties: {},
            inputs: [
                { id: "in-4", name: "Content", nickname: "C", kind: "input", status: "added" }
            ],
            outputs: []
        },
        {
            id: "node-5",
            name: "Curve",
            nickname: "Old Curve",
            x: 300,
            y: 250,
            w: 120,
            h: 50,
            status: "removed",
            riskScore: 80,
            riskReasons: ["Component removed"],
            properties: {},
            inputs: [],
            outputs: [
                { id: "out-5", name: "Curve", nickname: "C", kind: "output", status: "removed" }
            ]
        }
    ],
    edges: [
        {
            source: "node-1",
            sourcePort: "out-1",
            target: "node-2",
            targetPort: "in-2-a",
            status: "same"
        },
        {
            source: "node-2",
            sourcePort: "out-2",
            target: "node-3",
            targetPort: "in-3",
            status: "same"
        },
        {
            source: "node-3",
            sourcePort: "out-3",
            target: "node-4",
            targetPort: "in-4",
            status: "added"
        }
    ],
    statistics: {
        totalNodes: 5,
        addedNodes: 1,
        removedNodes: 1,
        modifiedNodes: 2,
        unchangedNodes: 1,
        totalEdges: 3,
        addedEdges: 1,
        removedEdges: 0
    },
    riskSummary: {
        criticalCount: 0,
        highCount: 1,
        mediumCount: 2,
        lowCount: 1
    },
    topRisks: [
        { nodeId: "node-5", nodeName: "Old Curve", nodeStatus: "removed", riskScore: 80, reasons: ["Component removed"] },
        { nodeId: "node-3", nodeName: "Process", nodeStatus: "modified", riskScore: 75, reasons: ["Script content changed"] },
        { nodeId: "node-2", nodeName: "Add", nodeStatus: "modified", riskScore: 45, reasons: ["Parameter value changed"] }
    ],
    diagnostics: []
};

async function generateScreenshots() {
    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({
        viewport: { width: 1600, height: 1000 },
        deviceScaleFactor: 1
    });

    const outputDir = path.join(__dirname, 'artifacts', 'screenshots');
    if (!fs.existsSync(outputDir)) {
        fs.mkdirSync(outputDir, { recursive: true });
    }

    console.log('Generating screenshots at 1600x1000...\n');

    // 1. Start page (index.html)
    console.log('📸 Capturing index.html (start page)...');
    const indexPage = await context.newPage();
    await indexPage.goto(`file://${path.join(__dirname, 'Source/HopTracer.Web/wwwroot/index.html')}`);
    await indexPage.waitForTimeout(500);
    await indexPage.screenshot({ path: path.join(outputDir, '01-index-start-page.png') });
    await indexPage.close();
    console.log('   ✓ Saved: 01-index-start-page.png');

    // 2. Selection page
    console.log('📸 Capturing selection.html...');
    const selectionPage = await context.newPage();
    await selectionPage.goto(`file://${path.join(__dirname, 'Source/HopTracer.Web/wwwroot/selection.html')}`);
    await selectionPage.waitForTimeout(500);
    await selectionPage.screenshot({ path: path.join(outputDir, '02-selection-page.png') });
    await selectionPage.close();
    console.log('   ✓ Saved: 02-selection-page.png');

    // 3. Error page
    console.log('📸 Capturing error.html...');
    const errorPage = await context.newPage();
    await errorPage.goto(`file://${path.join(__dirname, 'Source/HopTracer.Web/wwwroot/error.html')}`);
    await errorPage.waitForTimeout(500);
    await errorPage.screenshot({ path: path.join(outputDir, '03-error-page.png') });
    await errorPage.close();
    console.log('   ✓ Saved: 03-error-page.png');

    // 4. Diff viewer with mock data (no selection)
    console.log('📸 Capturing diff_viewer.html (with mock data)...');
    const diffPage = await context.newPage();
    
    // Read the diff viewer HTML and inject mock data
    let diffHtml = fs.readFileSync(
        path.join(__dirname, 'Source/HopTracer.Web/wwwroot/diff_viewer.html'),
        'utf-8'
    );
    
    // Replace the placeholder with actual mock data
    diffHtml = diffHtml.replace(
        'return {json_data};',
        `return ${JSON.stringify(mockDiffData)};`
    );
    
    // Write temporary file
    const tempDiffPath = path.join(__dirname, 'temp-diff.html');
    fs.writeFileSync(tempDiffPath, diffHtml);
    
    await diffPage.goto(`file://${tempDiffPath}`);
    await diffPage.waitForTimeout(2000); // Wait for canvas rendering
    await diffPage.screenshot({ path: path.join(outputDir, '04-diff-viewer-overview.png'), fullPage: false });
    console.log('   ✓ Saved: 04-diff-viewer-overview.png');

    // 5. Diff viewer with node selected
    console.log('📸 Capturing diff_viewer.html (node selected)...');
    // Click on the Python node (if canvas is interactive)
    try {
        await diffPage.click('text=Process', { timeout: 1000 });
        await diffPage.waitForTimeout(500);
    } catch (e) {
        console.log('   ⚠ Could not click node (canvas may not be interactive in file:// context)');
    }
    await diffPage.screenshot({ path: path.join(outputDir, '05-diff-viewer-selected.png'), fullPage: false });
    console.log('   ✓ Saved: 05-diff-viewer-selected.png');

    await diffPage.close();
    
    // Cleanup
    fs.unlinkSync(tempDiffPath);

    await browser.close();
    
    console.log('\n✅ All screenshots generated successfully!');
    console.log(`📁 Output directory: ${outputDir}`);
}

generateScreenshots().catch(console.error);
