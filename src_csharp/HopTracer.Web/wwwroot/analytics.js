// Analytics Module for HopTracer
// This provides analytics calculations and chart rendering

class Analytics {
    constructor(graphData) {
        this.nodes = graphData.nodes || [];
        this.edges = graphData.edges || [];
    }

    // Calculate summary metrics
    getSummaryMetrics() {
        const oldNodes = this.nodes.filter(n => n.status !== 'added').length;
        const newNodes = this.nodes.length - this.nodes.filter(n => n.status === 'removed').length;
        const oldEdges = this.edges.filter(e => e.status !== 'added').length;
        const newEdges = this.edges.length - this.edges.filter(e => e.status === 'removed').length;
        
        const changedNodes = this.nodes.filter(n => n.status !== 'same').length;
        const impactPercent = this.nodes.length > 0 
            ? Math.round((changedNodes / this.nodes.length) * 100) 
            : 0;

        return {
            nodesOld: oldNodes,
            nodesNew: newNodes,
            nodesChange: newNodes - oldNodes,
            edgesOld: oldEdges,
            edgesNew: newEdges,
            edgesChange: newEdges - oldEdges,
            impactPercent: impactPercent
        };
    }

    // Get distribution for chart
    getDistribution() {
        const counts = { added: 0, removed: 0, modified: 0, same: 0 };
        this.nodes.forEach(n => {
            if (counts[n.status] !== undefined) counts[n.status]++;
        });
        return counts;
    }

    // Get hotspot nodes (most impacted)
    getHotspots(limit = 5) {
        const nodeImpact = this.nodes.map(node => {
            const impactScore = (node.inAdded || 0) + (node.inRemoved || 0) + 
                              (node.outAdded || 0) + (node.outRemoved || 0);
            return {node.nickname || node.name,
                id: node.id,
                impact: impactScore
            };
        });

        return nodeImpact
            .filter(n => n.impact > 0)
            .sort((a, b) => b.impact - a.impact)
            .slice(0, limit);
    }

    // Draw donut chart on canvas
    drawDonutChart(canvas) {
        const dist = this.getDistribution();
        const ctx = canvas.getContext('2d');
        const centerX = canvas.width / 2;
        const centerY = canvas.height / 2;
        const radius = Math.min(centerX, centerY) * 0.7;
        const innerRadius = radius * 0.5;

        const colors = {
            added: '#2e7d32',
            removed: '#c62828',
            modified: '#f57f17',
            same: '#424242'
        };

        const total = Object.values(dist).reduce((a, b) => a + b, 0);
        if (total === 0) return;

        let currentAngle = -Math.PI / 2; // Start at top

        Object.entries(dist).forEach(([status, count]) => {
            if (count === 0) return;

            const sliceAngle = (count / total) * 2 * Math.PI;

            // Draw slice
            ctx.beginPath();
            ctx.arc(centerX, centerY, radius, currentAngle, currentAngle + sliceAngle);
            ctx.arc(centerX, centerY, innerRadius, currentAngle + sliceAngle, currentAngle, true);
            ctx.closePath();
            ctx.fillStyle = colors[status];
            ctx.fill();

            currentAngle += sliceAngle;
        });

        // Draw center circle (donut hole)
        ctx.beginPath();
        ctx.arc(centerX, centerY, innerRadius, 0, 2 * Math.PI);
        ctx.fillStyle = '#1c1e24';
        ctx.fill();

        // Draw total in center
        ctx.fillStyle = '#eceef5';
        ctx.font = 'bold 18px Inter';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(total, centerX, centerY);
    }

    // Export analytics as JSON
    exportJSON() {
        return {
            summary: this.getSummaryMetrics(),
            distribution: this.getDistribution(),
            hotspots: this.getHotspots(10),
            timestamp: new Date().toISOString()
        };
    }

    // Export analytics as CSV
    exportCSV() {
        const headers = 'Metric,Value\n';
        const metrics = this.getSummaryMetrics();
        const dist = this.getDistribution();
        
        let csv = headers;
        csv += `Nodes (Old),${metrics.nodesOld}\n`;
        csv += `Nodes (New),${metrics.nodesNew}\n`;
        csv += `Nodes (Change),${metrics.nodesChange}\n`;
        csv += `Edges (Old),${metrics.edgesOld}\n`;
        csv += `Edges (New),${metrics.edgesNew}\n`;
        csv += `Edges (Change),${metrics.edgesChange}\n`;
        csv += `Impact,%${metrics.impactPercent}\n`;
        csv += `\nDistribution\n`;
        csv += `Added,${dist.added}\n`;
        csv += `Removed,${dist.removed}\n`;
        csv += `Modified,${dist.modified}\n`;
        csv += `Unchanged,${dist.same}\n`;
        
        return csv;
    }
}

// Initialize and expose globally
if (typeof window !== 'undefined') {
    window.Analytics = Analytics;
}
