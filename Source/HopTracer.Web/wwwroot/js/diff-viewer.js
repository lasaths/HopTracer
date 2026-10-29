        // Data from server injection
        const GRAPH_DATA = window.HOP_DATA || {};

        // Configuration - ANDURA Colors (v2.2)
        const CONFIG = {
            colors: {
                added: '#44B782',
                removed: '#F45536',
                modified: '#F0963A',
                same: '#8A8C91',
                edgeSame: '#55575C',
                bg: '#000000',
                surface: '#090A0C',
                line: '#3C3D41',
                muted: '#8A8C91',
                textMuted: '#8A8C91',
                text: '#DADADD',
                selection: '#3A3CFF',
                emergency: '#F45536',
                group: '#141517',
                node: '#202125'
            },
            node: {
                width: 120,
                height: 50,
                radius: 6
            }
        };

        const CHANGED_STATUS_FILTERS = ['added', 'removed', 'modified'];

        // State
        let state = {
            nodes: [],
            groups: [],
            edges: [],
            view: { x: 0, y: 0, scale: 1 },
            dragging: false,
            dragMoved: false,
            minimapDragging: false,
            lastPos: { x: 0, y: 0 },
            selected: null,
            hovered: null,
            filterMode: 'all',
            filterStatuses: new Set(CHANGED_STATUS_FILTERS),
            search: '',
            searchMatches: new Set(),
            searchCatalog: [],
            searchEngine: null,
            spotlightOpen: false,
            spotlightResults: [],
            spotlightActiveIndex: 0,
            componentSort: 'risk',
            bounds: { minX: 0, maxX: 0, minY: 0, maxY: 0 },
            showPortDetails: true,
            showGroups: true,
            wireThreshold: Infinity, // Show all wires by default
            versionMix: 0.5, // 0 = Old, 1 = New
            activeDetailsTab: 'inspector',
            scriptContext: null,
            graphTabs: [],
            activeGraphTabId: null,
            nextGraphTabId: 1,
            sidebarHidden: false,
            detailsHidden: false,
            showChangedEdgesOnly: false,
            includeHighLevelModifications: false,
            largeModelMode: false,
            processingData: false,
            source: {
                old: { type: 'file', path: '', hash: null },
                new: { type: 'file', path: '', hash: null }
            },
            sourceModal: {
                side: 'old',
                mode: 'file',
                filePath: '',
                commitPath: '',
                commits: [],
                selectedCommit: null,
                commitAvailable: null
            }
        };

        const ROOT_GRAPH_TAB_ID = 'graph-root';
        const SPOTLIGHT_MAX_RESULTS = 28;

        // Elements
        const canvas = document.getElementById('graph-canvas');
        const ctx = canvas.getContext('2d');
        const minimap = document.getElementById('minimap-canvas');
        const miniCtx = minimap.getContext('2d');
        const detailsPaneInspector = document.getElementById('details-pane-inspector');
        const detailsPaneScript = document.getElementById('details-pane-script');
        const detailsTabInspector = document.getElementById('details-tab-inspector');
        const detailsTabScript = document.getElementById('details-tab-script');
        const graphTabsBar = document.getElementById('graph-tabs');
        const tooltip = document.getElementById('tooltip');
        const sidebar = document.getElementById('sidebar');
        const resizer = document.getElementById('resizer');
        const detailsPanel = document.getElementById('details-panel');
        const detailsResizer = document.getElementById('details-resizer');
        const perfIndicator = document.getElementById('perf-indicator');
        const mainContainer = document.getElementById('main-container');
        const metaFileOldElem = document.getElementById('meta-file-old');
        const metaFileNewElem = document.getElementById('meta-file-new');
        const btnSidebarCollapse = document.getElementById('btn-sidebar-collapse');
        const btnSidebarExpand = document.getElementById('btn-sidebar-expand');
        const btnDetailsCollapse = document.getElementById('btn-details-collapse');
        const btnDetailsExpand = document.getElementById('btn-details-expand');
        const sourceModal = document.getElementById('source-modal');
        const scriptModal = document.getElementById('script-modal');
        const sourceModalTitle = document.getElementById('source-modal-title');
        const sourceModalStatus = document.getElementById('source-modal-status');
        const scriptModalTitle = document.getElementById('script-modal-title');
        const scriptModalSubtitle = document.getElementById('script-modal-subtitle');
        const scriptModalBody = document.getElementById('script-modal-body');
        const sourceModeFileBtn = document.getElementById('source-mode-file');
        const sourceModeCommitBtn = document.getElementById('source-mode-commit');
        const sourcePaneFile = document.getElementById('source-pane-file');
        const sourcePaneCommit = document.getElementById('source-pane-commit');
        const sourceFilePath = document.getElementById('source-file-path');
        const sourceFileMeta = document.getElementById('source-file-meta');
        const sourceCommitPath = document.getElementById('source-commit-path');
        const sourceCommitMeta = document.getElementById('source-commit-meta');
        const sourceCommitList = document.getElementById('source-commit-list');
        const btnSourcePickFile = document.getElementById('btn-source-pick-file');
        const btnSourcePickCommitFile = document.getElementById('btn-source-pick-commit-file');
        const btnSourceLoadCommits = document.getElementById('btn-source-load-commits');
        const btnSourceApply = document.getElementById('btn-source-apply');
        const btnSourceCancel = document.getElementById('btn-source-cancel');
        const btnSourceModalClose = document.getElementById('btn-source-modal-close');
        const btnScriptModalClose = document.getElementById('btn-script-modal-close');
        const searchLauncher = document.getElementById('search-launcher');
        const searchLauncherTitle = document.getElementById('search-launcher-title');
        const searchLauncherMeta = document.getElementById('search-launcher-meta');
        const spotlightBackdrop = document.getElementById('spotlight-backdrop');
        const spotlightInput = document.getElementById('spotlight-input');
        const spotlightResults = document.getElementById('spotlight-results');
        const componentResultsList = document.getElementById('component-results-list');
        const componentResultsCount = document.getElementById('component-results-count');
        const componentSortSelect = document.getElementById('component-sort');
        const toggleHighLevelMods = document.getElementById('toggle-high-level-mods');
        const loadingOverlay = document.getElementById('loadingOverlay');
        const loadingTitle = document.getElementById('loadingTitle');
        const loadingSubtitle = document.getElementById('loadingSubtitle');
        const loadingPercent = document.getElementById('loadingPercent');
        const loadingProgressBar = document.getElementById('loadingProgressBar');
        let sessionToken = null;
        let loadingProgressTimer = null;
        let loadingProgressValue = 0;
        let spotlightReturnFocus = null;
        let scriptModalReturnFocus = null;

        async function ensureSessionToken() {
            if (sessionToken) return sessionToken;

            const response = await fetch('/system/session_token');
            if (!response.ok) {
                throw new Error('Failed to initialize session token.');
            }

            const payload = await response.json().catch(() => ({}));
            sessionToken = String(payload.token || '');
            if (!sessionToken) {
                throw new Error('Session token is missing.');
            }

            return sessionToken;
        }

        async function authFetch(url, options = {}) {
            const token = await ensureSessionToken();
            const headers = new Headers(options.headers || {});
            headers.set('X-HopTracer-Session', token);
            return fetch(url, { ...options, headers });
        }

        // Initialization
        async function init() {
            try {
                await ensureSessionToken();
                await processData();
                initSourceContext();
                if (isNarrowViewport()) {
                    state.sidebarHidden = true;
                    state.detailsHidden = true;
                }
                enforceResponsivePanelState();
                setupDetailsTabs();
                setupEventListeners();
                setupResizer();
                resize();
                fitView();
                updateStats();
                updateMetadata();
                renderComponentResultsList();
                requestAnimationFrame(loop);
            } catch (err) {
                console.error('HopTracer viewer initialization failed:', err);
                showPerf(`Viewer initialization failed: ${err && err.message ? err.message : err}`);
            }
        }

        const SCRIPT_PROPERTY_KEYS = new Set([
            'scriptsource', 'code', 'script', 'expression',
            'codeinput', 'codeoutput', 'pythonscript', 'pythoncode',
            'csharpcode', 'vbcode', 'sourcecode', 'additionalsource', 'usingsource'
        ]);
        const SCRIPT_NOISE_KEYS = new Set([
            'scriptparamaccess', 'editorposition', 'editorlocation', 'editorbounds',
            'cursorposition', 'caretposition', 'selectionstart', 'selectionlength',
            'showtypehints', 'typehintid', 'allowtreeaccess',
            'scriptcomponentversion', 'scriptparameterversion',
            'usingscriptinputparam', 'usingscriptoutputparam'
        ]);
        const SCRIPT_KEY_PATTERN = /(script|code|python|ghpython|csharp|c#|vb\.?net|expression)/i;
        const SCRIPT_COMPONENT_PATTERN = /(python|ghpython|c#|csharp|vb|script)/i;

        function setupDetailsTabs() {
            detailsTabInspector.addEventListener('click', () => setDetailsTab('inspector'));
            detailsTabScript.addEventListener('click', () => setDetailsTab('script'));
            setDetailsTab('inspector');
            renderEmptyScriptPane();
        }

        function setDetailsTab(tab) {
            state.activeDetailsTab = tab;

            const isInspector = tab === 'inspector';
            const isScript = tab === 'script';

            detailsTabInspector.classList.toggle('active', isInspector);
            detailsPaneInspector.classList.toggle('active', isInspector);

            detailsTabScript.classList.toggle('active', isScript);
            detailsPaneScript.classList.toggle('active', isScript);
        }

        function isAllStatusFilterActive() {
            return state.filterMode === 'all';
        }

        function doesNodeMatchStatusFilter(status) {
            if (isAllStatusFilterActive()) return true;
            return state.filterStatuses.has(status);
        }

        function normalizeSearchText(value, maxLength = 8000) {
            return String(value ?? '')
                .replace(/\r\n/g, '\n')
                .replace(/\s+/g, ' ')
                .trim()
                .slice(0, maxLength);
        }

        function detectScriptLanguage(node, key, sourceText) {
            const keyText = String(key || '').toLowerCase();
            const nameText = `${String(node?.name || '').toLowerCase()} ${String(node?.nickname || '').toLowerCase()}`;
            const sample = String(sourceText || '').toLowerCase();

            if (keyText.includes('python') || nameText.includes('python') ||
                /\b(def|import|from|lambda|elif|print)\b/.test(sample)) {
                return 'Python';
            }

            if (keyText.includes('c#') || keyText.includes('csharp') ||
                nameText.includes('c#') || nameText.includes('csharp') ||
                /\b(using|namespace|public class|private|var|new)\b/.test(sample)) {
                return 'C#';
            }

            return 'Other';
        }

        const PYTHON_SYNTAX_PATTERN = /(@[A-Za-z_][\w.]*)|(#.*$)|((?:[rRuUbBfF]|rb|br|fr|rf)?(?:"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'))|(\b(?:False|None|True|and|as|assert|async|await|break|case|class|continue|def|del|elif|else|except|finally|for|from|global|if|import|in|is|lambda|match|nonlocal|not|or|pass|raise|return|try|while|with|yield)\b)|(\b\d+(?:\.\d+)?\b)/gm;
        const CSHARP_SYNTAX_PATTERN = /(\/\/.*$)|(@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*')|(^\s*#\s*\w+.*$)|(\b(?:abstract|as|async|await|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|record|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|var|virtual|void|volatile|while)\b)|(\b(?:Action|CancellationToken|DateTime|Dictionary|Func|Guid|HashSet|ICollection|IDictionary|IEnumerable|IList|IReadOnlyDictionary|IReadOnlyList|List|Task|TimeSpan)\b)|(\b\d+(?:\.\d+)?(?:[fFdDmMuUlL])?\b)/gm;

        function normalizeSyntaxLanguage(language) {
            const label = String(language || '').trim().toLowerCase();
            if (label === 'python') return 'python';
            if (label === 'c#' || label === 'csharp' || label === 'csharpcode') return 'csharp';
            return 'plain';
        }

        function getSyntaxPattern(language) {
            switch (normalizeSyntaxLanguage(language)) {
                case 'python':
                    PYTHON_SYNTAX_PATTERN.lastIndex = 0;
                    return PYTHON_SYNTAX_PATTERN;
                case 'csharp':
                    CSHARP_SYNTAX_PATTERN.lastIndex = 0;
                    return CSHARP_SYNTAX_PATTERN;
                default:
                    return null;
            }
        }

        function highlightCodeLine(line, language) {
            const source = String(line ?? '');
            const pattern = getSyntaxPattern(language);
            if (!pattern || !source) return escapeHtml(source);

            let html = '';
            let lastIndex = 0;
            let match;

            while ((match = pattern.exec(source)) !== null) {
                const token = match[0];
                const tokenIndex = match.index;
                if (tokenIndex > lastIndex) {
                    html += escapeHtml(source.slice(lastIndex, tokenIndex));
                }

                let className = '';
                if (normalizeSyntaxLanguage(language) === 'python') {
                    if (match[1]) className = 'syntax-preprocessor';
                    else if (match[2]) className = 'syntax-comment';
                    else if (match[3]) className = 'syntax-string';
                    else if (match[4]) className = 'syntax-keyword';
                    else if (match[5]) className = 'syntax-number';
                } else if (normalizeSyntaxLanguage(language) === 'csharp') {
                    if (match[1]) className = 'syntax-comment';
                    else if (match[2]) className = 'syntax-string';
                    else if (match[3]) className = 'syntax-preprocessor';
                    else if (match[4]) className = 'syntax-keyword';
                    else if (match[5]) className = 'syntax-type';
                    else if (match[6]) className = 'syntax-number';
                }

                const safeToken = escapeHtml(token);
                html += className ? `<span class="${className}">${safeToken}</span>` : safeToken;
                lastIndex = pattern.lastIndex;
                if (token.length === 0) {
                    pattern.lastIndex += 1;
                }
            }

            if (lastIndex < source.length) {
                html += escapeHtml(source.slice(lastIndex));
            }

            return html;
        }

        function summarizeScriptSnippet(sourceText, query) {
            const compact = normalizeSearchText(sourceText, 8000);
            if (!compact) return '';
            const rawNeedle = String(query || '').trim();
            const needle = rawNeedle.toLowerCase();
            if (!needle) return compact.slice(0, 200);

            const lower = compact.toLowerCase();
            let idx = lower.indexOf(needle);
            if (idx < 0 && rawNeedle.includes(' ')) {
                const firstWord = needle.split(/\s+/).find(w => w.length > 1);
                if (firstWord) idx = lower.indexOf(firstWord);
            }
            if (idx < 0) {
                const words = needle.split(/\s+/).filter(w => w.length > 1);
                for (let i = 0; i < words.length; i++) {
                    idx = lower.indexOf(words[i]);
                    if (idx >= 0) break;
                }
            }
            if (idx < 0) return compact.slice(0, 200);

            const start = Math.max(0, idx - 80);
            const end = Math.min(compact.length, idx + Math.max(needle.length, 110));
            const prefix = start > 0 ? '…' : '';
            const suffix = end < compact.length ? '…' : '';
            return `${prefix}${compact.slice(start, end)}${suffix}`;
        }

        function buildInternalSearchTextForNode(node) {
            if (!node) return '';
            const parts = [];
            try {
                getUserInputEntries(node).forEach(e => {
                    if (e.newValue) parts.push(String(e.newValue));
                    if (e.oldValue) parts.push(String(e.oldValue));
                });
            } catch (e) {
                /* ignore */
            }
            if (isScriptComponent(node)) {
                getScriptPropertyEntries(node).forEach(e => {
                    if (e.newValue) parts.push(String(e.newValue));
                    if (e.oldValue) parts.push(String(e.oldValue));
                });
            }
            return normalizeSearchText(parts.join('\n'), 24000);
        }

        function getInternalSearchPreview(node, query) {
            if (!node) return null;
            const needle = String(query || '').trim().toLowerCase();
            if (!needle) return null;

            const tryMatch = (label, text) => {
                const t = String(text || '');
                if (!t.trim()) return null;
                if (!t.toLowerCase().includes(needle)) return null;
                return { label, snippet: summarizeScriptSnippet(t, query) };
            };

            try {
                const userEntries = getUserInputEntries(node);
                for (let i = 0; i < userEntries.length; i++) {
                    const e = userEntries[i];
                    const keyLabel = String(e.key || 'Field');
                    let hit = tryMatch(keyLabel, e.newValue);
                    if (!hit) hit = tryMatch(`${keyLabel} (old)`, e.oldValue);
                    if (hit) return hit;
                }
            } catch (e) {
                /* ignore */
            }

            if (isScriptComponent(node)) {
                const scriptEntries = getScriptPropertyEntries(node);
                for (let i = 0; i < scriptEntries.length; i++) {
                    const e = scriptEntries[i];
                    const keyLabel = String(e.key || 'Script');
                    let hit = tryMatch(keyLabel, e.newValue);
                    if (!hit) hit = tryMatch(`${keyLabel} (old)`, e.oldValue);
                    if (hit) return hit;
                }
            }

            return null;
        }

        function nodeMatchesSearch(node) {
            const search = String(state.search || '').trim().toLowerCase();
            if (!search) return true;

            const canonical = canonicalizeId(node?.id);
            if (canonical && state.searchMatches.has(canonical)) {
                return true;
            }

            const name = String(node?.name || '').toLowerCase();
            const nickname = String(node?.nickname || '').toLowerCase();
            return name.includes(search) || nickname.includes(search);
        }

        function buildSearchCatalog(nodes) {
            const catalog = [];
            (nodes || []).forEach(node => {
                if (!node) return;

                const nodeId = String(node.id || '');
                if (!nodeId) return;

                const title = String(node.nickname || node.name || 'Unnamed Component');
                const subtitle = String(node.name || '');
                const status = String(node.status || 'same').toLowerCase();
                const idText = String(node.id || '');
                const internalBlob = buildInternalSearchTextForNode(node);

                catalog.push({
                    id: `component:${nodeId}`,
                    type: 'component',
                    nodeId,
                    scriptKey: '',
                    language: '',
                    title,
                    subtitle: subtitle || 'Component',
                    preview: '',
                    status,
                    searchText: normalizeSearchText(`${title} ${subtitle} ${idText} ${internalBlob}`, 26000)
                });

                if (!isScriptComponent(node)) return;
                const scriptEntries = getScriptPropertyEntries(node);
                scriptEntries.forEach(entry => {
                    const rawScript = String(entry?.newValue || entry?.oldValue || '');
                    if (!rawScript.trim()) return;

                    const language = entry.language || detectScriptLanguage(node, entry.key, rawScript);
                    if (language !== 'C#' && language !== 'Python') return;

                    const scriptText = normalizeSearchText(rawScript, 12000);
                    if (!scriptText) return;

                    const scriptKey = String(entry.key || 'Script');
                    const diffTag = entry.isChanged ? 'modified' : 'unchanged';
                    catalog.push({
                        id: `script:${nodeId}:${scriptKey}`,
                        type: 'script',
                        nodeId,
                        scriptKey,
                        language,
                        title,
                        subtitle: `${language} code • ${scriptKey} (${diffTag})`,
                        preview: scriptText,
                        status,
                        searchText: normalizeSearchText(`${title} ${subtitle} ${language} ${scriptKey} ${scriptText}`, 14000)
                    });
                });
            });
            return catalog;
        }

        function createSearchEngine(catalog) {
            if (!window.Fuse || !Array.isArray(catalog) || catalog.length === 0) {
                return null;
            }

            return new window.Fuse(catalog, {
                includeScore: true,
                threshold: 0.32,
                ignoreLocation: true,
                minMatchCharLength: 2,
                keys: [
                    { name: 'title', weight: 0.24 },
                    { name: 'subtitle', weight: 0.12 },
                    { name: 'searchText', weight: 0.64 }
                ]
            });
        }

        function runBasicSearch(query) {
            const needle = String(query || '').trim().toLowerCase();
            if (!needle) return [];

            return (state.searchCatalog || [])
                .filter(entry => doesNodeMatchStatusFilter(entry.status))
                .filter(entry => String(entry.searchText || '').toLowerCase().includes(needle));
        }

        function runCatalogSearch(query) {
            const needle = String(query || '').trim();
            if (!needle) return [];

            const rawResults = state.searchEngine
                ? state.searchEngine.search(needle).map(result => result.item)
                : runBasicSearch(needle);
            const deduped = [];
            const seen = new Set();

            rawResults.forEach(entry => {
                if (!entry || !doesNodeMatchStatusFilter(entry.status)) return;
                const dedupeKey = entry.type === 'component' ? `component:${entry.nodeId}` : entry.id;
                if (seen.has(dedupeKey)) return;
                seen.add(dedupeKey);
                deduped.push(entry);
            });

            return deduped;
        }

        function getVisibleSpotlightResults() {
            return (state.spotlightResults || []).slice(0, SPOTLIGHT_MAX_RESULTS);
        }

        function isFuseAvailable() {
            return typeof window.Fuse === 'function';
        }

        function updateSearchLauncherState() {
            if (!searchLauncher || !searchLauncherTitle || !searchLauncherMeta) return;

            const query = String(state.search || '').trim();
            searchLauncher.classList.toggle('active', !!query);
            if (!query) {
                searchLauncherTitle.textContent = 'Search components + code';
                searchLauncherMeta.textContent = isFuseAvailable()
                    ? 'Spotlight search for C#/Python script components'
                    : 'Basic search mode (Fuse.js unavailable)';
                return;
            }

            const nodeMatches = state.searchMatches.size;
            searchLauncherTitle.textContent = `Search: ${query}`;
            searchLauncherMeta.textContent = `${nodeMatches.toLocaleString()} matching component${nodeMatches === 1 ? '' : 's'}`;
        }

        function renderSpotlightResults() {
            if (!spotlightResults) return;

            const query = String(state.search || '').trim();
            if (!query) {
                spotlightResults.innerHTML = '<div class="spotlight-empty">Type to search C#/Python scripts and component names.</div>';
                return;
            }

            const visible = getVisibleSpotlightResults();
            if (visible.length === 0) {
                spotlightResults.innerHTML = '<div class="spotlight-empty">No matches for the current query.</div>';
                return;
            }

            const active = Math.max(0, Math.min(state.spotlightActiveIndex, visible.length - 1));
            state.spotlightActiveIndex = active;
            spotlightResults.innerHTML = visible.map((entry, index) => {
                let previewBlock = '';
                if (entry.type === 'script' && entry.preview) {
                    previewBlock = `<div class="spotlight-item-preview">${escapeHtml(summarizeScriptSnippet(entry.preview, query))}</div>`;
                } else if (entry.type === 'component') {
                    const node = state.nodes.find(n => String(n.id) === String(entry.nodeId));
                    const internal = node ? getInternalSearchPreview(node, query) : null;
                    if (internal && internal.snippet) {
                        previewBlock = `
                            <div>
                                <div class="spotlight-item-preview-meta">${escapeHtml(internal.label)}</div>
                                <div class="spotlight-item-preview">${escapeHtml(internal.snippet)}</div>
                            </div>`;
                    }
                }

                return `
                    <button type="button" class="spotlight-item ${index === active ? 'active' : ''}" data-spotlight-index="${index}">
                        <div class="spotlight-item-head">
                            <div class="spotlight-item-title">${escapeHtml(entry.title || 'Unnamed')}</div>
                            <span class="spotlight-item-status" style="background:${CONFIG.colors[entry.status] || CONFIG.colors.same}">
                                ${escapeHtml(entry.status || 'same')}
                            </span>
                        </div>
                        <div class="spotlight-item-subtitle">${escapeHtml(entry.subtitle || 'Component')}</div>
                        ${previewBlock}
                    </button>
                `;
            }).join('');
        }

        function applySearchQuery(query, options = {}) {
            const { refreshView = true, syncInput = false } = options;
            const normalized = String(query ?? '').trim();
            state.search = normalized;

            if (!state.search) {
                state.searchMatches = new Set();
                state.spotlightResults = [];
                state.spotlightActiveIndex = 0;
            } else {
                state.spotlightResults = runCatalogSearch(state.search);
                state.searchMatches = new Set();
                state.spotlightResults.forEach(entry => {
                    const canonical = canonicalizeId(entry.nodeId);
                    if (canonical) state.searchMatches.add(canonical);
                });
                const visible = getVisibleSpotlightResults();
                state.spotlightActiveIndex = visible.length > 0
                    ? Math.min(state.spotlightActiveIndex, visible.length - 1)
                    : 0;
            }

            if (syncInput && spotlightInput) {
                spotlightInput.value = normalized;
            }

            updateSearchLauncherState();
            renderSpotlightResults();

            if (refreshView) {
                renderComponentResultsList();
                scheduleDraw();
            }
        }

        function rebuildSearchCatalog(options = {}) {
            const { refreshView = true } = options;
            state.searchCatalog = buildSearchCatalog(state.nodes);
            state.searchEngine = createSearchEngine(state.searchCatalog);
            applySearchQuery(state.search || '', { refreshView, syncInput: false });
        }

        function openSpotlight() {
            if (!spotlightBackdrop || !spotlightInput) return;
            const currentFocus = document.activeElement;
            const validFocus = currentFocus instanceof HTMLElement &&
                currentFocus !== document.body &&
                currentFocus !== document.documentElement
                ? currentFocus
                : null;
            spotlightReturnFocus = validFocus || searchLauncher;
            state.spotlightOpen = true;
            if (mainContainer) mainContainer.setAttribute('inert', '');
            if (sourceModal && sourceModal.classList.contains('open')) sourceModal.setAttribute('inert', '');
            spotlightBackdrop.classList.add('open');
            spotlightBackdrop.setAttribute('aria-hidden', 'false');
            document.body.style.overflow = 'hidden';
            renderSpotlightResults();
            spotlightInput.value = String(state.search || '');
            window.setTimeout(() => {
                spotlightInput.focus();
                spotlightInput.select();
            }, 0);
        }

        function closeSpotlight(options = {}) {
            if (!spotlightBackdrop) return;
            const { clearQuery = false } = options;

            if (spotlightInput && document.activeElement === spotlightInput) {
                spotlightInput.blur();
            }

            state.spotlightOpen = false;
            spotlightBackdrop.classList.remove('open');
            spotlightBackdrop.setAttribute('aria-hidden', 'true');
            if (mainContainer) mainContainer.removeAttribute('inert');
            if (sourceModal) sourceModal.removeAttribute('inert');
            document.body.style.overflow = '';
            if (clearQuery) {
                applySearchQuery('', { refreshView: true, syncInput: true });
            }

            const returnTarget = spotlightReturnFocus;
            spotlightReturnFocus = null;
            if (returnTarget instanceof HTMLElement && returnTarget.isConnected) {
                returnTarget.focus();
            } else if (searchLauncher) {
                searchLauncher.focus();
            }
        }

        function toggleSpotlight() {
            if (state.spotlightOpen) {
                closeSpotlight();
            } else {
                openSpotlight();
            }
        }

        function moveSpotlightSelection(delta) {
            const visible = getVisibleSpotlightResults();
            if (visible.length === 0) return;
            const current = Math.max(0, Math.min(state.spotlightActiveIndex, visible.length - 1));
            const next = (current + delta + visible.length) % visible.length;
            state.spotlightActiveIndex = next;
            renderSpotlightResults();

            const activeButton = spotlightResults?.querySelector(`[data-spotlight-index="${next}"]`);
            if (activeButton) {
                activeButton.scrollIntoView({ block: 'nearest' });
            }
        }

        function activateSpotlightSelection(index = state.spotlightActiveIndex) {
            const visible = getVisibleSpotlightResults();
            if (visible.length === 0) return;
            const targetIndex = Math.max(0, Math.min(index, visible.length - 1));
            const entry = visible[targetIndex];
            if (!entry) return;

            zoomToNode(entry.nodeId);
            if (entry.type === 'script' && entry.scriptKey) {
                openScriptView(entry.scriptKey);
            }
            closeSpotlight();
        }

        function getFilteredNodes() {
            const filtered = [];

            for (let i = 0; i < state.nodes.length; i++) {
                const n = state.nodes[i];
                if (doesNodeMatchStatusFilter(n.status) && nodeMatchesSearch(n)) {
                    filtered.push(n);
                }
            }

            filtered.sort((a, b) => compareNodesForComponentSort(a, b));

            return filtered;
        }

        function compareNodesAlphabetical(a, b, descending = false) {
            const an = String(a.nickname || a.name || '').toLowerCase();
            const bn = String(b.nickname || b.name || '').toLowerCase();
            if (an < bn) return descending ? 1 : -1;
            if (an > bn) return descending ? -1 : 1;
            return String(a.id || '').localeCompare(String(b.id || ''));
        }

        function getNodeDistanceFromOrigin(node) {
            const rect = getRenderedNodeRect(node);
            const cx = Number(rect.x) + (Number(rect.w) / 2);
            const cy = Number(rect.y) + (Number(rect.h) / 2);
            if (!Number.isFinite(cx) || !Number.isFinite(cy)) {
                return Number.POSITIVE_INFINITY;
            }
            return Math.hypot(cx, cy);
        }

        const DATA_SIZE_FALLBACK_EXCLUDED_KEYS = new Set([
            'clusterpreviewgraph',
            'clusterpreviewmessage',
            'clusterpreviewstatus'
        ]);

        function getUtf8ByteLength(value) {
            if (value === null || value === undefined) return 0;
            return new TextEncoder().encode(String(value)).length;
        }

        function getNodeInternalizedDataSize(node) {
            const props = node?.properties || {};
            const candidates = [
                props.PersistentData,
                props.persistentData,
                props.InternalisedData,
                props.internalisedData,
                props.InternalizedData,
                props.internalizedData
            ];

            let total = 0;
            for (let i = 0; i < candidates.length; i++) {
                total += getUtf8ByteLength(candidates[i]);
            }

            if (total > 0) {
                return total;
            }

            // Fallback: estimate from component values so "Data Size" remains useful on files
            // where explicit internalized payload keys are not emitted.
            for (const [key, value] of Object.entries(props)) {
                const normalizedKey = String(key || '').trim().toLowerCase();
                if (DATA_SIZE_FALLBACK_EXCLUDED_KEYS.has(normalizedKey)) continue;
                total += getUtf8ByteLength(value);
            }

            return total;
        }

        function compareNodesForComponentSort(a, b) {
            const sort = state.componentSort || 'risk';
            
            // ANDURA Review Queue: changed items first, sorted by risk (high to low)
            const aChanged = (a.status === 'added' || a.status === 'removed' || a.status === 'modified');
            const bChanged = (b.status === 'added' || b.status === 'removed' || b.status === 'modified');
            
            // Changed before unchanged
            if (aChanged !== bChanged) {
                return aChanged ? -1 : 1;
            }

            if (sort === 'risk') {
                const ar = Number(a.riskScore ?? a.risk ?? a.RiskScore ?? 0);
                const br = Number(b.riskScore ?? b.risk ?? b.RiskScore ?? 0);
                if (ar !== br) return br - ar; // Higher risk first
                return compareNodesAlphabetical(a, b, false);
            }

            if (sort === 'za') {
                return compareNodesAlphabetical(a, b, true);
            }

            if (sort === 'dist') {
                const ad = getNodeDistanceFromOrigin(a);
                const bd = getNodeDistanceFromOrigin(b);
                if (ad < bd) return -1;
                if (ad > bd) return 1;
                return compareNodesAlphabetical(a, b, false);
            }

            if (sort === 'data') {
                const as = getNodeInternalizedDataSize(a);
                const bs = getNodeInternalizedDataSize(b);
                const aHasData = as > 0;
                const bHasData = bs > 0;

                if (aHasData && bHasData) {
                    if (as !== bs) return bs - as; // Larger internalized payloads first.
                    return compareNodesAlphabetical(a, b, false);
                }

                if (aHasData !== bHasData) {
                    return aHasData ? -1 : 1;
                }

                // Requested fallback: if data size is zero, use A-Z ordering.
                return compareNodesAlphabetical(a, b, false);
            }

            return compareNodesAlphabetical(a, b, false);
        }

        function getRenderedNodeRect(node, mix = state.versionMix) {
            return {
                x: node.x - (node.dx || 0) * (1 - mix),
                y: node.y - (node.dy || 0) * (1 - mix),
                w: node.w,
                h: node.h
            };
        }

        function renderComponentResultsList() {
            // Render ANDURA-style risk-ordered change list
            const changeList = document.getElementById('change-list');
            const changeListCount = document.getElementById('change-list-count');
            
            if (!changeList) return;

            const filtered = getFilteredNodes();
            const total = filtered.length;

            if (changeListCount) {
                changeListCount.textContent = total.toLocaleString();
            }
            changeList.innerHTML = '';

            if (total === 0) {
                const empty = document.createElement('div');
                empty.className = 'component-results-empty';
                empty.textContent = 'No changes to review.';
                changeList.appendChild(empty);
                return;
            }

            const fragment = document.createDocumentFragment();

            filtered.forEach(node => {
                const item = document.createElement('button');
                item.className = 'qrow change-item';
                if (state.selected === node.id) item.classList.add('selected sel');
                item.dataset.nodeId = String(node.id);
                item.tabIndex = 0;
                item.type = 'button';
                
                const displayName = String(node.nickname || node.name || 'Unnamed Component');
                const reason = getRiskReason(node) || 'No changes detected';
                item.setAttribute('title', `${displayName} - ${reason}`);

                // Status glyph column (.qg)
                const glyphCol = document.createElement('span');
                glyphCol.className = 'qg';
                
                const statusGlyph = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
                statusGlyph.setAttribute('class', 'ico');
                statusGlyph.setAttribute('viewBox', '0 0 256 256');
                statusGlyph.setAttribute('fill', 'currentColor');
                
                const use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
                const status = node.status || 'same';
                const glyphMap = {
                    added: '#ph-plus-circle',
                    removed: '#ph-minus-circle',
                    modified: '#ph-pencil-simple-line',
                    same: '#ph-circle-dashed'
                };
                
                // Status color classes
                const statusColorMap = {
                    added: 'ad',
                    removed: 'em',
                    modified: 'ca',
                    same: 'muted'
                };
                statusGlyph.classList.add(statusColorMap[status] || 'muted');
                
                use.setAttributeNS('http://www.w3.org/1999/xlink', 'href', `/vendor/phosphor/icons.svg${glyphMap[status] || glyphMap.same}`);
                statusGlyph.appendChild(use);
                glyphCol.appendChild(statusGlyph);

                // Content column (.grow)
                const content = document.createElement('span');
                content.className = 'grow';
                
                // Line 1: Name
                const nameRow = document.createElement('span');
                nameRow.className = 'row';
                const name = document.createElement('span');
                name.className = 'trunc qn';
                name.textContent = displayName;
                name.title = displayName;
                nameRow.appendChild(name);
                content.appendChild(nameRow);
                
                // Line 2: Status + reason
                const reasonRow = document.createElement('div');
                reasonRow.className = 'small muted trunc';
                
                const statusLabel = document.createElement('span');
                statusLabel.className = 'label';
                statusLabel.style.color = 'inherit';
                statusLabel.textContent = status.charAt(0).toUpperCase() + status.slice(1);
                
                reasonRow.appendChild(statusLabel);
                reasonRow.appendChild(document.createTextNode(' · ' + reason));
                reasonRow.title = reason;
                content.appendChild(reasonRow);

                // Risk column (.qr)
                const riskCol = document.createElement('span');
                riskCol.className = 'qr';
                
                const riskScoreRaw = node.riskScore ?? node.risk ?? node.RiskScore;
                const riskScore = Number.isFinite(Number(riskScoreRaw)) ? Number(riskScoreRaw) : 0;
                const roundedRisk = Math.round(riskScore);
                
                // High risk: warning-octagon in Emergency
                // Medium risk: warning in Caution
                // Low risk: just the number
                if (roundedRisk >= 70) {
                    const highIcon = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
                    highIcon.setAttribute('class', 'ico em');
                    highIcon.setAttribute('viewBox', '0 0 256 256');
                    highIcon.setAttribute('fill', 'currentColor');
                    const highUse = document.createElementNS('http://www.w3.org/2000/svg', 'use');
                    highUse.setAttributeNS('http://www.w3.org/1999/xlink', 'href', '/vendor/phosphor/icons.svg#ph-warning-octagon');
                    highIcon.appendChild(highUse);
                    riskCol.appendChild(highIcon);
                } else if (roundedRisk >= 40) {
                    const medIcon = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
                    medIcon.setAttribute('class', 'ico ca');
                    medIcon.setAttribute('viewBox', '0 0 256 256');
                    medIcon.setAttribute('fill', 'currentColor');
                    const medUse = document.createElementNS('http://www.w3.org/2000/svg', 'use');
                    medUse.setAttributeNS('http://www.w3.org/1999/xlink', 'href', '/vendor/phosphor/icons.svg#ph-warning');
                    medIcon.appendChild(medUse);
                    riskCol.appendChild(medIcon);
                } else {
                    // Low risk: empty 16px space
                    const spacer = document.createElement('span');
                    spacer.style.width = '16px';
                    riskCol.appendChild(spacer);
                }
                
                const riskValue = document.createElement('span');
                riskValue.className = 'mono';
                riskValue.textContent = String(roundedRisk);
                riskCol.appendChild(riskValue);

                item.appendChild(glyphCol);
                item.appendChild(content);
                item.appendChild(riskCol);

                // Click handler
                item.addEventListener('click', () => {
                    zoomToNode(node.id);
                });
                
                // Keyboard handler for accessibility
                item.addEventListener('keydown', (e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        zoomToNode(node.id);
                    } else if (e.key.toLowerCase() === 'j') {
                        e.preventDefault();
                        navigateToNext();
                    } else if (e.key.toLowerCase() === 'k') {
                        e.preventDefault();
                        navigateToPrevious();
                    }
                });

                fragment.appendChild(item);
            });

            changeList.appendChild(fragment);
        }

        function getRiskReason(node) {
            // Use real riskReasons array if available
            if (!node) return '';
            
            // First priority: use actual riskReasons array from diff computation
            if (Array.isArray(node.riskReasons) && node.riskReasons.length > 0) {
                return node.riskReasons[0]; // Show first/primary reason
            }
            
            const status = node.status || 'same';
            const riskScoreRaw = node.riskScore ?? node.risk ?? node.RiskScore;
            const riskScore = Number.isFinite(Number(riskScoreRaw)) ? Number(riskScoreRaw) : 0;
            
            if (status === 'added') return 'Component added to definition';
            if (status === 'removed') return 'Component removed from definition';
            if (status === 'modified') {
                // Build reason from actual changed fields
                const parts = [];
                
                // Check for position change
                const hasMoved = node.dx != null && node.dy != null && (node.dx !== 0 || node.dy !== 0);
                if (hasMoved) {
                    parts.push('Moved');
                }
                
                // Check for changed inputs/outputs
                if (node.changedInputs && node.changedInputs.length > 0) {
                    parts.push(`${node.changedInputs.length} input${node.changedInputs.length > 1 ? 's' : ''} changed`);
                }
                if (node.changedOutputs && node.changedOutputs.length > 0) {
                    parts.push(`${node.changedOutputs.length} output${node.changedOutputs.length > 1 ? 's' : ''} changed`);
                }
                
                // Check for script change
                if (node.scriptChanged) {
                    parts.push('Script changed');
                }
                
                if (parts.length > 0) {
                    return parts.join(', ');
                }
                
                // Fall back to risk-based message
                if (riskScore >= 70) return 'High-risk modifications detected';
                if (riskScore >= 40) return 'Moderate changes to parameters';
                return 'Minor parameter adjustments';
            }
            return 'No changes';
        }

        function zoomToNode(nodeId) {
            if (!nodeId) return;

            const needle = String(nodeId);
            const node = state.nodes.find(n => String(n.id) === needle);
            if (!node) return;

            const rect = getRenderedNodeRect(node);
            const targetScale = Math.max(state.view.scale, 1.15);
            state.view.scale = targetScale;
            state.view.x = (canvas.width / 2) - (rect.x + rect.w / 2) * targetScale;
            state.view.y = (canvas.height / 2) - (rect.y + rect.h / 2) * targetScale;
            state.selected = node.id;

            updateDetailsPanel(node);
            renderComponentResultsList();
            scheduleDraw();
        }

        function navigateToNext() {
            // Only navigate through changed nodes
            const filtered = getFilteredNodes().filter(n => 
                n.status === 'added' || n.status === 'removed' || n.status === 'modified'
            );
            if (filtered.length === 0) return;

            const currentIndex = filtered.findIndex(n => n.id === state.selected);
            const nextIndex = (currentIndex + 1) % filtered.length;
            zoomToNode(filtered[nextIndex].id);
        }

        function navigateToPrevious() {
            // Only navigate through changed nodes
            const filtered = getFilteredNodes().filter(n => 
                n.status === 'added' || n.status === 'removed' || n.status === 'modified'
            );
            if (filtered.length === 0) return;

            const currentIndex = filtered.findIndex(n => n.id === state.selected);
            const prevIndex = currentIndex <= 0 ? filtered.length - 1 : currentIndex - 1;
            zoomToNode(filtered[prevIndex].id);
        }

        function syncStatusFilterButtons() {
            document.querySelectorAll('.filter-btn[data-filter]').forEach(btn => {
                const key = btn.dataset.filter;
                if (key === 'all') {
                    btn.classList.toggle('active', isAllStatusFilterActive());
                } else {
                    btn.classList.toggle('active', !isAllStatusFilterActive() && state.filterStatuses.has(key));
                }
            });
        }

        function setScriptTabVisibility(visible, count = 0) {
            detailsTabScript.style.display = visible ? '' : 'none';
            detailsTabScript.textContent = count > 1 ? `Script (${count})` : 'Script';

            if (!visible && state.activeDetailsTab === 'script') {
                setDetailsTab('inspector');
            }
        }

        function renderEmptyScriptPane(message = 'Select a script component to view source') {
            closeExpandedScriptDiff({ restoreFocus: false });
            detailsPaneScript.innerHTML = `<div class="script-empty">${message}</div>`;
        }

        function applyPanelVisibility() {
            if (!mainContainer) return;

            mainContainer.classList.toggle('sidebar-hidden', !!state.sidebarHidden);
            mainContainer.classList.toggle('details-hidden', !!state.detailsHidden);

            resize();
        }

        function isNarrowViewport() {
            return window.innerWidth <= 900;
        }

        function enforceResponsivePanelState() {
            if (!isNarrowViewport()) return false;
            if (!state.sidebarHidden && !state.detailsHidden) {
                state.detailsHidden = true;
                return true;
            }
            return false;
        }

        function getFileNameFromPath(path) {
            const value = String(path || '').trim();
            if (!value) return '';
            return value.split(/[\\/]/).pop() || value;
        }

        function shortHash(hash) {
            const value = String(hash || '').trim();
            if (!value) return '';
            return value.length > 7 ? value.substring(0, 7) : value;
        }

        function formatBytes(bytes) {
            const value = Number(bytes);
            if (!Number.isFinite(value) || value < 0) return 'unknown';
            if (value < 1024) return `${Math.round(value)} B`;
            const units = ['KB', 'MB', 'GB', 'TB'];
            let size = value / 1024;
            let unitIndex = 0;
            while (size >= 1024 && unitIndex < units.length - 1) {
                size /= 1024;
                unitIndex++;
            }
            return `${size.toFixed(size >= 100 ? 0 : (size >= 10 ? 1 : 2))} ${units[unitIndex]}`;
        }

        async function fetchFileInfo(path) {
            if (!path) return null;
            try {
                const res = await authFetch('/git/file_info', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ path })
                });
                if (!res.ok) return null;
                const data = await res.json().catch(() => ({}));
                return {
                    sizeBytes: data.size_bytes ?? data.sizeBytes ?? null,
                    modifiedUtc: data.modified_utc ?? data.modifiedUtc ?? null
                };
            } catch {
                return null;
            }
        }

        async function updateSourcePathMeta(targetElem, path) {
            if (!targetElem) return;
            const value = String(path || '').trim();
            targetElem.dataset.path = value;
            if (!value) {
                targetElem.textContent = '';
                return;
            }

            targetElem.textContent = 'File size: loading...';
            const info = await fetchFileInfo(value);
            if (targetElem.dataset.path !== value) return;

            if (info && info.sizeBytes !== null) {
                targetElem.textContent = `File size: ${formatBytes(info.sizeBytes)}`;
            } else {
                targetElem.textContent = 'File size: unknown';
            }
        }

        function toggleSidebar() {
            const willShowSidebar = state.sidebarHidden;
            state.sidebarHidden = !state.sidebarHidden;
            if (isNarrowViewport() && willShowSidebar) {
                state.detailsHidden = true;
            }
            applyPanelVisibility();
        }

        function toggleDetailsPanel() {
            const willShowDetails = state.detailsHidden;
            state.detailsHidden = !state.detailsHidden;
            if (isNarrowViewport() && willShowDetails) {
                state.sidebarHidden = true;
            }
            applyPanelVisibility();
        }

        function setLoadingProgress(value, subtitle) {
            const clamped = Math.max(0, Math.min(100, Math.round(value)));
            loadingProgressValue = clamped;
            if (loadingPercent) loadingPercent.textContent = `${clamped}%`;
            if (loadingProgressBar) loadingProgressBar.style.width = `${clamped}%`;
            if (subtitle && loadingSubtitle) loadingSubtitle.textContent = subtitle;
        }

        function startLoadingProgressSimulation(mode = 'compare') {
            if (loadingProgressTimer) {
                clearInterval(loadingProgressTimer);
                loadingProgressTimer = null;
            }

            const stagePlan = mode === 'git'
                ? [
                    { upTo: 25, text: 'Contacting Git and preparing file...' },
                    { upTo: 55, text: 'Converting selected revision...' },
                    { upTo: 80, text: 'Parsing graph data...' },
                    { upTo: 94, text: 'Computing differences...' }
                ]
                : [
                    { upTo: 20, text: 'Preparing selected files...' },
                    { upTo: 52, text: 'Converting .gh files to .ghx...' },
                    { upTo: 78, text: 'Parsing graph data...' },
                    { upTo: 94, text: 'Computing differences...' }
                ];

            let stageIndex = 0;
            let stage = stagePlan[stageIndex];
            setLoadingProgress(2, stage.text);

            loadingProgressTimer = setInterval(() => {
                if (stageIndex < stagePlan.length - 1 && loadingProgressValue >= stage.upTo) {
                    stageIndex++;
                    stage = stagePlan[stageIndex];
                }

                const target = stage.upTo;
                if (loadingProgressValue < target) {
                    const remaining = target - loadingProgressValue;
                    const step = Math.max(1, Math.min(remaining, Math.ceil(remaining * 0.14)));
                    setLoadingProgress(loadingProgressValue + step, stage.text);
                } else {
                    setLoadingProgress(loadingProgressValue, stage.text);
                }
            }, 260);
        }

        function showLoadingOverlay(title, subtitle, mode = 'compare') {
            if (!loadingOverlay) return;
            if (title && loadingTitle) loadingTitle.textContent = title;
            if (subtitle && loadingSubtitle) loadingSubtitle.textContent = subtitle;
            loadingOverlay.style.display = 'flex';
            startLoadingProgressSimulation(mode);
        }

        async function postForm(url, data, options = {}) {
            const mode = options.mode || (url.includes('/git/') ? 'git' : 'compare');
            const title = options.title || 'Converting & Comparing...';
            const subtitle = options.subtitle || (mode === 'git'
                ? 'Fetching version and preparing diff.'
                : 'This may take a few moments for large files.');

            showLoadingOverlay(title, subtitle, mode);

            const form = document.createElement('form');
            form.method = 'POST';
            form.action = url;
            // /compare binds IFormFile parameters; enforce multipart to avoid HTTP 415.
            form.enctype = 'multipart/form-data';

            Object.entries(data).forEach(([key, value]) => {
                if (value === undefined || value === null || value === '') return;
                const input = document.createElement('input');
                input.type = 'hidden';
                input.name = key;
                input.value = String(value);
                form.appendChild(input);
            });

            const token = await ensureSessionToken();
            const tokenInput = document.createElement('input');
            tokenInput.type = 'hidden';
            tokenInput.name = '_session_token';
            tokenInput.value = token;
            form.appendChild(tokenInput);

            document.body.appendChild(form);
            requestAnimationFrame(() => {
                requestAnimationFrame(() => {
                    form.submit();
                });
            });
        }

        async function pickNativeFilePath() {
            try {
                const response = await authFetch('/api/filepicker/pick-native-file');
                if (!response.ok) {
                    const err = await response.json().catch(() => ({}));
                    return { path: null, error: err.error || 'No file selected.' };
                }

                const data = await response.json().catch(() => ({}));
                return { path: data.path || null, error: null };
            } catch {
                return { path: null, error: 'Failed to open file picker.' };
            }
        }

        async function fetchCommitsForPath(path, options = {}) {
            const { silent = false } = options;
            try {
                const checkRes = await authFetch('/git/check', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ path })
                });
                const checkData = await checkRes.json().catch(() => ({}));
                if (!checkData.is_repo) {
                    return { commits: [], error: silent ? null : 'Selected file is not inside a Git repository.' };
                }

                const res = await authFetch('/git/commits', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ path })
                });
                if (!res.ok) return { commits: [], error: silent ? null : 'Failed to load commits.' };
                const data = await res.json().catch(() => ({}));
                return {
                    commits: Array.isArray(data.commits) ? data.commits : [],
                    error: silent ? null : (data.error || null)
                };
            } catch {
                return { commits: [], error: silent ? null : 'Failed to fetch commits.' };
            }
        }

        function initSourceContext() {
            const meta = GRAPH_DATA.meta || {};

            state.source.old = {
                type: String(meta.sourceTypeOld || meta.SourceTypeOld || (meta.commitHash || meta.CommitHash ? 'commit' : 'file')),
                path: String(meta.sourcePathOld || meta.SourcePathOld || ''),
                hash: meta.sourceHashOld || meta.SourceHashOld || meta.commitHash || meta.CommitHash || null
            };
            state.source.new = {
                type: String(meta.sourceTypeNew || meta.SourceTypeNew || 'file'),
                path: String(meta.sourcePathNew || meta.SourcePathNew || ''),
                hash: meta.sourceHashNew || meta.SourceHashNew || null
            };
        }

        function setSourceModalStatus(message = '', tone = 'muted') {
            if (!sourceModalStatus) return;
            const colors = {
                muted: 'var(--text-secondary)',
                info: 'var(--accent-color)',
                warn: '#ffb74d',
                error: '#ff6b6b'
            };
            sourceModalStatus.textContent = message;
            sourceModalStatus.style.color = colors[tone] || colors.muted;
        }

        function renderSourceCommitList() {
            if (!sourceCommitList) return;
            const commits = state.sourceModal.commits || [];
            sourceCommitList.replaceChildren();

            if (!commits.length) {
                const emptyNode = document.createElement('div');
                emptyNode.style.padding = '12px';
                emptyNode.style.color = 'var(--text-secondary)';
                emptyNode.style.fontSize = '11px';
                emptyNode.textContent = 'No commits loaded.';
                sourceCommitList.replaceChildren(emptyNode);
                return;
            }

            const fragment = document.createDocumentFragment();
            commits.forEach(c => {
                const hash = String(c.hash || '');
                const author = String(c.author || c.Author || 'Unknown');
                const age = String(c.date || c.Date || 'Unknown age');
                const sizeBytes = c.fileSizeBytes ?? c.FileSizeBytes ?? 0;
                const sizeLabel = formatBytes(sizeBytes);
                const row = document.createElement('button');
                row.type = 'button';
                row.className = 'source-commit-row';
                row.dataset.commitHash = hash;
                row.setAttribute('aria-pressed', state.sourceModal.selectedCommit === hash ? 'true' : 'false');
                if (state.sourceModal.selectedCommit === hash) {
                    row.classList.add('selected');
                }
                row.title = `Blame: ${author} | Age: ${age} | Size: ${sizeLabel}`;

                const hashNode = document.createElement('div');
                hashNode.className = 'source-commit-hash';
                hashNode.textContent = hash.substring(0, 7);

                const mainNode = document.createElement('div');
                mainNode.className = 'source-commit-main';

                const messageNode = document.createElement('div');
                messageNode.className = 'source-commit-msg';
                messageNode.textContent = c.message || '(no message)';
                messageNode.title = c.message || '(no message)';

                const metaNode = document.createElement('div');
                metaNode.className = 'source-commit-meta';
                metaNode.textContent = `Blame: ${author} · Age: ${age} · Size: ${sizeLabel}`;
                metaNode.title = `Blame: ${author} | Age: ${age} | Size: ${sizeLabel}`;

                mainNode.append(messageNode, metaNode);
                row.append(hashNode, mainNode);
                row.addEventListener('click', () => {
                    state.sourceModal.selectedCommit = hash;
                    renderSourceCommitList();
                    setSourceModalStatus(`Selected commit ${hash.substring(0, 7)}`, 'info');
                });
                fragment.appendChild(row);
            });
            sourceCommitList.appendChild(fragment);
        }

        function renderSourceModal() {
            const m = state.sourceModal;
            const canUseCommit = m.commitAvailable !== false;
            if (!canUseCommit && m.mode === 'commit') {
                m.mode = 'file';
            }
            const isCommit = m.mode === 'commit';
            const isOld = m.side === 'old';

            if (sourceModalTitle) {
                sourceModalTitle.textContent = isOld ? 'Change Left Source' : 'Change Right Source';
            }

            if (sourceModeFileBtn) sourceModeFileBtn.classList.toggle('active', !isCommit);
            if (sourceModeCommitBtn) {
                sourceModeCommitBtn.style.display = canUseCommit ? '' : 'none';
                sourceModeCommitBtn.classList.toggle('active', isCommit);
                sourceModeCommitBtn.disabled = !isOld;
            }

            if (sourcePaneFile) sourcePaneFile.style.display = isCommit ? 'none' : '';
            if (sourcePaneCommit) sourcePaneCommit.style.display = isCommit ? '' : 'none';

            if (sourceFilePath) sourceFilePath.textContent = m.filePath || 'No file selected';
            if (sourceCommitPath) sourceCommitPath.textContent = m.commitPath || 'No file selected';
            updateSourcePathMeta(sourceFileMeta, m.filePath);
            updateSourcePathMeta(sourceCommitMeta, m.commitPath);

            renderSourceCommitList();
        }

        function openSourceModal(side) {
            state.sourceModal.side = side;
            state.sourceModal.mode = state.source[side].type === 'commit' ? 'commit' : 'file';
            state.sourceModal.filePath = state.source[side].path || '';
            state.sourceModal.commitPath = state.source[side].path || state.source.old.path || state.source.new.path || '';
            state.sourceModal.commits = [];
            state.sourceModal.selectedCommit = state.source[side].hash || null;
            state.sourceModal.commitAvailable = null;

            renderSourceModal();
            setSourceModalStatus('', 'muted');
            if (sourceModal) sourceModal.classList.add('open');

            if (state.sourceModal.commitPath) {
                (async () => {
                    const { commits } = await fetchCommitsForPath(state.sourceModal.commitPath, { silent: true });
                    if (!sourceModal || !sourceModal.classList.contains('open') || state.sourceModal.side !== side) return;
                    state.sourceModal.commitAvailable = commits.length > 0;
                    if (!state.sourceModal.commitAvailable) {
                        state.sourceModal.mode = 'file';
                        setSourceModalStatus('No commit history found for this file.', 'warn');
                    } else if (state.sourceModal.mode === 'commit' && !state.sourceModal.selectedCommit && commits.length) {
                        state.sourceModal.commits = commits;
                        state.sourceModal.selectedCommit = commits[0].hash;
                    }
                    renderSourceModal();
                })();
            }
        }

        function closeSourceModal() {
            if (sourceModal) sourceModal.classList.remove('open');
        }

        async function loadSourceModalCommits() {
            const path = state.sourceModal.commitPath;
            if (!path) {
                setSourceModalStatus('Choose a file first.', 'warn');
                return;
            }

            setSourceModalStatus('Loading commits...', 'info');
            const { commits, error } = await fetchCommitsForPath(path);
            state.sourceModal.commits = commits;
            state.sourceModal.selectedCommit = commits.length ? commits[0].hash : null;
            renderSourceCommitList();
            if (error) {
                setSourceModalStatus(error, 'error');
                return;
            }
            if (!commits.length) {
                setSourceModalStatus('No commits found for this file.', 'warn');
                return;
            }
            setSourceModalStatus(`Loaded ${commits.length} commits.`, 'info');
        }

        async function applySourceModalSelection() {
            const m = state.sourceModal;
            const side = m.side;

            if (m.mode === 'file') {
                if (!m.filePath) {
                    setSourceModalStatus('Pick a file to continue.', 'warn');
                    return;
                }
                state.source[side] = { type: 'file', path: m.filePath, hash: null };
                const otherSide = side === 'old' ? 'new' : 'old';

                if (!state.source[otherSide].path) {
                    setSourceModalStatus(`Set the ${otherSide === 'old' ? 'left' : 'right'} file before comparing.`, 'warn');
                    return;
                }
            }
            else {
                if (!m.commitPath) {
                    setSourceModalStatus('Choose a Git-tracked file first.', 'warn');
                    return;
                }
                if (!m.selectedCommit) {
                    setSourceModalStatus('Select a commit first.', 'warn');
                    return;
                }
                state.source[side] = { type: 'commit', path: m.commitPath, hash: m.selectedCommit };

                const otherSide = side === 'old' ? 'new' : 'old';
                if (!state.source[otherSide].path) {
                    setSourceModalStatus(`Set the ${otherSide === 'old' ? 'left' : 'right'} source before comparing.`, 'warn');
                    return;
                }
            }

            closeSourceModal();
            const oldIsCommit = state.source.old.type === 'commit' && !!state.source.old.hash;
            const newIsCommit = state.source.new.type === 'commit' && !!state.source.new.hash;
            if (oldIsCommit || newIsCommit) {
                await postForm('/git/view_diff', {
                    path_old: state.source.old.path,
                    hash_old: oldIsCommit ? state.source.old.hash : null,
                    path_new: state.source.new.path,
                    hash_new: newIsCommit ? state.source.new.hash : null
                });
                return;
            }

            await postForm('/compare', {
                path_old: state.source.old.path,
                path_new: state.source.new.path
            });
        }

        function calculateBoundsForNodes(nodes) {
            if (!Array.isArray(nodes) || nodes.length === 0) {
                return { minX: 0, maxX: 0, minY: 0, maxY: 0 };
            }

            let minX = Infinity;
            let maxX = -Infinity;
            let minY = Infinity;
            let maxY = -Infinity;

            for (let i = 0; i < nodes.length; i++) {
                const n = nodes[i];
                if (!Number.isFinite(n.x) || !Number.isFinite(n.y)) continue;

                if (n.x < minX) minX = n.x;
                if (n.x + n.w > maxX) maxX = n.x + n.w;
                if (n.y < minY) minY = n.y;
                if (n.y + n.h > maxY) maxY = n.y + n.h;
            }

            if (!Number.isFinite(minX) || !Number.isFinite(maxX) || !Number.isFinite(minY) || !Number.isFinite(maxY)) {
                return { minX: 0, maxX: 0, minY: 0, maxY: 0 };
            }

            return { minX, maxX, minY, maxY };
        }

        function createGraphTab({ id, title, type, sourceNodeId, parentTabId, breadcrumbSegments, nodes, groups, edges }) {
            const segments = Array.isArray(breadcrumbSegments) && breadcrumbSegments.length > 0
                ? breadcrumbSegments
                : ['Main'];

            return {
                id,
                title: title || segments.join(' / '),
                type,
                sourceNodeId: sourceNodeId || null,
                parentTabId: parentTabId || null,
                breadcrumbSegments: segments,
                nodes,
                groups: Array.isArray(groups) ? groups : [],
                edges,
                bounds: calculateBoundsForNodes(nodes),
                view: null,
                selected: null
            };
        }

        function getGraphNodeTabLabel(node) {
            const rawName = String(node?.nickname || node?.name || 'Cluster').trim();
            return rawName || 'Cluster';
        }

        function getActiveGraphTab() {
            return state.graphTabs.find(t => t.id === state.activeGraphTabId) || null;
        }

        function persistActiveGraphTabState() {
            const active = getActiveGraphTab();
            if (!active) return;

            active.view = { ...state.view };
            active.selected = state.selected;
            active.bounds = { ...state.bounds };
        }

        function initializeGraphTabs(nodes, edges, groups = []) {
            state.graphTabs = [
                createGraphTab({
                    id: ROOT_GRAPH_TAB_ID,
                    title: 'Main',
                    type: 'root',
                    breadcrumbSegments: ['Main'],
                    nodes,
                    groups,
                    edges
                })
            ];
            state.activeGraphTabId = null;
            switchGraphTab(ROOT_GRAPH_TAB_ID, { fit: true, preserveCurrent: false });
        }

        function renderGraphTabs() {
            if (!graphTabsBar) return;

            graphTabsBar.innerHTML = state.graphTabs.map(tab => {
                const isActive = tab.id === state.activeGraphTabId;
                const closable = tab.id !== ROOT_GRAPH_TAB_ID;
                const closeButton = closable
                    ? `<span class="graph-tab-close" data-close-tab-id="${escapeHtml(tab.id)}" title="Close tab"><svg class="ico" width="12" height="12"><use href="/vendor/phosphor/icons.svg#ph-x"></use></svg></span>`
                    : '';

                return `
                    <button type="button" class="graph-tab-btn ${isActive ? 'active' : ''}" data-tab-id="${escapeHtml(tab.id)}" title="${escapeHtml(tab.title)}">
                        <span class="graph-tab-title">${escapeHtml(tab.title)}</span>
                        ${closeButton}
                    </button>
                `;
            }).join('');

            graphTabsBar.querySelectorAll('[data-tab-id]').forEach(btn => {
                btn.addEventListener('click', () => {
                    const tabId = btn.dataset.tabId;
                    if (!tabId || tabId === state.activeGraphTabId) return;
                    switchGraphTab(tabId);
                });
            });

            graphTabsBar.querySelectorAll('[data-close-tab-id]').forEach(btn => {
                btn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    const tabId = btn.dataset.closeTabId;
                    if (!tabId) return;
                    closeGraphTab(tabId);
                });
            });
        }

        function switchGraphTab(tabId, options = {}) {
            const { fit = false, preserveCurrent = true } = options;
            const tab = state.graphTabs.find(t => t.id === tabId);
            if (!tab) return;

            if (preserveCurrent) {
                persistActiveGraphTabState();
            }

            state.activeGraphTabId = tab.id;
            state.nodes = tab.nodes;
            state.groups = tab.groups || [];
            state.edges = tab.edges;
            state.bounds = { ...tab.bounds };
            state.selected = tab.selected || null;
            state.view = tab.view ? { ...tab.view } : { x: 0, y: 0, scale: 1 };
            applyModificationStatusToNodes(state.nodes);
            rebuildSearchCatalog({ refreshView: false });

            const selectedNode = state.nodes.find(n => n.id === state.selected);
            updateDetailsPanel(selectedNode || null);

            if (fit || !tab.view) {
                fitView();
            } else {
                scheduleDraw();
            }

            renderGraphTabs();
            renderComponentResultsList();
        }

        function closeGraphTab(tabId) {
            if (!tabId || tabId === ROOT_GRAPH_TAB_ID) return;
            const targetTab = state.graphTabs.find(t => t.id === tabId);
            if (!targetTab) return;

            const tabsToClose = new Set([tabId]);
            let expanded = true;
            while (expanded) {
                expanded = false;
                state.graphTabs.forEach(tab => {
                    if (tab.parentTabId && tabsToClose.has(tab.parentTabId) && !tabsToClose.has(tab.id)) {
                        tabsToClose.add(tab.id);
                        expanded = true;
                    }
                });
            }

            const activeWillClose = tabsToClose.has(state.activeGraphTabId);
            let fallbackTabId = ROOT_GRAPH_TAB_ID;

            if (targetTab.parentTabId &&
                !tabsToClose.has(targetTab.parentTabId) &&
                state.graphTabs.some(t => t.id === targetTab.parentTabId)) {
                fallbackTabId = targetTab.parentTabId;
            }

            state.graphTabs = state.graphTabs.filter(tab => !tabsToClose.has(tab.id));

            if (activeWillClose) {
                if (!state.graphTabs.some(t => t.id === fallbackTabId)) {
                    fallbackTabId = ROOT_GRAPH_TAB_ID;
                }
                switchGraphTab(fallbackTabId, { fit: false, preserveCurrent: false });
            } else {
                renderGraphTabs();
            }
        }

        function setupResizer() {
            let isLeftResizing = false;
            let isRightResizing = false;

            resizer.addEventListener('mousedown', (e) => {
                isLeftResizing = true;
                resizer.classList.add('active');
                document.body.style.cursor = 'col-resize';
                e.preventDefault(); // Prevent text selection
            });

            if (detailsResizer) {
                detailsResizer.addEventListener('mousedown', (e) => {
                    isRightResizing = true;
                    detailsResizer.classList.add('active');
                    document.body.style.cursor = 'col-resize';
                    e.preventDefault();
                });
            }

            window.addEventListener('mousemove', (e) => {
                if (isLeftResizing) {
                    let newWidth = e.clientX;
                    if (newWidth < 220) newWidth = 220;
                    if (newWidth > 640) newWidth = 640;

                    sidebar.style.width = newWidth + 'px';
                    resize();
                }

                if (isRightResizing && detailsPanel) {
                    let newWidth = window.innerWidth - e.clientX;
                    if (newWidth < 300) newWidth = 300;
                    if (newWidth > 760) newWidth = 760;

                    detailsPanel.style.width = `${newWidth}px`;
                    resize();
                }
            });

            window.addEventListener('mouseup', () => {
                if (isLeftResizing || isRightResizing) {
                    isLeftResizing = false;
                    isRightResizing = false;
                    resizer.classList.remove('active');
                    if (detailsResizer) detailsResizer.classList.remove('active');
                    document.body.style.cursor = '';
                    resize();
                }
            });
        }

        function formatTimeDifference(ms) {
            const seconds = Math.floor(ms / 1000);
            const minutes = Math.floor(seconds / 60);
            const hours = Math.floor(minutes / 60);
            const days = Math.floor(hours / 24);
            const months = Math.floor(days / 30);
            const years = Math.floor(days / 365);

            if (years > 0) return `${years} year${years > 1 ? 's' : ''}`;
            if (months > 0) return `${months} month${months > 1 ? 's' : ''}`;
            if (days > 0) return `${days} day${days > 1 ? 's' : ''}`;
            if (hours > 0) return `${hours} hour${hours > 1 ? 's' : ''}`;
            if (minutes > 0) return `${minutes} minute${minutes > 1 ? 's' : ''}`;
            return `${seconds} second${seconds !== 1 ? 's' : ''}`;
        }

        function updateMetadata() {
            const meta = GRAPH_DATA.meta || {};
            const commitHash = meta.commitHash || meta.CommitHash;
            const commitMessage = meta.commitMessage || meta.CommitMessage;
            const commitAuthor = meta.commitAuthor || meta.CommitAuthor;
            const commitDate = meta.commitDate || meta.CommitDate;
            const fileOld = meta.fileOld || meta.FileOld;
            const fileNew = meta.fileNew || meta.FileNew;
            const sourcePathOld = meta.sourcePathOld || meta.SourcePathOld || '';
            const sourcePathNew = meta.sourcePathNew || meta.SourcePathNew || '';
            const sourceTypeOld = String(meta.sourceTypeOld || meta.SourceTypeOld || 'file');
            const sourceTypeNew = String(meta.sourceTypeNew || meta.SourceTypeNew || 'file');
            const sourceHashOld = meta.sourceHashOld || meta.SourceHashOld || null;
            const sourceHashNew = meta.sourceHashNew || meta.SourceHashNew || null;
            const isCommitContext = sourceTypeOld === 'commit' || sourceTypeNew === 'commit';

            const analyticsTitle = document.getElementById('analytics-section-title');
            if (analyticsTitle) {
                analyticsTitle.textContent = isCommitContext ? 'Source Details' : 'File Analytics';
            }

            const leftNameElem = document.getElementById('meta-new-name');
            const leftDateElem = document.getElementById('meta-new-date');
            const leftVersionElem = document.getElementById('meta-new-version');
            const rightNameElem = document.getElementById('meta-old-name');
            const rightDateElem = document.getElementById('meta-old-date');
            const rightVersionElem = document.getElementById('meta-old-version');
            const timeDiffContainer = document.getElementById('time-diff-container');
            const versionDiffContainer = document.getElementById('version-diff-container');
            const versionDiffValue = document.getElementById('version-diff-value');
            const getVersionInfo = (graphMeta) => {
                const fileVersion = graphMeta?.fileVersion || graphMeta?.FileVersion || '';
                const archiveVersion = graphMeta?.archiveVersion || graphMeta?.ArchiveVersion || '';
                const compareValue = fileVersion || archiveVersion || '';
                const displayValue = fileVersion || (archiveVersion ? `Archive ${archiveVersion}` : 'Unknown');
                const titleParts = [];

                if (fileVersion) {
                    titleParts.push(`Grasshopper file version: ${fileVersion}`);
                }
                if (archiveVersion) {
                    titleParts.push(`Archive version: ${archiveVersion}`);
                }
                if (!titleParts.length) {
                    titleParts.push('File version unavailable');
                }

                return {
                    compareKind: fileVersion ? 'file' : (archiveVersion ? 'archive' : ''),
                    compareValue,
                    displayValue,
                    title: titleParts.join(' • ')
                };
            };
            const oldVersionInfo = getVersionInfo(GRAPH_DATA.oldMeta);
            const newVersionInfo = getVersionInfo(GRAPH_DATA.newMeta);
            const versionsDiffer = Boolean(oldVersionInfo.compareKind) &&
                oldVersionInfo.compareKind === newVersionInfo.compareKind &&
                Boolean(oldVersionInfo.compareValue) &&
                Boolean(newVersionInfo.compareValue) &&
                oldVersionInfo.compareValue !== newVersionInfo.compareValue;

            [leftVersionElem, rightVersionElem].forEach(elem => {
                if (!elem) return;
                elem.classList.toggle('version-changed', versionsDiffer);
            });

            if (leftVersionElem) {
                leftVersionElem.textContent = newVersionInfo.displayValue;
                leftVersionElem.title = newVersionInfo.title;
            }

            if (rightVersionElem) {
                rightVersionElem.textContent = oldVersionInfo.displayValue;
                rightVersionElem.title = oldVersionInfo.title;
            }

            if (versionDiffContainer && versionDiffValue) {
                if (versionsDiffer) {
                    versionDiffValue.textContent = `${oldVersionInfo.displayValue} -> ${newVersionInfo.displayValue}`;
                    versionDiffContainer.style.display = 'block';
                } else {
                    versionDiffContainer.style.display = 'none';
                }
            }

            if (isCommitContext) {
                const applySourceInfo = (nameElem, dateElem, sourceType, sourcePath, sourceHash) => {
                    if (!nameElem || !dateElem) return;

                    if (sourceType === 'commit') {
                        const resolvedHash = sourceHash || commitHash || '';
                        const hashLabel = shortHash(resolvedHash) || 'unknown';
                        const isPrimaryCommit = Boolean(commitHash) && resolvedHash && String(resolvedHash).startsWith(String(commitHash).substring(0, hashLabel.length));

                        nameElem.textContent = isPrimaryCommit && commitMessage
                            ? commitMessage
                            : `Commit ${hashLabel}`;
                        nameElem.title = resolvedHash ? `Commit ${resolvedHash}` : 'Commit';

                        if (isPrimaryCommit && (commitAuthor || commitDate)) {
                            const details = [];
                            if (commitAuthor) details.push(`by ${commitAuthor}`);
                            if (commitDate) details.push(commitDate);
                            dateElem.textContent = details.join(' • ');
                        } else {
                            dateElem.textContent = 'From Git history';
                        }
                        dateElem.title = resolvedHash ? `Full hash: ${resolvedHash}` : 'Git commit source';
                        return;
                    }

                    const fileName = getFileNameFromPath(sourcePath) || 'Current file';
                    nameElem.textContent = fileName;
                    nameElem.title = sourcePath || fileName;
                    dateElem.textContent = 'Working tree file';
                    dateElem.title = sourcePath || fileName;
                };

                applySourceInfo(leftNameElem, leftDateElem, sourceTypeNew, sourcePathNew, sourceHashNew);
                applySourceInfo(rightNameElem, rightDateElem, sourceTypeOld, sourcePathOld, sourceHashOld);
                if (timeDiffContainer) {
                    timeDiffContainer.style.display = 'none';
                }
            } else {
                if (GRAPH_DATA.newMeta) {
                    leftNameElem.textContent = GRAPH_DATA.newMeta.name || 'Unknown';
                    leftDateElem.textContent = GRAPH_DATA.newMeta.date ? new Date(GRAPH_DATA.newMeta.date).toLocaleString() : 'Unknown Date';
                    leftDateElem.title = GRAPH_DATA.newMeta.description || '';
                }
                if (GRAPH_DATA.oldMeta) {
                    rightNameElem.textContent = GRAPH_DATA.oldMeta.name || 'Unknown';
                    rightDateElem.textContent = GRAPH_DATA.oldMeta.date ? new Date(GRAPH_DATA.oldMeta.date).toLocaleString() : 'Unknown Date';
                    rightDateElem.title = GRAPH_DATA.oldMeta.description || '';
                }

                if (GRAPH_DATA.newMeta && GRAPH_DATA.oldMeta && GRAPH_DATA.newMeta.date && GRAPH_DATA.oldMeta.date) {
                    const newDate = new Date(GRAPH_DATA.newMeta.date);
                    const oldDate = new Date(GRAPH_DATA.oldMeta.date);
                    const diffMs = Math.abs(newDate - oldDate);
                    const timeDiff = formatTimeDifference(diffMs);

                    document.getElementById('time-diff-value').textContent = timeDiff;
                    if (timeDiffContainer) {
                        timeDiffContainer.style.display = 'block';
                    }
                } else if (timeDiffContainer) {
                    timeDiffContainer.style.display = 'none';
                }
            }

            // Populate file metadata header
            if (GRAPH_DATA.meta) {
                const metaHeader = document.getElementById('file-metadata-header');
                const metaFileOld = document.getElementById('meta-file-old');
                const metaFileNew = document.getElementById('meta-file-new');

                const displayOld = fileOld || getFileNameFromPath(sourcePathOld) || 'Old Source';
                const displayNew = fileNew || getFileNameFromPath(sourcePathNew) || 'New Source';

                metaFileOld.textContent = displayOld;
                metaFileNew.textContent = displayNew;
                metaFileOld.title = sourcePathOld || displayOld;
                metaFileNew.title = sourcePathNew || displayNew;
                metaHeader.style.display = 'flex';
                
                // Update time diff display
                const metaTimeDiff = document.getElementById('meta-time-diff');
                if (metaTimeDiff) {
                    if (GRAPH_DATA.newMeta && GRAPH_DATA.oldMeta && GRAPH_DATA.newMeta.date && GRAPH_DATA.oldMeta.date) {
                        const newDate = new Date(GRAPH_DATA.newMeta.date);
                        const oldDate = new Date(GRAPH_DATA.oldMeta.date);
                        const diffMs = Math.abs(newDate - oldDate);
                        const timeDiffText = formatTimeDifference(diffMs);
                        metaTimeDiff.textContent = timeDiffText;
                        metaTimeDiff.style.display = 'inline-block';
                    } else {
                        metaTimeDiff.textContent = '-';
                        metaTimeDiff.style.display = 'none';
                    }
                }
            }
        }

        function renderInsights() {
            const meta = GRAPH_DATA.meta || {};
            const stageElem = document.getElementById('insights-stage');
            const riskElem = document.getElementById('insights-risk');
            const diagnosticsElem = document.getElementById('insights-diagnostics');

            const timings = meta.stageTimings || meta.StageTimings || [];
            if (stageElem) {
                stageElem.textContent = timings.length
                    ? `Stage timings: ${timings.map(t => `${(t.name || t.Name)} ${t.durationMs ?? t.DurationMs}ms`).join(' · ')}`
                    : 'Stage timings: n/a';
            }

            const risk = meta.riskSummary || meta.RiskSummary;
            if (riskElem) {
                if (risk) {
                    riskElem.textContent = `Risk summary: C${risk.criticalCount ?? risk.CriticalCount ?? 0} H${risk.highCount ?? risk.HighCount ?? 0} M${risk.mediumCount ?? risk.MediumCount ?? 0} L${risk.lowCount ?? risk.LowCount ?? 0}`;
                } else {
                    riskElem.textContent = 'Risk summary: n/a';
                }
            }

            const diagnostics = meta.diagnostics || meta.Diagnostics || [];
            if (diagnosticsElem) {
                if (diagnostics.length) {
                    diagnosticsElem.innerHTML = 'Diagnostics:<br>' + diagnostics
                        .slice(0, 6)
                        .map(d => {
                            const code = d.code || d.Code || 'INFO';
                            const msg = d.message || d.Message || '';
                            const sev = (d.severity || d.Severity || 'info').toUpperCase();
                            return `<div style="margin-top:4px;"><strong>${sev}</strong> [${code}] ${escapeHtml(msg)}</div>`;
                        })
                        .join('');
                } else {
                    diagnosticsElem.textContent = 'Diagnostics: none';
                }
            }
        }

        async function exportForensicReport() {
            const meta = GRAPH_DATA.meta || {};
            const payload = {
                fileOld: meta.fileOld || meta.FileOld || null,
                fileNew: meta.fileNew || meta.FileNew || null,
                nodeCount: state.nodes.length,
                edgeCount: state.edges.length,
                diagnostics: meta.diagnostics || meta.Diagnostics || [],
                topRisks: meta.topRisks || meta.TopRisks || [],
                riskSummary: meta.riskSummary || meta.RiskSummary || null,
                baselineName: meta.baselineName || meta.BaselineName || null
            };

            try {
                const res = await authFetch('/review/report/from_diff', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });

                const data = await res.json().catch(() => ({}));
                if (!res.ok) {
                    alert(`Forensic export failed: ${data.error || res.statusText}`);
                    return;
                }

                const stemBase = `hoptracer_forensic_${Date.now()}`;
                downloadTextFile(`${stemBase}.json`, data.report_json || '{}', 'application/json;charset=utf-8');
                downloadTextFile(`${stemBase}.html`, data.report_html || '<html><body>Empty report</body></html>', 'text/html;charset=utf-8');
            } catch (err) {
                alert(`Forensic export failed: ${err}`);
            }
        }

        async function saveBaselinePrompt() {
            const result = await pickNativeFilePath();
            if (!result.path) {
                if (result.error && !String(result.error).toLowerCase().includes('cancel')) {
                    alert(`Baseline save failed: ${result.error}`);
                }
                return;
            }
            const defaultName = (result.path.split(/[\\/]/).pop() || 'baseline')
                .replace(/\.(gh|ghx)$/i, '')
                .replace(/[^a-zA-Z0-9_-]/g, '_');
            const name = prompt('Baseline name:', defaultName || 'baseline');
            if (!name) return;

            const res = await authFetch('/review/baseline/save', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ path: result.path, name })
            });
            const data = await res.json().catch(() => ({}));
            alert(res.ok ? `Baseline saved: ${name}` : `Baseline save failed: ${data.error || res.statusText}`);
        }

        async function checkBaselinePrompt() {
            const result = await pickNativeFilePath();
            if (!result.path) {
                if (result.error && !String(result.error).toLowerCase().includes('cancel')) {
                    alert(`Baseline compare failed: ${result.error}`);
                }
                return;
            }
            const name = prompt('Baseline name to compare:');
            if (!name) return;

            const res = await authFetch('/review/baseline/compare', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ path: result.path, name })
            });
            const data = await res.json().catch(() => ({}));
            if (!res.ok) {
                alert(`Baseline compare failed: ${data.error || res.statusText}`);
                return;
            }
            const verdict = data.passed ? 'PASSED' : 'FAILED';
            alert(`Baseline ${verdict}\nFingerprint Match: ${data.fingerprint_match}\nCritical: ${data.risk_summary?.criticalCount ?? 0}\nHigh: ${data.risk_summary?.highCount ?? 0}`);
        }

        function downloadTextFile(filename, content, contentType = 'application/json;charset=utf-8') {
            const blob = new Blob([content], { type: contentType });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            a.remove();
            URL.revokeObjectURL(url);
        }

        async function processData() {
            const rawNodes = GRAPH_DATA.nodes || GRAPH_DATA.Nodes || [];
            const rawEdges = GRAPH_DATA.edges || GRAPH_DATA.Edges || [];

            state.processingData = true;
            state.largeModelMode = rawNodes.length >= 4000 || rawEdges.length >= 12000;
            showPerf(`Preparing ${rawNodes.length.toLocaleString()} nodes...`);

            let normalizedNodes = [];
            if (rawNodes.length < 2500) {
                normalizedNodes = rawNodes.map(normalizeNode);
            } else {
                const chunkSize = 1200;
                normalizedNodes = [];
                for (let i = 0; i < rawNodes.length; i += chunkSize) {
                    const chunk = rawNodes.slice(i, i + chunkSize).map(normalizeNode);
                    normalizedNodes.push(...chunk);
                    showPerf(`Preparing nodes ${Math.min(rawNodes.length, i + chunk.length).toLocaleString()} / ${rawNodes.length.toLocaleString()}...`);
                    await waitFrame();
                }
            }

            const { componentNodes, groupNodes } = splitComponentAndGroupNodes(normalizedNodes);
            state.nodes = componentNodes;
            state.groups = buildRenderableGroups(groupNodes, componentNodes);

            const nodeMap = buildNodeLookupMap(state.nodes);

            const attachEdgeNodes = e => {
                const sourceNode = nodeMap.get(e.source) || nodeMap.get(canonicalizeId(e.source));
                const targetNode = nodeMap.get(e.target) || nodeMap.get(canonicalizeId(e.target));
                if (!sourceNode || !targetNode) return null;

                return {
                    ...e,
                    source: sourceNode.id,
                    target: targetNode.id,
                    sourceNode,
                    targetNode
                };
            };

            if (rawEdges.length < 6000) {
                state.edges = rawEdges
                    .map(normalizeEdge)
                    .map(attachEdgeNodes)
                    .filter(e => !!e);
            } else {
                const chunkSize = 2500;
                state.edges = [];
                for (let i = 0; i < rawEdges.length; i += chunkSize) {
                    const mapped = rawEdges.slice(i, i + chunkSize)
                        .map(normalizeEdge)
                        .map(attachEdgeNodes)
                        .filter(e => !!e);
                    state.edges.push(...mapped);
                    showPerf(`Preparing edges ${Math.min(rawEdges.length, i + chunkSize).toLocaleString()} / ${rawEdges.length.toLocaleString()}...`);
                    await waitFrame();
                }
            }

            initializeGraphTabs(state.nodes, state.edges, state.groups);
            applyModificationStatusMode({ refreshUI: false });
            updateBounds();
            state.processingData = false;
            let perfMessage = '';
            if (rawEdges.length > 0 && state.edges.length === 0) {
                perfMessage = 'No wires could be mapped to visible nodes. Check node/edge ID format.';
            } else if (state.largeModelMode) {
                perfMessage = `Large-model mode: viewport culling enabled (${state.nodes.length.toLocaleString()} nodes, ${state.edges.length.toLocaleString()} wires)`;
            }
            showPerf(perfMessage);
        }

        function normalizeNodeStatus(rawStatus) {
            const status = String(rawStatus || 'same').trim().toLowerCase();
            if (status === 'added' || status === 'removed' || status === 'modified') return status;
            return 'same';
        }

        function getNodeOriginalStatus(node) {
            return normalizeNodeStatus(node?.statusOriginal ?? node?.status ?? 'same');
        }

        function valuesDifferForPropertyComparison(keyA, keyB, oldValue, newValue) {
            const oldText = String(oldValue ?? '');
            const newText = String(newValue ?? '');
            const scriptLike = isLikelyScriptPropertyKey(keyA) ||
                isLikelyScriptPropertyKey(keyB) ||
                looksLikeScriptContent(oldText) ||
                looksLikeScriptContent(newText);
            if (scriptLike) {
                return normalizeScriptForCompare(oldText) !== normalizeScriptForCompare(newText);
            }
            return oldText !== newText;
        }

        const CLUSTER_METADATA_PROPERTY_KEYS = new Set([
            'iscluster',
            'clusterhash',
            'clustersize',
            'clusterpreviewstatus',
            'clusterpreviewmessage',
            'clusterpreviewgraph'
        ]);

        function isClusterMetadataPropertyKey(key) {
            return CLUSTER_METADATA_PROPERTY_KEYS.has(String(key || '').trim().toLowerCase());
        }

        function hasValueLevelPropertyDifferences(node) {
            const props = node?.properties || {};
            const oldProps = node?.propertiesOld || {};
            const originalStatus = getNodeOriginalStatus(node);
            const hasSparseOldValues = originalStatus === 'modified' || originalStatus === 'same';
            const oldByLowerKey = new Map();

            Object.entries(oldProps).forEach(([key, value]) => {
                if (isClusterMetadataPropertyKey(key)) return;
                oldByLowerKey.set(String(key || '').toLowerCase(), { key, value });
            });

            const matchedOldKeys = new Set();
            for (const [key, newValue] of Object.entries(props)) {
                if (isClusterMetadataPropertyKey(key)) continue;
                const lowerKey = String(key || '').toLowerCase();
                const oldEntry = oldByLowerKey.get(lowerKey);
                if (!oldEntry) {
                    if (!hasSparseOldValues && String(newValue ?? '') !== '') return true;
                    continue;
                }

                matchedOldKeys.add(lowerKey);
                if (valuesDifferForPropertyComparison(key, oldEntry.key, oldEntry.value, newValue)) {
                    return true;
                }
            }

            for (const [lowerKey, oldEntry] of oldByLowerKey.entries()) {
                if (matchedOldKeys.has(lowerKey)) continue;
                if (!hasSparseOldValues && String(oldEntry.value ?? '') !== '') return true;
            }

            return false;
        }

        function hasValueLevelPortDifferences(node) {
            const ports = [
                ...(Array.isArray(node?.inputs) ? node.inputs : []),
                ...(Array.isArray(node?.outputs) ? node.outputs : [])
            ];

            return ports.some(port => {
                if (isTrueish(port?.valueChanged)) return true;
                const status = String(port?.status || port?.Status || 'same').trim().toLowerCase();
                return status === 'modified';
            });
        }

        function hasValueLevelModification(node) {
            return hasValueLevelPropertyDifferences(node) || hasValueLevelPortDifferences(node);
        }

        function hasExplicitClusterInternalsChange(preview) {
            if (!preview?.isCluster) return false;

            const statusText = String(preview.status || '').toLowerCase();
            if (statusText.includes('unchanged') || statusText.includes('same') || statusText.includes('identical')) {
                return false;
            }
            if (statusText.includes('modified') || statusText.includes('changed') || statusText.includes('diff')) {
                return true;
            }

            const oldHash = String(preview.oldHash || '').trim();
            const newHash = String(preview.newHash || '').trim();
            if (oldHash && newHash) {
                return oldHash !== newHash;
            }

            return false;
        }

        function computeEffectiveNodeStatus(node, includeHighLevel = showHighLevelModificationDetails()) {
            const originalStatus = getNodeOriginalStatus(node);
            if (originalStatus !== 'modified') return originalStatus;
            if (includeHighLevel) return 'modified';
            if (hasValueLevelModification(node)) return 'modified';

            // Cluster internals are a user-visible diff, not just a high-level heuristic.
            return hasExplicitClusterInternalsChange(getClusterPreviewData(node)) ? 'modified' : 'same';
        }

        function applyModificationStatusToNodes(nodes, includeHighLevel = showHighLevelModificationDetails()) {
            if (!Array.isArray(nodes) || nodes.length === 0) return false;

            let changed = false;
            nodes.forEach(node => {
                const nextStatus = computeEffectiveNodeStatus(node, includeHighLevel);
                if (node.status !== nextStatus) {
                    node.status = nextStatus;
                    changed = true;
                }
            });

            return changed;
        }

        function applyModificationStatusMode(options = {}) {
            const refreshUI = options.refreshUI !== false;
            const includeHighLevel = showHighLevelModificationDetails();

            if (Array.isArray(state.graphTabs) && state.graphTabs.length > 0) {
                state.graphTabs.forEach(tab => {
                    applyModificationStatusToNodes(tab.nodes, includeHighLevel);
                });
            }

            applyModificationStatusToNodes(state.nodes, includeHighLevel);
            rebuildSearchCatalog({ refreshView: false });

            if (!refreshUI) return;

            updateStats();
            const selectedNode = state.nodes.find(n => n.id === state.selected) || null;
            updateDetailsPanel(selectedNode);
            renderComponentResultsList();
            scheduleDraw();
        }

        function normalizeNode(n) {
            const id = n.id || n.Id || `node_${Math.random().toString(36).slice(2, 10)}`;
            const name = n.name || n.Name || 'Unknown';
            const nickname = n.nickname || n.Nickname || name;
            const status = normalizeNodeStatus(n.status || n.Status || 'same');
            const properties = normalizePropertyMap(n.properties || n.Properties);
            const propertiesOld = normalizePropertyMap(n.propertiesOld || n.PropertiesOld);
            const riskScoreRaw = n.riskScore ?? n.RiskScore;
            const riskScore = Number.isFinite(Number(riskScoreRaw)) ? Number(riskScoreRaw) : undefined;
            const riskReasonsRaw = n.riskReasons ?? n.RiskReasons;
            const riskReasons = Array.isArray(riskReasonsRaw)
                ? riskReasonsRaw.map(r => String(r || '').trim()).filter(Boolean)
                : [];
            const rawX = n.x !== undefined ? n.x : (n.X !== undefined ? n.X : 0);
            const rawY = n.y !== undefined ? n.y : (n.Y !== undefined ? n.Y : 0);
            const rawW = n.w !== undefined ? n.w : (n.W !== undefined ? n.W : CONFIG.node.width);
            const rawH = n.h !== undefined ? n.h : (n.H !== undefined ? n.H : CONFIG.node.height);
            const xNum = Number(rawX);
            const yNum = Number(rawY);
            const wNum = Number(rawW);
            const hNum = Number(rawH);
            const hasExplicitBounds =
                (n.w !== undefined || n.W !== undefined) &&
                (n.h !== undefined || n.H !== undefined) &&
                Number.isFinite(wNum) && Number.isFinite(hNum) &&
                wNum > 0 && hNum > 0;
            const x = Number.isFinite(xNum) ? xNum : 0;
            const y = Number.isFinite(yNum) ? yNum : 0;
            const w = Number.isFinite(wNum) && wNum > 0 ? wNum : CONFIG.node.width;
            const h = Number.isFinite(hNum) && hNum > 0 ? hNum : CONFIG.node.height;

            return {
                ...n,
                id,
                name,
                nickname,
                status,
                statusOriginal: status,
                x,
                y,
                w,
                h,
                hasExplicitBounds,
                properties,
                propertiesOld,
                riskScore,
                riskReasons,
                inputs: normalizePorts(n.inputs || n.Inputs || [], 'input', id),
                outputs: normalizePorts(n.outputs || n.Outputs || [], 'output', id),
                vx: 0,
                vy: 0,
                fx: 0,
                fy: 0
            };
        }

        function normalizePropertyMap(props) {
            if (!props || typeof props !== 'object') return {};
            return { ...props };
        }

        const BOOLEAN_PORT_OPTION_CANONICAL_KEYS = ['Optional', 'Flatten', 'Graft', 'Simplify', 'Reverse'];

        function normalizePortOptionMap(options) {
            if (!options || typeof options !== 'object') return {};

            const normalized = {};
            Object.keys(options)
                .sort((a, b) => a.localeCompare(b))
                .forEach(key => {
                    const value = options[key];
                    if (value === null || value === undefined) return;
                    normalized[String(key)] = String(value).trim();
                });

            return normalized;
        }

        function normalizeBooleanPortOptionValue(value) {
            const normalizedValue = String(value ?? '').trim().toLowerCase();
            if (!normalizedValue) return 'false';
            if (normalizedValue === 'true' || normalizedValue === '1' || normalizedValue === 'yes' || normalizedValue === 'on') return 'true';
            if (normalizedValue === 'false' || normalizedValue === '0' || normalizedValue === 'no' || normalizedValue === 'off') return 'false';
            return normalizedValue;
        }

        function normalizePortOptionSignatureMap(options) {
            const normalized = normalizePortOptionMap(options);
            const signature = {};
            const seenBooleanKeys = new Set();

            Object.keys(normalized)
                .sort((a, b) => a.localeCompare(b))
                .forEach(key => {
                    if (isBooleanPortOptionKey(key)) {
                        signature[key] = normalizeBooleanPortOptionValue(normalized[key]);
                        seenBooleanKeys.add(normalizePropertyKeyName(key));
                        return;
                    }

                    signature[key] = normalized[key];
                });

            BOOLEAN_PORT_OPTION_CANONICAL_KEYS.forEach(key => {
                if (!seenBooleanKeys.has(normalizePropertyKeyName(key))) {
                    signature[key] = 'false';
                }
            });

            return signature;
        }

        function extractGroupMemberIds(nodeOrProps) {
            const props = nodeOrProps && nodeOrProps.properties ? nodeOrProps.properties : (nodeOrProps || {});
            const rawList = props.groupMemberIds || props.GroupMemberIds || '';
            const parsed = [];

            if (typeof rawList === 'string' && rawList.trim()) {
                try {
                    const value = JSON.parse(rawList);
                    if (Array.isArray(value)) {
                        value.forEach(v => {
                            if (v !== null && v !== undefined) parsed.push(String(v));
                        });
                    }
                } catch {
                    rawList.split(/[;,]/).forEach(v => {
                        if (v && v.trim()) parsed.push(v.trim());
                    });
                }
            } else if (Array.isArray(rawList)) {
                rawList.forEach(v => {
                    if (v !== null && v !== undefined) parsed.push(String(v));
                });
            }

            const fallbackId = props.id || props.Id || '';
            if (fallbackId) {
                parsed.push(String(fallbackId));
            }

            const unique = [];
            const seen = new Set();
            parsed.forEach(id => {
                const canonical = canonicalizeId(id);
                if (!canonical || seen.has(canonical)) return;
                seen.add(canonical);
                unique.push(id);
            });
            return unique;
        }

        function isGroupLikeNode(node) {
            if (!node) return false;

            const props = node.properties || {};
            const oldProps = node.propertiesOld || {};
            if (isTrueish(props.isGroup) || isTrueish(props.IsGroup) || isTrueish(oldProps.isGroup) || isTrueish(oldProps.IsGroup)) {
                return true;
            }

            const name = String(node.name || '').trim().toLowerCase();
            const nickname = String(node.nickname || '').trim().toLowerCase();
            if (name === 'group' || nickname === 'group') {
                return true;
            }

            const desc = String(props.description || props.Description || '').toLowerCase();
            return desc.includes('group of grasshopper objects');
        }

        function splitComponentAndGroupNodes(nodes) {
            const componentNodes = [];
            const groupNodes = [];

            (nodes || []).forEach(node => {
                if (isGroupLikeNode(node)) {
                    groupNodes.push(node);
                } else {
                    componentNodes.push(node);
                }
            });

            return { componentNodes, groupNodes };
        }

        function isScribbleLikeNode(node) {
            if (!node) return false;
            const name = String(node.name || '').trim().toLowerCase();
            const nickname = String(node.nickname || '').trim().toLowerCase();
            const props = node.properties || {};
            const desc = String(props.description || props.Description || '').trim().toLowerCase();
            return name === 'scribble' ||
                nickname === 'scribble' ||
                name.includes('scribble') ||
                nickname.includes('scribble') ||
                desc.includes('scribble');
        }

        function getScribbleText(node) {
            const props = node?.properties || {};
            const candidates = [
                props.ScribbleText,
                props.scribbleText,
                props.Text,
                props.text,
                props.UserText,
                props.userText,
                props.PanelContent,
                props.panelContent,
                node?.nickname,
                node?.name
            ];

            for (let i = 0; i < candidates.length; i++) {
                const raw = candidates[i];
                const value = String(raw || '').trim();
                if (!value) continue;
                if (value.toLowerCase() === 'scribble') continue;
                return value;
            }

            return 'Scribble';
        }

        function getScribbleFontFamily(node) {
            const props = node?.properties || {};
            const candidates = [
                props.ScribbleFontFamily,
                props.scribbleFontFamily,
                props.FontFamily,
                props.fontFamily,
                props.FontFace,
                props.fontFace,
                props.FontName,
                props.fontName,
                props.Font,
                props.font
            ];

            for (let i = 0; i < candidates.length; i++) {
                const raw = String(candidates[i] || '').trim();
                if (!raw) continue;

                const firstToken = raw.split(/[,;]/)[0]?.trim() || '';
                const cleaned = firstToken.replace(/^["']+|["']+$/g, '').trim();
                if (!cleaned) continue;
                return cleaned;
            }

            return '';
        }

        function getScribbleCanvasFont(node, sizePx) {
            const props = node?.properties || {};
            const styleHints = String(
                props.ScribbleFontStyle ||
                props.scribbleFontStyle ||
                props.FontStyle ||
                props.fontStyle ||
                ''
            ).toLowerCase();
            const fontStyle = (styleHints.includes('italic') || styleHints.includes('oblique')) ? 'italic ' : '';
            const fontWeight = styleHints.includes('bold') ? '700 ' : '';

            const preferredFamily = getScribbleFontFamily(node);
            const fallbackFamilies = ['"Segoe Print"', '"Bradley Hand"', '"Comic Sans MS"', '"Segoe UI"', 'sans-serif'];
            if (preferredFamily) {
                fallbackFamilies.unshift(`"${preferredFamily.replace(/"/g, '\\"')}"`);
            }

            return `${fontStyle}${fontWeight}${Math.max(1, Number(sizePx) || 12)}px ${fallbackFamilies.join(', ')}`;
        }

        function parseGroupColorRaw(raw, fallbackAlpha = 0.18) {
            const text = String(raw || '').trim();
            const nums = (text.match(/\d+/g) || []).map(v => Number.parseInt(v, 10)).filter(v => Number.isFinite(v));
            if (nums.length >= 4) {
                const a = Math.max(0, Math.min(255, nums[0])) / 255;
                const r = Math.max(0, Math.min(255, nums[1]));
                const g = Math.max(0, Math.min(255, nums[2]));
                const b = Math.max(0, Math.min(255, nums[3]));
                return { r, g, b, a };
            }
            if (nums.length >= 3) {
                return {
                    r: Math.max(0, Math.min(255, nums[0])),
                    g: Math.max(0, Math.min(255, nums[1])),
                    b: Math.max(0, Math.min(255, nums[2])),
                    a: fallbackAlpha
                };
            }
            return { r: 255, g: 255, b: 255, a: fallbackAlpha };
        }

        function buildRenderableGroups(groupNodes, componentNodes) {
            const groupMap = buildNodeLookupMap(groupNodes || []);
            const componentMap = buildNodeLookupMap(componentNodes || []);
            const resolvedCache = new Map();

            const statusOnSide = (node, side) => {
                const status = String(node?.status || node?.Status || 'same').toLowerCase();
                if (side === 'old') return status === 'added' ? 'hidden' : status;
                if (side === 'new') return status === 'removed' ? 'hidden' : status;
                return status;
            };

            const extractGroupMemberIdsForSide = (groupNode, side) => {
                if (!groupNode) return [];

                if (statusOnSide(groupNode, side) === 'hidden') {
                    return [];
                }

                const props = groupNode.properties || {};
                const propsOld = groupNode.propertiesOld || {};
                if (side === 'old') {
                    const oldRaw = propsOld.GroupMemberIds ?? propsOld.groupMemberIds;
                    if (oldRaw !== undefined && oldRaw !== null && String(oldRaw).trim() !== '') {
                        return extractGroupMemberIds({ GroupMemberIds: oldRaw });
                    }
                }

                return extractGroupMemberIds(props);
            };

            const resolveMembers = (groupNode, side, stack = new Set()) => {
                if (!groupNode) return [];
                const cacheKey = `${side}:${canonicalizeId(groupNode.id)}`;
                if (cacheKey && resolvedCache.has(cacheKey)) {
                    return resolvedCache.get(cacheKey);
                }
                if (cacheKey && stack.has(cacheKey)) return [];

                if (cacheKey) stack.add(cacheKey);
                const result = [];
                const seen = new Set();
                const members = extractGroupMemberIdsForSide(groupNode, side);
                members.forEach(memberId => {
                    const canonical = canonicalizeId(memberId);
                    if (!canonical) return;

                    const component = componentMap.get(memberId) || componentMap.get(canonical);
                    if (component && statusOnSide(component, side) !== 'hidden') {
                        const componentCanonical = canonicalizeId(component.id);
                        if (componentCanonical && !seen.has(componentCanonical)) {
                            seen.add(componentCanonical);
                            result.push(component.id);
                        }
                        return;
                    }

                    const nestedGroup = groupMap.get(memberId) || groupMap.get(canonical);
                    if (nestedGroup && statusOnSide(nestedGroup, side) !== 'hidden') {
                        const nestedMembers = resolveMembers(nestedGroup, side, stack);
                        nestedMembers.forEach(nestedId => {
                            const nestedCanonical = canonicalizeId(nestedId);
                            if (nestedCanonical && !seen.has(nestedCanonical)) {
                                seen.add(nestedCanonical);
                                result.push(nestedId);
                            }
                        });
                    }
                });

                if (cacheKey) {
                    stack.delete(cacheKey);
                    resolvedCache.set(cacheKey, result);
                }
                return result;
            };

            return (groupNodes || []).map(group => {
                const props = group.properties || {};
                const colorRaw = props.groupColor || props.GroupColor || props.colour || props.Colour || '';
                const memberIdsOld = resolveMembers(group, 'old');
                const memberIdsNew = resolveMembers(group, 'new');
                const memberIds = [];
                const seenMembers = new Set();
                [...memberIdsOld, ...memberIdsNew].forEach(id => {
                    const canonical = canonicalizeId(id);
                    if (!canonical || seenMembers.has(canonical)) return;
                    seenMembers.add(canonical);
                    memberIds.push(id);
                });
                const hasFallbackRect = Number.isFinite(group.x) && Number.isFinite(group.y) &&
                    Number.isFinite(group.w) && Number.isFinite(group.h) && group.w > 0 && group.h > 0;
                const hasAuthoredRect = !!(group.hasExplicitBounds || group.HasExplicitBounds);

                return {
                    id: group.id,
                    name: String(group.nickname || group.name || 'Group').trim() || 'Group',
                    status: group.status || 'same',
                    color: parseGroupColorRaw(colorRaw),
                    memberIdsOld,
                    memberIdsNew,
                    memberIds,
                    fallbackRect: hasFallbackRect ? { x: group.x, y: group.y, w: group.w, h: group.h } : null,
                    explicitRect: (hasFallbackRect && hasAuthoredRect) ? {
                        x: group.x,
                        y: group.y,
                        w: group.w,
                        h: group.h,
                        dx: Number.isFinite(group.dx) ? group.dx : 0,
                        dy: Number.isFinite(group.dy) ? group.dy : 0
                    } : null
                };
            }).filter(group => group.memberIds.length > 0 || !!group.fallbackRect);
        }

        const GROUP_MEMBERSHIP_BLEND_WINDOW = 0.10;

        function getGroupMemberIdsForMix(group, mix = state.versionMix) {
            if (!group) return [];
            const oldMembers = Array.isArray(group.memberIdsOld) ? group.memberIdsOld : [];
            const newMembers = Array.isArray(group.memberIdsNew) ? group.memberIdsNew : [];
            if (mix <= 0.001) return oldMembers;
            if (mix >= 0.999) return newMembers;

            const oldMap = new Map();
            const newMap = new Map();
            oldMembers.forEach(id => {
                const canonical = canonicalizeId(id);
                if (!canonical || oldMap.has(canonical)) return;
                oldMap.set(canonical, id);
            });
            newMembers.forEach(id => {
                const canonical = canonicalizeId(id);
                if (!canonical || newMap.has(canonical)) return;
                newMap.set(canonical, id);
            });

            const result = [];
            const includeOldOnly = mix <= GROUP_MEMBERSHIP_BLEND_WINDOW;
            const includeNewOnly = mix >= (1 - GROUP_MEMBERSHIP_BLEND_WINDOW);
            const keys = new Set([...oldMap.keys(), ...newMap.keys()]);
            keys.forEach(key => {
                const inOld = oldMap.has(key);
                const inNew = newMap.has(key);
                if (inOld && inNew) {
                    result.push(newMap.get(key) || oldMap.get(key));
                    return;
                }
                if (inOld && includeOldOnly) {
                    result.push(oldMap.get(key));
                    return;
                }
                if (inNew && includeNewOnly) {
                    result.push(newMap.get(key));
                }
            });

            return result.filter(Boolean);
        }

        function normalizePorts(ports, kind, nodeId) {
            if (!Array.isArray(ports)) return [];

            return ports.map((p, index) => {
                const id = p?.id || p?.Id || `${nodeId}:${kind}:${index}`;
                const options = normalizePortOptionMap(p?.options ?? p?.Options);
                const optionsOld = normalizePortOptionMap(p?.optionsOld ?? p?.OptionsOld);
                const optionsNew = normalizePortOptionMap(p?.optionsNew ?? p?.OptionsNew);
                return {
                    ...p,
                    id,
                    name: p?.name || p?.Name || '',
                    nickname: p?.nickname || p?.Nickname || '',
                    kind: p?.kind || p?.Kind || kind,
                    status: p?.status || p?.Status || 'same',
                    options,
                    optionsOld,
                    optionsNew,
                    optionsChanged: !!(p?.optionsChanged ?? p?.OptionsChanged) || Object.keys(optionsOld).length > 0 || Object.keys(optionsNew).length > 0,
                    wireDisplay: parseWireDisplayMode(p?.wireDisplay ?? p?.WireDisplay ?? p?.wireDisplayMode ?? p?.WireDisplayMode)
                };
            });
        }

        function parseWireDisplayMode(raw) {
            if (raw === null || raw === undefined) return 0;

            if (typeof raw === 'number') {
                return Math.max(0, Math.min(2, Math.trunc(raw)));
            }

            const text = String(raw).trim().toLowerCase();
            if (!text) return 0;

            if (text === '0' || text.includes('default') || text.includes('full')) return 0;
            if (text === '1' || text.includes('faint')) return 1;
            if (text === '2' || text.includes('hidden')) return 2;

            const numeric = Number.parseInt(text, 10);
            if (Number.isFinite(numeric)) {
                return Math.max(0, Math.min(2, numeric));
            }

            return 0;
        }

        function normalizeEdge(e) {
            const source = e.source || e.Source || e.from || e.From || e.start || e.Start || '';
            const target = e.target || e.Target || e.to || e.To || e.end || e.End || '';
            return {
                ...e,
                source,
                target,
                sourcePort: e.sourcePort || e.SourcePort,
                targetPort: e.targetPort || e.TargetPort,
                status: e.status || e.Status || 'same'
            };
        }

        function canonicalizeId(value) {
            if (value === null || value === undefined) return '';
            return String(value).trim().replace(/[{}]/g, '').toLowerCase();
        }

        function canonicalizePortId(value) {
            if (value === null || value === undefined) return '';
            return String(value).trim().replace(/[{}]/g, '').toLowerCase();
        }

        function buildNodeLookupMap(nodes) {
            const map = new Map();
            nodes.forEach(n => {
                map.set(n.id, n);
                const canonical = canonicalizeId(n.id);
                if (canonical && !map.has(canonical)) {
                    map.set(canonical, n);
                }
            });
            return map;
        }

        function findPortIndex(ports, portId) {
            if (!Array.isArray(ports) || ports.length === 0 || !portId) return -1;
            const targetId = canonicalizePortId(portId);
            if (!targetId) return -1;

            for (let i = 0; i < ports.length; i++) {
                const port = ports[i];
                const currentId = port?.id || port?.Id || '';
                if (canonicalizePortId(currentId) === targetId) {
                    return i;
                }
            }

            return -1;
        }

        function getNodePortAnchor(node, side, portId) {
            const isOutput = side === 'output';
            const x = isOutput ? node.x + node.w : node.x;
            const ports = isOutput
                ? (node.outputs || node.Outputs || [])
                : (node.inputs || node.Inputs || []);
            const index = findPortIndex(ports, portId);

            if (index < 0 || ports.length === 0) {
                return { x, y: node.y + node.h / 2 };
            }

            const step = node.h / (ports.length + 1);
            return { x, y: node.y + (index + 1) * step };
        }

        function getEdgeWireDisplayMode(edge, targetNode) {
            if (!edge || !targetNode) return 0;

            const inputs = targetNode.inputs || targetNode.Inputs || [];
            const index = findPortIndex(inputs, edge.targetPort);
            if (index < 0 || index >= inputs.length) return 0;

            const port = inputs[index] || {};
            return parseWireDisplayMode(port.wireDisplay ?? port.WireDisplay ?? port.wireDisplayMode ?? port.WireDisplayMode);
        }

        function getPortStatusColor(status) {
            const key = String(status || 'same').trim().toLowerCase();
            if (key === 'added') return CONFIG.colors.added;
            if (key === 'removed') return CONFIG.colors.removed;
            if (key === 'modified') return CONFIG.colors.modified;
            return CONFIG.colors.textMuted;
        }

        function getPortDisplayLabel(port, index, kind, nodeId) {
            const explicit = String(port?.nickname || port?.Nickname || port?.name || port?.Name || '').trim();
            if (explicit) return explicit;

            const rawId = String(port?.id || port?.Id || '').trim();
            const generatedPrefix = `${nodeId}:${kind}:`;
            if (rawId && !rawId.startsWith(generatedPrefix)) return rawId;

            return `${kind === 'input' ? 'In' : 'Out'} ${index + 1}`;
        }

        function drawNodePortSockets(node, collectLabelsOnly = false, labelsToCollect = null) {
            if (!state.showPortDetails || !node) return;

            const inputs = Array.isArray(node.inputs) ? node.inputs : (Array.isArray(node.Inputs) ? node.Inputs : []);
            const outputs = Array.isArray(node.outputs) ? node.outputs : (Array.isArray(node.Outputs) ? node.Outputs : []);
            if (inputs.length === 0 && outputs.length === 0) return;

            const radius = Math.max(2.4 / state.view.scale, 1.4);
            const borderWidth = Math.max(1 / state.view.scale, 0.55);
            const showLabels = state.view.scale >= 1.2;
            // Screen size = clamp(11, 12*scale, 14) px; world size = screen / scale
            const screenPx = Math.max(11, Math.min(12 * state.view.scale, 14));
            const labelFontSize = screenPx / state.view.scale;

            const drawSide = (ports, isOutput) => {
                if (!Array.isArray(ports) || ports.length === 0) return;
                const x = isOutput ? node.x + node.w : node.x;
                const step = node.h / (ports.length + 1);

                ports.forEach((port, index) => {
                    const y = node.y + (index + 1) * step;
                    
                    if (!collectLabelsOnly) {
                        ctx.beginPath();
                        ctx.arc(x, y, radius, 0, Math.PI * 2);
                        ctx.fillStyle = getPortStatusColor(port?.status || port?.Status);
                        ctx.fill();
                        ctx.lineWidth = borderWidth;
                        ctx.strokeStyle = '#0f1115';
                        ctx.stroke();
                    }

                    if (showLabels && labelsToCollect) {
                        const rawLabel = getPortDisplayLabel(port, index, isOutput ? 'output' : 'input', node.id);
                        
                        // Measure in screen space with screen font
                        ctx.font = `${screenPx}px "IBM Plex Mono", ui-monospace, monospace`;
                        const maxScreenWidth = 80; // px on screen
                        let displayLabel = rawLabel;
                        if (ctx.measureText(displayLabel).width > maxScreenWidth) {
                            while (displayLabel.length > 1 && ctx.measureText(displayLabel + '…').width > maxScreenWidth) {
                                displayLabel = displayLabel.slice(0, -1);
                            }
                            displayLabel += '…';
                        }
                        
                        const textWidth = ctx.measureText(displayLabel).width;
                        const gap = 4; // screen pixels
                        
                        // Compute screen coordinates
                        const worldX = isOutput ? node.x + node.w : node.x;
                        const worldY = y;
                        const screenPortX = (worldX * state.view.scale) + state.view.x;
                        const screenPortY = (worldY * state.view.scale) + state.view.y;
                        
                        const screenX = isOutput ? (screenPortX + gap) : (screenPortX - gap - textWidth);
                        const screenY = screenPortY - (screenPx / 2);
                        
                        const padding = 2; // screen pixels
                        
                        labelsToCollect.push({
                            node,
                            text: displayLabel,
                            screenX,
                            screenY,
                            screenW: textWidth,
                            screenH: screenPx,
                            screenFontSize: screenPx,
                            isOutput,
                            screenRect: { x: screenX - padding, y: screenY - padding, w: textWidth + padding * 2, h: screenPx + padding * 2 },
                            priority: (state.selected === node.id || state.hovered === node.id) ? 1 : 0
                        });
                    }
                });
            };

            ctx.save();
            drawSide(inputs, false);
            drawSide(outputs, true);
            ctx.restore();
        }

        function getWireStyle(mode, isConnected = false) {
            if (mode === 2) {
                if (!isConnected) {
                    return { hidden: true, widthFactor: 0, alphaFactor: 0 };
                }
                return { hidden: false, widthFactor: 0.9, alphaFactor: 0.95 };
            }

            if (mode === 1) {
                return {
                    hidden: false,
                    widthFactor: isConnected ? 0.58 : 0.46,
                    alphaFactor: isConnected ? 0.62 : 0.2
                };
            }

            return { hidden: false, widthFactor: 1.0, alphaFactor: 1.0 };
        }

        function wireDisplayModeLabel(raw) {
            const mode = parseWireDisplayMode(raw);
            if (mode === 1) return 'faint';
            if (mode === 2) return 'hidden';
            return 'full';
        }

        function waitFrame() {
            return new Promise(resolve => requestAnimationFrame(resolve));
        }

        let drawFrameId = 0;
        let drawRequested = false;

        function scheduleDraw() {
            drawRequested = true;
            if (drawFrameId) return;

            drawFrameId = requestAnimationFrame(() => {
                drawFrameId = 0;
                if (!drawRequested) return;
                drawRequested = false;
                draw();
            });
        }

        function showPerf(message) {
            if (!perfIndicator) return;
            if (!message) {
                perfIndicator.style.display = 'none';
                return;
            }
            perfIndicator.textContent = message;
            perfIndicator.style.display = 'block';
        }

        function updateBounds() {
            if (state.nodes.length === 0) return;

            let minX = Infinity;
            let maxX = -Infinity;
            let minY = Infinity;
            let maxY = -Infinity;

            for (let i = 0; i < state.nodes.length; i++) {
                const n = state.nodes[i];
                if (!Number.isFinite(n.x) || !Number.isFinite(n.y)) continue;

                if (n.x < minX) minX = n.x;
                if (n.x + n.w > maxX) maxX = n.x + n.w;
                if (n.y < minY) minY = n.y;
                if (n.y + n.h > maxY) maxY = n.y + n.h;
            }

            if (!Number.isFinite(minX) || !Number.isFinite(minY) || !Number.isFinite(maxX) || !Number.isFinite(maxY)) {
                state.bounds.minX = 0;
                state.bounds.maxX = 0;
                state.bounds.minY = 0;
                state.bounds.maxY = 0;
                return;
            }

            state.bounds.minX = minX;
            state.bounds.maxX = maxX;
            state.bounds.minY = minY;
            state.bounds.maxY = maxY;
        }

        function setupEventListeners() {
            window.addEventListener('resize', () => {
                if (enforceResponsivePanelState()) {
                    applyPanelVisibility();
                    return;
                }
                resize();
            });

            // Canvas interactions
            canvas.addEventListener('mousedown', onMouseDown);
            window.addEventListener('mousemove', onMouseMove);
            window.addEventListener('mouseup', onMouseUp);
            canvas.addEventListener('wheel', onWheel, { passive: false });
            canvas.addEventListener('click', onClick);
            canvas.addEventListener('dblclick', onDoubleClick);

            // Minimap interactions
            minimap.addEventListener('mousedown', onMinimapDown);
            minimap.addEventListener('mousemove', onMinimapMove);
            minimap.addEventListener('mouseup', () => state.minimapDragging = false);

            // Keyboard shortcuts
            window.addEventListener('keydown', (e) => {
                const key = String(e.key || '').toLowerCase();
                const targetTag = String(e.target?.tagName || '').toUpperCase();
                const isTypingTarget = targetTag === 'INPUT' ||
                    targetTag === 'TEXTAREA' ||
                    !!e.target?.isContentEditable;
                const isSpotlightShortcut = (e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && key === 'k';

                if (isScriptModalOpen()) {
                    if (key === 'escape') {
                        e.preventDefault();
                        closeExpandedScriptDiff();
                    }
                    return;
                }

                if (isSpotlightShortcut) {
                    e.preventDefault();
                    toggleSpotlight();
                    return;
                }

                if (state.spotlightOpen) {
                    if (key === 'escape') {
                        e.preventDefault();
                        closeSpotlight();
                    }
                    return;
                }

                if (isTypingTarget) return;

                switch (key) {
                    case 'f': fitView(); break;
                    case 'j':
                    case 'arrowdown': navigateToNext(); break;
                    case 'k':
                    case 'arrowup': navigateToPrevious(); break;
                    case 'escape':
                        state.selected = null;
                        updateDetailsPanel(null);
                        renderComponentResultsList();
                        scheduleDraw();
                        break;
                    case '=':
                    case '+': zoom(1.2); break;
                    case '-': zoom(0.8); break;
                }
            });

            // Controls
            if (searchLauncher) {
                searchLauncher.addEventListener('click', () => openSpotlight());
            }

            if (spotlightBackdrop) {
                spotlightBackdrop.addEventListener('click', (e) => {
                    if (e.target === spotlightBackdrop) {
                        closeSpotlight();
                    }
                });
            }

            if (spotlightInput) {
                spotlightInput.addEventListener('input', (e) => {
                    applySearchQuery(e.target.value, { refreshView: true, syncInput: false });
                });

                spotlightInput.addEventListener('keydown', (e) => {
                    switch (String(e.key || '').toLowerCase()) {
                        case 'arrowdown':
                            e.preventDefault();
                            moveSpotlightSelection(1);
                            break;
                        case 'arrowup':
                            e.preventDefault();
                            moveSpotlightSelection(-1);
                            break;
                        case 'enter':
                            e.preventDefault();
                            activateSpotlightSelection();
                            break;
                        case 'escape':
                            e.preventDefault();
                            closeSpotlight();
                            break;
                    }
                });
            }

            if (spotlightResults) {
                spotlightResults.addEventListener('click', (e) => {
                    const trigger = e.target.closest('[data-spotlight-index]');
                    if (!trigger) return;
                    const index = Number.parseInt(trigger.dataset.spotlightIndex || '0', 10);
                    if (!Number.isFinite(index)) return;
                    activateSpotlightSelection(index);
                });
            }

            document.querySelectorAll('.filter-btn[data-filter]').forEach(btn => {
                btn.addEventListener('click', (e) => {
                    const key = e.target.dataset.filter;
                    if (!key) return;

                    if (key === 'all') {
                        state.filterMode = 'all';
                        state.filterStatuses = new Set(CHANGED_STATUS_FILTERS);
                    } else {
                        if (state.filterMode === 'all') {
                            state.filterMode = 'custom';
                            state.filterStatuses = new Set([key]);
                        } else if (state.filterStatuses.has(key)) {
                            state.filterStatuses.delete(key);
                            if (state.filterStatuses.size === 0) {
                                state.filterMode = 'all';
                                state.filterStatuses = new Set(CHANGED_STATUS_FILTERS);
                            }
                        } else {
                            state.filterStatuses.add(key);
                        }
                    }

                    syncStatusFilterButtons();
                    applySearchQuery(state.search, { refreshView: false, syncInput: false });
                    renderComponentResultsList();
                    scheduleDraw();
                });
            });
            syncStatusFilterButtons();
            renderComponentResultsList();

            // Navigation controls
            const btnNextChange = document.getElementById('btn-next-change');
            const btnPrevChange = document.getElementById('btn-prev-change');
            
            if (btnNextChange) {
                btnNextChange.addEventListener('click', () => navigateToNext());
            }
            
            if (btnPrevChange) {
                btnPrevChange.addEventListener('click', () => navigateToPrevious());
            }

            if (componentSortSelect) {
                componentSortSelect.value = state.componentSort || 'risk';
                componentSortSelect.addEventListener('change', (e) => {
                    const nextSort = String(e.target.value || 'risk').toLowerCase();
                    state.componentSort = nextSort;
                    renderComponentResultsList();
                });
            }

            const togglePorts = document.getElementById('toggle-ports');
            if (togglePorts) {
                state.showPortDetails = !!togglePorts.checked;
                togglePorts.addEventListener('change', () => {
                    state.showPortDetails = !!togglePorts.checked;
                    applyPortDetailsVisibility();
                    scheduleDraw();
                });
                togglePorts.addEventListener('input', () => {
                    state.showPortDetails = !!togglePorts.checked;
                    applyPortDetailsVisibility();
                    scheduleDraw();
                });
            }

            const toggleGroups = document.getElementById('toggle-groups');
            if (toggleGroups) {
                state.showGroups = !!toggleGroups.checked;
                toggleGroups.addEventListener('change', () => {
                    state.showGroups = !!toggleGroups.checked;
                    scheduleDraw();
                });
                toggleGroups.addEventListener('input', () => {
                    state.showGroups = !!toggleGroups.checked;
                    scheduleDraw();
                });
            }

            if (toggleHighLevelMods) {
                toggleHighLevelMods.checked = !!state.includeHighLevelModifications;

                const applyHighLevelModeToggle = () => {
                    state.includeHighLevelModifications = !!toggleHighLevelMods.checked;
                    applyModificationStatusMode();
                };

                toggleHighLevelMods.addEventListener('change', applyHighLevelModeToggle);
                toggleHighLevelMods.addEventListener('input', applyHighLevelModeToggle);
            }

            const toggleChangedEdges = document.getElementById('toggle-changed-edges');
            if (toggleChangedEdges) {
                toggleChangedEdges.addEventListener('change', (e) => {
                    state.showChangedEdgesOnly = !!e.target.checked;
                    scheduleDraw();
                });
            }

            const exportBtn = document.getElementById('btn-export-forensic');
            if (exportBtn) exportBtn.addEventListener('click', exportForensicReport);

            const saveBaselineBtn = document.getElementById('btn-save-baseline');
            if (saveBaselineBtn) saveBaselineBtn.addEventListener('click', saveBaselinePrompt);

            const checkBaselineBtn = document.getElementById('btn-check-baseline');
            if (checkBaselineBtn) checkBaselineBtn.addEventListener('click', checkBaselinePrompt);

            document.getElementById('btn-fit').addEventListener('click', fitView);
            document.getElementById('btn-zoom-in').addEventListener('click', () => zoom(1.2));
            document.getElementById('btn-zoom-out').addEventListener('click', () => zoom(0.8));

            if (btnSidebarCollapse) btnSidebarCollapse.addEventListener('click', toggleSidebar);
            if (btnSidebarExpand) btnSidebarExpand.addEventListener('click', toggleSidebar);
            if (btnDetailsCollapse) btnDetailsCollapse.addEventListener('click', toggleDetailsPanel);
            if (btnDetailsExpand) btnDetailsExpand.addEventListener('click', toggleDetailsPanel);

            if (metaFileOldElem) {
                metaFileOldElem.addEventListener('click', () => openSourceModal('old'));
            }
            if (metaFileNewElem) {
                metaFileNewElem.addEventListener('click', () => openSourceModal('new'));
            }

            if (sourceModeFileBtn) {
                sourceModeFileBtn.addEventListener('click', () => {
                    state.sourceModal.mode = 'file';
                    renderSourceModal();
                    setSourceModalStatus('', 'muted');
                });
            }

            if (sourceModeCommitBtn) {
                sourceModeCommitBtn.addEventListener('click', () => {
                    if (state.sourceModal.commitAvailable === false) {
                        setSourceModalStatus('No commit history available for this file.', 'warn');
                        return;
                    }
                    state.sourceModal.mode = 'commit';
                    renderSourceModal();
                    setSourceModalStatus('', 'muted');
                });
            }

            if (btnSourcePickFile) {
                btnSourcePickFile.addEventListener('click', async () => {
                    const result = await pickNativeFilePath();
                    if (!result.path) {
                        if (result.error && !String(result.error).toLowerCase().includes('cancel')) {
                            setSourceModalStatus(result.error, 'error');
                        }
                        return;
                    }
                    state.sourceModal.filePath = result.path;
                    state.sourceModal.commitPath = result.path;
                    const { commits } = await fetchCommitsForPath(result.path, { silent: true });
                    state.sourceModal.commitAvailable = commits.length > 0;
                    if (state.sourceModal.mode === 'commit' && !state.sourceModal.commitAvailable) {
                        state.sourceModal.mode = 'file';
                    }
                    renderSourceModal();
                    setSourceModalStatus(
                        state.sourceModal.commitAvailable
                            ? 'File selected. Git history available.'
                            : 'File selected.',
                        'info'
                    );
                });
            }

            if (btnSourcePickCommitFile) {
                btnSourcePickCommitFile.addEventListener('click', async () => {
                    const result = await pickNativeFilePath();
                    if (!result.path) {
                        if (result.error && !String(result.error).toLowerCase().includes('cancel')) {
                            setSourceModalStatus(result.error, 'error');
                        }
                        return;
                    }
                    state.sourceModal.commitPath = result.path;
                    const { commits } = await fetchCommitsForPath(result.path, { silent: true });
                    state.sourceModal.commitAvailable = commits.length > 0;
                    state.sourceModal.commits = commits;
                    state.sourceModal.selectedCommit = commits.length ? commits[0].hash : null;
                    if (!state.sourceModal.commitAvailable) {
                        state.sourceModal.mode = 'file';
                    }
                    renderSourceModal();
                    setSourceModalStatus(
                        state.sourceModal.commitAvailable
                            ? 'File selected. Commit history loaded.'
                            : 'No commit history found for this file.',
                        state.sourceModal.commitAvailable ? 'info' : 'warn'
                    );
                });
            }

            if (btnSourceLoadCommits) {
                btnSourceLoadCommits.addEventListener('click', () => loadSourceModalCommits());
            }

            if (btnSourceApply) {
                btnSourceApply.addEventListener('click', () => applySourceModalSelection());
            }

            if (btnSourceCancel) {
                btnSourceCancel.addEventListener('click', closeSourceModal);
            }

            if (btnSourceModalClose) {
                btnSourceModalClose.addEventListener('click', closeSourceModal);
            }

            if (sourceModal) {
                sourceModal.addEventListener('click', (e) => {
                    if (e.target === sourceModal) {
                        closeSourceModal();
                    }
                });
            }

            if (btnScriptModalClose) {
                btnScriptModalClose.addEventListener('click', () => closeExpandedScriptDiff());
            }

            if (scriptModal) {
                scriptModal.addEventListener('click', (e) => {
                    if (e.target === scriptModal) {
                        closeExpandedScriptDiff();
                    }
                });
            }

            if (componentResultsList) {
                componentResultsList.addEventListener('click', (e) => {
                    const trigger = e.target.closest('.component-result-item');
                    if (!trigger) return;
                    zoomToNode(trigger.dataset.nodeId);
                });
            }

            // Wire Slider
            const wireSlider = document.getElementById('wire-slider');
            const wireLabel = document.getElementById('wire-label');

            function applyWireThreshold(val) {
                if (!Number.isFinite(val)) {
                    state.wireThreshold = Infinity;
                    wireLabel.textContent = "Show All";
                    return;
                }

                if (val === 0) {
                    state.wireThreshold = Infinity; // Show all
                    wireLabel.textContent = "Show All";
                } else if (val === 100) {
                    state.wireThreshold = 0; // Hide all (except connected)
                    wireLabel.textContent = "Hide All";
                } else {
                    // Non-linear mapping for smoother control
                    // val 0-100 -> threshold 20000-100
                    const factor = (100 - val) / 100;
                    state.wireThreshold = 20000 * (factor * factor);

                    if (state.wireThreshold < 100) state.wireThreshold = 100;
                    wireLabel.textContent = `> ${Math.round(state.wireThreshold)}px Hidden`;
                }
            }

            wireSlider.addEventListener('input', (e) => {
                applyWireThreshold(parseInt(e.target.value, 10));
                scheduleDraw();
            });

            // Force predictable defaults on load (browser/webview can restore prior input states).
            wireSlider.value = '0';
            applyWireThreshold(parseInt(wireSlider.value, 10));

            // Version Slider
            const verSlider = document.getElementById('version-slider');
            const verLabel = document.getElementById('version-label');

            verSlider.value = '50';
            state.versionMix = 0.5;
            verLabel.textContent = "Mixed";

            if (toggleChangedEdges) {
                toggleChangedEdges.checked = false;
                state.showChangedEdgesOnly = false;
            }

            verSlider.addEventListener('input', (e) => {
                const val = parseInt(e.target.value);
                // 0 = Old (left), 100 = New (right)
                state.versionMix = val / 100.0;

                if (val === 0) verLabel.textContent = "Old Only";
                else if (val === 100) verLabel.textContent = "New Only";
                else if (val === 50) verLabel.textContent = "Mixed";
                else verLabel.textContent = `${100 - val}% Old`;

                if (state.componentSort === 'dist') {
                    renderComponentResultsList();
                }
                scheduleDraw();
            });

            applyPanelVisibility();
        }

        function resize() {
            canvas.width = canvas.parentElement.clientWidth;
            canvas.height = canvas.parentElement.clientHeight;

            const miniContainer = document.getElementById('minimap-container');
            minimap.width = miniContainer.clientWidth;
            minimap.height = miniContainer.clientHeight;

            scheduleDraw();
        }

        function fitView() {
            updateBounds();
            if (state.nodes.length === 0) return;

            const padding = 50;
            const w = state.bounds.maxX - state.bounds.minX;
            const h = state.bounds.maxY - state.bounds.minY;

            // Prevent division by zero
            if (w === 0 || h === 0) return;

            const scaleX = (canvas.width - padding * 2) / w;
            const scaleY = (canvas.height - padding * 2) / h;

            state.view.scale = Math.min(scaleX, scaleY, 1); // Cap scale at 1
            state.view.x = (canvas.width - w * state.view.scale) / 2 - state.bounds.minX * state.view.scale;
            state.view.y = (canvas.height - h * state.view.scale) / 2 - state.bounds.minY * state.view.scale;

            scheduleDraw();
        }

        function zoom(factor) {
            const cx = canvas.width / 2;
            const cy = canvas.height / 2;

            const newScale = state.view.scale * factor;
            state.view.x = cx - (cx - state.view.x) * (newScale / state.view.scale);
            state.view.y = cy - (cy - state.view.y) * (newScale / state.view.scale);
            state.view.scale = newScale;

            scheduleDraw();
        }

        // --- Drawing ---

        function loop() {
            // Animation loop if needed later
            // requestAnimationFrame(loop);
        }

        function draw() {
            // Clear
            ctx.fillStyle = CONFIG.colors.surface;
            ctx.fillRect(0, 0, canvas.width, canvas.height);

            // Transform
            ctx.save();
            ctx.translate(state.view.x, state.view.y);
            ctx.scale(state.view.scale, state.view.scale);

            // Pre-calculate rendered positions for all nodes
            const mix = state.versionMix;
            const renderNodes = new Map();

            state.nodes.forEach(n => {
                // Lerp position: 
                // If mix=1 (New), use n.x (New Pos)
                // If mix=0 (Old), use n.x - n.dx (Old Pos)
                // Interpolated = n.x - n.dx * (1 - mix)
                // We overwrite x/y in this temporary object so downstream functions (like drawMinimap) work automatically
                const rx = n.x - (n.dx || 0) * (1 - mix);
                const ry = n.y - (n.dy || 0) * (1 - mix);

                renderNodes.set(n.id, {
                    ...n,
                    x: rx,
                    y: ry,
                    // Interpolate alpha based on status
                    alpha: (n.status === 'added' ? mix :
                        (n.status === 'removed' ? (1.0 - mix) : 1.0))
                });
            });

            const viewLeft = (-state.view.x / state.view.scale) - 200;
            const viewTop = (-state.view.y / state.view.scale) - 200;
            const viewRight = ((canvas.width - state.view.x) / state.view.scale) + 200;
            const viewBottom = ((canvas.height - state.view.y) / state.view.scale) + 200;
            const renderNodeLookup = buildNodeLookupMap(Array.from(renderNodes.values()));

            // Determine visibility/dimming
            const nodesToDraw = [];
            renderNodes.forEach(n => {
                if (state.largeModelMode && !state.selected) {
                    const outside = n.x + n.w < viewLeft || n.x > viewRight || n.y + n.h < viewTop || n.y > viewBottom;
                    if (outside) return;
                }

                const matchesFilter = doesNodeMatchStatusFilter(n.status) &&
                    nodeMatchesSearch(n);

                nodesToDraw.push({ node: n, dimmed: !matchesFilter, alpha: n.alpha });
            });

            const matchingNodeSet = new Set(nodesToDraw.filter(n => !n.dimmed).map(n => n.node.id));
            const visibleNodeSet = new Set(nodesToDraw.filter(n => !n.dimmed && n.alpha > 0.05).map(n => n.node.id));

            // Connection Highlighting Logic
            const connectedIds = new Set();
            if (state.selected) {
                connectedIds.add(state.selected);
                state.edges.forEach(e => {
                    if (e.source === state.selected) connectedIds.add(e.target);
                    if (e.target === state.selected) connectedIds.add(e.source);
                });
            }

            drawGroupBackgrounds(renderNodeLookup, viewLeft, viewTop, viewRight, viewBottom, mix, connectedIds, matchingNodeSet);

            // Draw Edges
            state.edges.forEach(e => {
                if (state.showChangedEdgesOnly && e.status === 'same') return;

                const sourceNode = renderNodes.get(e.source);
                const targetNode = renderNodes.get(e.target);

                if (!sourceNode || !targetNode) return;

                const wireMode = getEdgeWireDisplayMode(e, targetNode);
                const isConnected = !!state.selected && (e.source === state.selected || e.target === state.selected);
                const wireStyle = getWireStyle(wireMode, isConnected);
                if (wireStyle.hidden) return;

                if (state.largeModelMode && !state.selected) {
                    const sourceOff = sourceNode.x + sourceNode.w < viewLeft || sourceNode.x > viewRight || sourceNode.y + sourceNode.h < viewTop || sourceNode.y > viewBottom;
                    const targetOff = targetNode.x + targetNode.w < viewLeft || targetNode.x > viewRight || targetNode.y + targetNode.h < viewTop || targetNode.y > viewBottom;
                    if (sourceOff && targetOff) return;
                }

                const sourceDimmed = !visibleNodeSet.has(e.source);
                const targetDimmed = !visibleNodeSet.has(e.target);
                let isDimmed = sourceDimmed || targetDimmed;

                const start = getNodePortAnchor(sourceNode, 'output', e.sourcePort);
                const end = getNodePortAnchor(targetNode, 'input', e.targetPort);

                // Long wire logic
                const dx = end.x - start.x;
                const dy = end.y - start.y;
                const dist = Math.sqrt(dx * dx + dy * dy);
                const isLong = dist > state.wireThreshold;

                if (isLong && !isConnected) return;

                // Highlight logic
                if (state.selected) {
                    if (isConnected) {
                        isDimmed = false; // Force visible
                        ctx.lineWidth = Math.max(1.2 / state.view.scale, 0.8) * wireStyle.widthFactor;
                        ctx.globalAlpha = 1.0;
                    } else {
                        isDimmed = true; // Dim unrelated
                        ctx.lineWidth = Math.max(0.9 / state.view.scale, 0.65) * wireStyle.widthFactor;
                        ctx.globalAlpha = 0.18;
                    }
                } else {
                    ctx.lineWidth = Math.max(1.0 / state.view.scale, 0.7) * wireStyle.widthFactor;
                    ctx.globalAlpha = isDimmed ? 0.28 : 0.9;
                }

                // Apply version alpha to edges too
                let edgeAlpha = 1.0;
                if (e.status === 'added') edgeAlpha = mix;
                if (e.status === 'removed') edgeAlpha = 1.0 - mix;

                ctx.globalAlpha *= edgeAlpha * wireStyle.alphaFactor;
                if (ctx.globalAlpha < 0.01) return; // Skip invisible

                ctx.beginPath();
                ctx.moveTo(start.x, start.y);
                ctx.lineCap = 'round';
                ctx.lineJoin = 'round';

                // Bezier curve
                const cp1 = { x: start.x + (end.x - start.x) / 2, y: start.y };
                const cp2 = { x: end.x - (end.x - start.x) / 2, y: end.y };
                ctx.bezierCurveTo(cp1.x, cp1.y, cp2.x, cp2.y, end.x, end.y);

                ctx.strokeStyle = CONFIG.colors.edgeSame;
                if (e.status === 'added') ctx.strokeStyle = CONFIG.colors.added;
                if (e.status === 'removed') ctx.strokeStyle = CONFIG.colors.removed;

                // Override color if selected
                if (isConnected) ctx.strokeStyle = CONFIG.colors.selection;

                ctx.stroke();
                ctx.globalAlpha = 1.0;
            });

            // Draw Nodes
            nodesToDraw.forEach(item => {
                const n = item.node;
                let isDimmed = item.dimmed;

                if (state.selected) {
                    if (connectedIds.has(n.id)) {
                        isDimmed = false;
                    } else {
                        isDimmed = true;
                    }
                }

                ctx.globalAlpha = (isDimmed ? 0.1 : 1.0) * item.alpha;
                if (ctx.globalAlpha < 0.01) return; // Skip invisible

                const isScribble = isScribbleLikeNode(n);
                if (isScribble && state.view.scale >= 0.5) {
                    const scribbleText = getScribbleText(n);
                    const maxWidth = Math.max(30, n.w - 8);
                    // Screen size = clamp(11, 12*scale, 14) px; world size = screen / scale
                    const screenPx = Math.max(11, Math.min(13 * state.view.scale, 14));
                    const fontSize = screenPx / state.view.scale;
                    const lineHeight = fontSize * 1.18;

                    ctx.fillStyle = CONFIG.colors[n.status] || CONFIG.colors.text;
                    ctx.font = getScribbleCanvasFont(n, fontSize);
                    ctx.textBaseline = 'top';
                    ctx.textAlign = 'left';

                    const lines = String(scribbleText)
                        .split(/\r?\n/)
                        .flatMap(line => {
                            if (!line) return [''];
                            const words = line.split(/\s+/);
                            const wrapped = [];
                            let current = '';
                            words.forEach(word => {
                                const trial = current ? `${current} ${word}` : word;
                                if (ctx.measureText(trial).width > maxWidth && current) {
                                    wrapped.push(current);
                                    current = word;
                                } else {
                                    current = trial;
                                }
                            });
                            if (current) wrapped.push(current);
                            return wrapped.length ? wrapped : [''];
                        });

                    const maxLines = Math.max(1, Math.floor((Math.max(18, n.h) - 4) / lineHeight));
                    const finalLines = lines.slice(0, maxLines);
                    finalLines.forEach((line, idx) => {
                        const text = (idx === maxLines - 1 && lines.length > maxLines && line.length > 1)
                            ? `${line.slice(0, Math.max(1, line.length - 1))}…`
                            : line;
                        ctx.fillText(text, n.x + 4, n.y + 2 + (idx * lineHeight));
                    });

                    if (state.selected === n.id) {
                        ctx.save();
                        ctx.strokeStyle = CONFIG.colors.selection;
                        ctx.lineWidth = 1.5 / state.view.scale;
                        ctx.setLineDash([5 / state.view.scale, 4 / state.view.scale]);
                        roundRect(ctx, n.x, n.y, n.w, n.h, 4 / state.view.scale);
                        ctx.stroke();
                        ctx.setLineDash([]);
                        ctx.restore();
                    }

                    ctx.globalAlpha = 1.0;
                    return;
                }

                // Node Body - ANDURA dark fill with status-colored hairline
                ctx.fillStyle = CONFIG.colors.surface;
                ctx.strokeStyle = CONFIG.colors[n.status] || CONFIG.colors.line;

                if (state.selected === n.id) {
                    ctx.strokeStyle = CONFIG.colors.selection;
                    ctx.lineWidth = 2 / state.view.scale;
                    ctx.shadowBlur = 0;
                } else if (state.selected && connectedIds.has(n.id)) {
                    ctx.strokeStyle = CONFIG.colors.text;
                    ctx.lineWidth = 1.5 / state.view.scale;
                    ctx.shadowBlur = 0;
                } else {
                    ctx.lineWidth = 1 / state.view.scale;
                    ctx.shadowBlur = 0;
                }

                roundRect(ctx, n.x, n.y, n.w, n.h, CONFIG.node.radius);
                ctx.fill();
                ctx.stroke();
                ctx.shadowBlur = 0;

                // Text label - always show node nickname or name
                // Hide labels below zoom 0.5 (11px would be too large in world space)
                if (state.view.scale >= 0.5) {
                    const label = n.nickname || n.name || 'Unnamed';
                    ctx.fillStyle = CONFIG.colors.text;
                    // Screen size = clamp(11, 12*scale, 14) px; world size = screen / scale
                    const screenPx = Math.max(11, Math.min(12 * state.view.scale, 14));
                    const fontSize = screenPx / state.view.scale;
                    ctx.font = `${fontSize}px "IBM Plex Sans", ui-sans-serif, system-ui, sans-serif`;
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    
                    // Center text in node
                    const maxWidth = n.w - 16;
                    const tx = n.x + n.w / 2;
                    const ty = n.y + n.h / 2;
                    
                    // Truncate if needed
                    let displayLabel = label;
                    if (ctx.measureText(label).width > maxWidth) {
                        while (displayLabel.length > 3 && ctx.measureText(displayLabel + '…').width > maxWidth) {
                            displayLabel = displayLabel.slice(0, -1);
                        }
                        displayLabel += '…';
                    }
                    
                    ctx.fillText(displayLabel, tx, ty);
                }

                // Draw port sockets only (no labels yet)
                drawNodePortSockets(n, false, null);

                // Cluster indicator: draw a special double-border and icon
                const props = n.properties || n.Properties || {};
                const isCluster = isTrueish(props.isCluster) || isTrueish(props.IsCluster) || /cluster/i.test(String(n.name || ''));
                if (isCluster) {
                    // Inner dashed border for cluster nodes
                    ctx.save();
                    ctx.strokeStyle = '#b388ff';
                    ctx.lineWidth = 1.5 / state.view.scale;
                    ctx.setLineDash([4 / state.view.scale, 3 / state.view.scale]);
                    roundRect(ctx, n.x + 2, n.y + 2, n.w - 4, n.h - 4, CONFIG.node.radius - 1);
                    ctx.stroke();
                    ctx.setLineDash([]);
                    ctx.restore();

                    // Cluster icon (small box-in-box) in top-right corner
                    if (state.view.scale > 0.35) {
                        const ix = n.x + n.w - 16;
                        const iy = n.y + 8;
                        ctx.strokeStyle = '#b388ff';
                        ctx.lineWidth = 1 / state.view.scale;
                        ctx.strokeRect(ix, iy, 8, 6);
                        ctx.strokeRect(ix + 2, iy + 2, 8, 6);
                    }
                }

                ctx.globalAlpha = 1.0;
            });

            ctx.restore();

            // Draw port labels with collision detection (after wires and nodes)
            if (state.showPortDetails && state.view.scale >= 1.2) {
                const portLabels = [];
                
                // Create lookup for node alpha
                const nodeAlphaMap = new Map();
                nodesToDraw.forEach(item => {
                    const finalAlpha = (item.dimmed ? 0.1 : 1.0) * item.alpha;
                    if (state.selected) {
                        const n = item.node;
                        const isConnected = connectedIds.has(n.id);
                        nodeAlphaMap.set(n.id, isConnected ? item.alpha : 0.1 * item.alpha);
                    } else {
                        nodeAlphaMap.set(item.node.id, finalAlpha);
                    }
                });
                
                // Collect all port labels, prioritizing selected/hovered nodes
                nodesToDraw.forEach(item => {
                    const n = item.node;
                    const nodeAlpha = nodeAlphaMap.get(n.id) || 0;
                    if (nodeAlpha < 0.5) return; // Skip dimmed nodes
                    drawNodePortSockets(n, true, portLabels);
                });
                
                // Sort by priority (selected/hovered first)
                portLabels.sort((a, b) => b.priority - a.priority);
                
                // Track node rects and drawn label rects for collision detection
                const nodeRects = nodesToDraw.map(item => {
                    const n = item.node;
                    return {
                        x: (n.x * state.view.scale) + state.view.x,
                        y: (n.y * state.view.scale) + state.view.y,
                        w: n.w * state.view.scale,
                        h: n.h * state.view.scale
                    };
                });
                const drawnRects = [];
                
                const rectsCollide = (r1, r2) => {
                    return !(r1.x + r1.w < r2.x || r2.x + r2.w < r1.x ||
                             r1.y + r1.h < r2.y || r2.y + r2.h < r1.y);
                };
                
                // Draw labels in screen space, skipping collisions
                portLabels.forEach(label => {
                    // Check collision with nodes
                    let collides = nodeRects.some(nr => rectsCollide(label.screenRect, nr));
                    
                    // Check collision with already drawn labels
                    if (!collides) {
                        collides = drawnRects.some(dr => rectsCollide(label.screenRect, dr));
                    }
                    
                    if (!collides) {
                        // Draw background rect (screen coordinates)
                        // Determine group membership and check if label is fully inside group bounds
                        const nodeGroups = state.groups.filter(g => {
                            const members = g.memberIds || g.MemberIds || g.memberIdsOld || g.memberIdsNew || [];
                            return members.includes(label.node.id);
                        });
                        
                        let bgColor = CONFIG.colors.surface;
                        let bgAlpha = 1.0;
                        
                        // Only use group color if label rect is fully inside group screen bounds
                        if (nodeGroups.length > 0) {
                            const group = nodeGroups[0];
                            const groupRect = computeGroupRenderRect(group, renderNodeLookup);
                            if (groupRect) {
                                // Transform group rect to screen coordinates
                                const groupScreenX = groupRect.x * state.view.scale + state.view.x;
                                const groupScreenY = groupRect.y * state.view.scale + state.view.y;
                                const groupScreenW = groupRect.w * state.view.scale;
                                const groupScreenH = groupRect.h * state.view.scale;
                                
                                // Check if label rect is fully inside group rect
                                const labelLeft = label.screenX - 2;
                                const labelRight = label.screenX + label.screenW + 2;
                                const labelTop = label.screenY - 2;
                                const labelBottom = label.screenY + label.screenH + 2;
                                
                                const fullyInside = labelLeft >= groupScreenX &&
                                                   labelRight <= groupScreenX + groupScreenW &&
                                                   labelTop >= groupScreenY &&
                                                   labelBottom <= groupScreenY + groupScreenH;
                                
                                if (fullyInside) {
                                    // Calculate same alpha factor as group rendering
                                    const memberIds = getGroupMemberIdsForMix(group, state.versionMix);
                                    const connectedIds = getConnectedNodeIds();
                                    const connectedCanonical = new Set(Array.from(connectedIds).map(canonicalizeId));
                                    const matchingCanonical = new Set((state.matchingNodes || []).map(n => canonicalizeId(n.id)));
                                    const matchingNodeSet = state.matchingNodes ? new Set(state.matchingNodes.map(n => n.id)) : null;
                                    const isSelectedGroup = !!state.selected && memberIds.some(id => canonicalizeId(id) === canonicalizeId(state.selected));
                                    const isConnectedGroup = !isSelectedGroup && !!state.selected && memberIds.some(id =>
                                        connectedIds.has(id) || connectedCanonical.has(canonicalizeId(id)));
                                    const dimUnrelated = !!state.selected && !isSelectedGroup && !isConnectedGroup;
                                    const hasMatchingMember = memberIds.some(id =>
                                        (matchingNodeSet && matchingNodeSet.has(id)) || matchingCanonical.has(canonicalizeId(id)));
                                    const dimForSearch = !!state.search && !hasMatchingMember;
                                    const dimForStatusFilter = !isAllStatusFilterActive() && !hasMatchingMember;
                                    const selectionAlphaFactor = dimUnrelated ? 0.18 : (isConnectedGroup ? 0.45 : 1.0);
                                    const criteriaAlphaFactor = (dimForSearch || dimForStatusFilter) ? 0.1 : 1.0;
                                    const alphaFactor = selectionAlphaFactor * criteriaAlphaFactor;
                                    
                                    // Blend group color with surface based on alpha
                                    bgColor = CONFIG.colors.group;
                                    bgAlpha = alphaFactor;
                                }
                            }
                        }
                        
                        const padding = 2; // screen pixels
                        
                        // Draw backing with proper alpha blending
                        ctx.save();
                        ctx.globalAlpha = bgAlpha;
                        ctx.fillStyle = bgColor;
                        ctx.fillRect(label.screenX - padding, label.screenY - padding,
                                     label.screenW + padding * 2, label.screenH + padding * 2);
                        ctx.restore();
                        
                        // Draw text (screen coordinates)
                        ctx.fillStyle = CONFIG.colors.textMuted;
                        ctx.font = `${label.screenFontSize}px "IBM Plex Mono", ui-monospace, monospace`;
                        ctx.textBaseline = 'top';
                        ctx.textAlign = 'left';
                        ctx.fillText(label.text, label.screenX, label.screenY);
                        
                        drawnRects.push(label.screenRect);
                    }
                });
            }

            drawMinimap(nodesToDraw);
        }

        function computeGroupRenderRect(group, renderNodeLookup, mix = state.versionMix) {
            if (!group) return null;

            let minX = Infinity;
            let maxX = -Infinity;
            let minY = Infinity;
            let maxY = -Infinity;
            let memberCount = 0;

            const members = getGroupMemberIdsForMix(group, mix);
            members.forEach(memberId => {
                const node = renderNodeLookup.get(memberId) || renderNodeLookup.get(canonicalizeId(memberId));
                if (!node) return;

                memberCount++;
                if (node.x < minX) minX = node.x;
                if (node.x + node.w > maxX) maxX = node.x + node.w;
                if (node.y < minY) minY = node.y;
                if (node.y + node.h > maxY) maxY = node.y + node.h;
            });

            const padding = 16;
            if (memberCount > 0 && Number.isFinite(minX) && Number.isFinite(minY) && Number.isFinite(maxX) && Number.isFinite(maxY)) {
                return {
                    x: minX - padding,
                    y: minY - padding,
                    w: (maxX - minX) + padding * 2,
                    h: (maxY - minY) + padding * 2,
                    memberCount
                };
            }

            // Fall back to authored group rectangle only when member nodes are not available.
            if (group.explicitRect) {
                const dx = Number.isFinite(group.explicitRect.dx) ? group.explicitRect.dx : 0;
                const dy = Number.isFinite(group.explicitRect.dy) ? group.explicitRect.dy : 0;
                return {
                    x: group.explicitRect.x - dx * (1 - mix),
                    y: group.explicitRect.y - dy * (1 - mix),
                    w: group.explicitRect.w,
                    h: group.explicitRect.h,
                    memberCount: members.length
                };
            }

            if (group.fallbackRect) {
                return {
                    x: group.fallbackRect.x,
                    y: group.fallbackRect.y,
                    w: group.fallbackRect.w,
                    h: group.fallbackRect.h,
                    memberCount: 0
                };
            }

            return null;
        }

        function drawGroupBackgrounds(renderNodeLookup, viewLeft, viewTop, viewRight, viewBottom, mix, connectedIds, matchingNodeSet) {
            if (!state.showGroups) return;
            if (!Array.isArray(state.groups) || state.groups.length === 0) return;
            const connectedCanonical = new Set(Array.from(connectedIds || []).map(id => canonicalizeId(id)));
            const matchingCanonical = new Set(Array.from(matchingNodeSet || []).map(id => canonicalizeId(id)));

            for (let i = 0; i < state.groups.length; i++) {
                const group = state.groups[i];
                const rect = computeGroupRenderRect(group, renderNodeLookup, mix);
                if (!rect || rect.w <= 0 || rect.h <= 0) continue;

                const offscreen = rect.x + rect.w < viewLeft || rect.x > viewRight || rect.y + rect.h < viewTop || rect.y > viewBottom;
                if (state.largeModelMode && !state.selected && offscreen) continue;

                let groupAlpha = 1.0;
                if (group.status === 'added') groupAlpha = mix;
                else if (group.status === 'removed') groupAlpha = 1.0 - mix;
                if (groupAlpha <= 0.02) continue;

                const memberIds = getGroupMemberIdsForMix(group, mix);
                const isSelectedGroup = !!state.selected && memberIds.some(id => canonicalizeId(id) === canonicalizeId(state.selected));
                const isConnectedGroup = !isSelectedGroup && !!state.selected && memberIds.some(id =>
                    connectedIds.has(id) || connectedCanonical.has(canonicalizeId(id)));
                const dimUnrelated = !!state.selected && !isSelectedGroup && !isConnectedGroup;
                const hasMatchingMember = memberIds.some(id =>
                    (matchingNodeSet && matchingNodeSet.has(id)) || matchingCanonical.has(canonicalizeId(id)));
                const dimForSearch = !!state.search && !hasMatchingMember;
                const dimForStatusFilter = !isAllStatusFilterActive() && !hasMatchingMember;
                const selectionAlphaFactor = dimUnrelated ? 0.18 : (isConnectedGroup ? 0.45 : 1.0);
                const criteriaAlphaFactor = (dimForSearch || dimForStatusFilter) ? 0.1 : 1.0;
                const alphaFactor = selectionAlphaFactor * criteriaAlphaFactor;
                // Use fixed group color #141517 from CONFIG.colors.group
                const color = { r: 20, g: 21, b: 23 };

                ctx.save();
                ctx.globalAlpha = Math.max(0.05, groupAlpha * alphaFactor);
                ctx.fillStyle = `rgba(${color.r}, ${color.g}, ${color.b}, 1.0)`;
                ctx.strokeStyle = CONFIG.colors[group.status] || CONFIG.colors.same;
                ctx.lineWidth = (isSelectedGroup ? 2.0 : 1.2) / state.view.scale;
                ctx.setLineDash([8 / state.view.scale, 6 / state.view.scale]);
                roundRect(ctx, rect.x, rect.y, rect.w, rect.h, 12);
                ctx.fill();
                ctx.stroke();
                ctx.setLineDash([]);

                // Draw group label above the rectangle to avoid overlap
                const groupLabel = String(group.name || '').trim();
                if (groupLabel && groupLabel.toLowerCase() !== 'group' && state.view.scale >= 0.5) {
                    // Screen size = clamp(11, 12*scale, 14) px; world size = screen / scale
                    const screenPx = Math.max(11, Math.min(12 * state.view.scale, 14));
                    const fontSize = screenPx / state.view.scale;
                    ctx.fillStyle = CONFIG.colors.textMuted;
                    ctx.font = `500 ${fontSize}px "IBM Plex Mono", ui-monospace, monospace`;
                    ctx.textBaseline = 'bottom';
                    ctx.fillText(groupLabel, rect.x + 8 / state.view.scale, rect.y - 4 / state.view.scale);
                }
                ctx.restore();
            }
        }

        function drawMinimap(nodesWithDim) {
            miniCtx.clearRect(0, 0, minimap.width, minimap.height);

            const w = state.bounds.maxX - state.bounds.minX;
            const h = state.bounds.maxY - state.bounds.minY;

            // Handle empty or zero size
            if (w === 0 || h === 0) return;

            const scale = Math.min(minimap.width / w, minimap.height / h) * 0.9;

            const offsetX = (minimap.width - w * scale) / 2 - state.bounds.minX * scale;
            const offsetY = (minimap.height - h * scale) / 2 - state.bounds.minY * scale;

            // Draw nodes
            nodesWithDim.forEach(item => {
                const n = item.node;
                miniCtx.globalAlpha = item.dimmed ? 0.2 : 1.0;
                miniCtx.fillStyle = CONFIG.colors[n.status] || CONFIG.colors.same;
                miniCtx.fillRect(
                    n.x * scale + offsetX,
                    n.y * scale + offsetY,
                    n.w * scale,
                    n.h * scale
                );
            });
            miniCtx.globalAlpha = 1.0;

            // Draw Viewport Rect
            const vx = (-state.view.x / state.view.scale) * scale + offsetX;
            const vy = (-state.view.y / state.view.scale) * scale + offsetY;
            const vw = (canvas.width / state.view.scale) * scale;
            const vh = (canvas.height / state.view.scale) * scale;

            miniCtx.strokeStyle = 'rgba(255, 255, 255, 0.3)';
            miniCtx.lineWidth = 1;
            miniCtx.strokeRect(vx, vy, vw, vh);
        }

        function roundRect(ctx, x, y, w, h, r) {
            if (w < 2 * r) r = w / 2;
            if (h < 2 * r) r = h / 2;
            ctx.beginPath();
            ctx.moveTo(x + r, y);
            ctx.arcTo(x + w, y, x + w, y + h, r);
            ctx.arcTo(x + w, y + h, x, y + h, r);
            ctx.arcTo(x, y + h, x, y, r);
            ctx.arcTo(x, y, x + w, y, r);
            ctx.closePath();
        }

        /**
         * Map pointer position to canvas backing-store pixels. Required when CSS-scaled size
         * differs from canvas.width/height (WebView/DPI/layout), or clicks miss drawn nodes.
         */
        function canvasClientToBitmap(clientX, clientY) {
            const rect = canvas.getBoundingClientRect();
            const rw = rect.width || 1;
            const rh = rect.height || 1;
            return {
                x: (clientX - rect.left) * (canvas.width / rw),
                y: (clientY - rect.top) * (canvas.height / rh)
            };
        }

        function minimapClientToBitmap(clientX, clientY) {
            const rect = minimap.getBoundingClientRect();
            const rw = rect.width || 1;
            const rh = rect.height || 1;
            return {
                x: (clientX - rect.left) * (minimap.width / rw),
                y: (clientY - rect.top) * (minimap.height / rh)
            };
        }

        function getNodeAtCanvasPoint(clientX, clientY) {
            const { x: px, y: py } = canvasClientToBitmap(clientX, clientY);
            const mx = (px - state.view.x) / state.view.scale;
            const my = (py - state.view.y) / state.view.scale;
            const mix = state.versionMix;

            for (let i = state.nodes.length - 1; i >= 0; i--) {
                const n = state.nodes[i];
                const rx = n.x - (n.dx || 0) * (1 - mix);
                const ry = n.y - (n.dy || 0) * (1 - mix);

                if (mx >= rx && mx <= rx + n.w && my >= ry && my <= ry + n.h) {
                    return n;
                }
            }

            return null;
        }

        // --- Interaction Handlers ---

        function onMouseDown(e) {
            state.dragging = true;
            state.dragMoved = false;
            state.lastPos = { x: e.clientX, y: e.clientY };
        }

        function onMouseMove(e) {
            if (state.dragging) {
                const rect = canvas.getBoundingClientRect();
                const sx = canvas.width / (rect.width || 1);
                const sy = canvas.height / (rect.height || 1);
                const dx = (e.clientX - state.lastPos.x) * sx;
                const dy = (e.clientY - state.lastPos.y) * sy;
                if (Math.abs(dx) > 2 || Math.abs(dy) > 2) {
                    state.dragMoved = true;
                }
                state.view.x += dx;
                state.view.y += dy;
                state.lastPos = { x: e.clientX, y: e.clientY };
                scheduleDraw();
            }

            // Tooltip Logic
            const hovered = getNodeAtCanvasPoint(e.clientX, e.clientY);

            if (hovered) {
                tooltip.style.display = 'block';
                tooltip.style.left = (e.clientX + 10) + 'px';
                tooltip.style.top = (e.clientY + 10) + 'px';
                tooltip.innerHTML = `<strong>${hovered.nickname || hovered.name}</strong><br><span style="color:var(--text-secondary)">${hovered.name}</span>`;
                canvas.style.cursor = 'pointer';
            } else {
                tooltip.style.display = 'none';
                canvas.style.cursor = state.dragging ? 'grabbing' : 'grab';
            }
        }

        function onMouseUp() {
            state.dragging = false;
        }

        // --- Minimap Interaction ---

        function onMinimapDown(e) {
            state.minimapDragging = true;
            onMinimapMove(e);
        }

        function onMinimapMove(e) {
            if (!state.minimapDragging) return;

            const { x: mx, y: my } = minimapClientToBitmap(e.clientX, e.clientY);

            const w = state.bounds.maxX - state.bounds.minX;
            const h = state.bounds.maxY - state.bounds.minY;
            if (w === 0 || h === 0) return;

            const scale = Math.min(minimap.width / w, minimap.height / h) * 0.9;
            const offsetX = (minimap.width - w * scale) / 2 - state.bounds.minX * scale;
            const offsetY = (minimap.height - h * scale) / 2 - state.bounds.minY * scale;

            // Reverse map from minimap coords to world coords
            // mx = x * scale + offsetX  =>  x = (mx - offsetX) / scale
            const worldX = (mx - offsetX) / scale;
            const worldY = (my - offsetY) / scale;

            // Center view on worldX, worldY
            // view.x = canvas.width/2 - worldX * view.scale
            state.view.x = canvas.width / 2 - worldX * state.view.scale;
            state.view.y = canvas.height / 2 - worldY * state.view.scale;

            scheduleDraw();
        }

        function onWheel(e) {
            e.preventDefault();
            const delta = -Math.sign(e.deltaY);
            const factor = Math.pow(1.1, delta);

            const { x: mx, y: my } = canvasClientToBitmap(e.clientX, e.clientY);

            const newScale = state.view.scale * factor;

            // Zoom towards mouse pointer
            state.view.x = mx - (mx - state.view.x) * (newScale / state.view.scale);
            state.view.y = my - (my - state.view.y) * (newScale / state.view.scale);
            state.view.scale = newScale;

            scheduleDraw();
        }

        function onClick(e) {
            if (state.dragMoved) return;

            const clicked = getNodeAtCanvasPoint(e.clientX, e.clientY);

            state.selected = clicked ? clicked.id : null;
            updateDetailsPanel(clicked);
            renderComponentResultsList();
            scheduleDraw();
        }

        function onDoubleClick(e) {
            const clicked = getNodeAtCanvasPoint(e.clientX, e.clientY);

            if (!clicked) return;

            state.selected = clicked.id;
            updateDetailsPanel(clicked);
            renderComponentResultsList();

            const clusterPreview = getClusterPreviewData(clicked);
            if (clusterPreview.isCluster) {
                openClusterGraphTab(clicked, clusterPreview);
            }

            scheduleDraw();
        }

        function applyPortDetailsVisibility() {
            const togglePorts = document.getElementById('toggle-ports');
            const showPorts = togglePorts ? !!togglePorts.checked : !!state.showPortDetails;
            state.showPortDetails = showPorts;

            detailsPaneInspector.querySelectorAll('.connections-card').forEach(card => {
                card.style.display = showPorts ? '' : 'none';
            });
        }

        function showHighLevelModificationDetails() {
            return !!state.includeHighLevelModifications;
        }

        function updateDetailsPanel(node) {
            if (!node) {
                detailsPaneInspector.innerHTML = '<div class="empty-state">Select a node in the graph<br>to view details</div>';
                setScriptTabVisibility(false);
                state.scriptContext = null;
                renderEmptyScriptPane();
                return;
            }

            const togglePorts = document.getElementById('toggle-ports');
            const showPorts = togglePorts ? !!togglePorts.checked : !!state.showPortDetails;
            state.showPortDetails = showPorts;
            const riskScore = Number.isFinite(Number(node.riskScore)) ? Number(node.riskScore) : undefined;
            const riskReasons = Array.isArray(node.riskReasons) ? node.riskReasons : [];
            const showHighLevelDetails = showHighLevelModificationDetails();
            const effectiveStatus = computeEffectiveNodeStatus(node, showHighLevelDetails);
            const renderTopUserInputs = !isPanelLikeNode(node);
            const riskReasonText = showHighLevelDetails && riskReasons.length ? ` (${riskReasons.slice(0, 2).join(', ')})` : '';

            const connectionsBlock = showHighLevelDetails ? `
                <div class="detail-card connections-card" style="${showPorts ? '' : 'display:none;'}">
                    <div class="node-name" style="font-size: 12px; margin-bottom: 8px;">Connections</div>
                    <div class="prop-row">
                        <span class="prop-label">Inputs</span>
                        <span class="prop-value">${node.inAdded || 0} add / ${node.inRemoved || 0} rem</span>
                    </div>
                    <div class="prop-row">
                        <span class="prop-label">Outputs</span>
                        <span class="prop-value">${node.outAdded || 0} add / ${node.outRemoved || 0} rem</span>
                    </div>
                    ${renderPorts(node.inputs, 'Inputs')}
                    ${renderPorts(node.outputs, 'Outputs')}
                </div>
            ` : '';

            const html = `
                <div class="detail-card">
                    <div class="detail-header">
                        <div>
                            <div class="node-name">${node.nickname || node.name}</div>
                            <div class="node-type">${node.name}</div>
                        </div>
                        <span class="status-badge" style="background: ${CONFIG.colors[effectiveStatus]}; color: white">
                            ${effectiveStatus}
                        </span>
                    </div>
                    
                    <div class="prop-row">
                        <span class="prop-label">ID</span>
                        <span class="prop-value" title="${node.id}">${(node.id || '').substring(0, 8)}...</span>
                    </div>
                    <div class="prop-row">
                        <span class="prop-label">Position</span>
                        <span class="prop-value">${Math.round(node.x)}, ${Math.round(node.y)}</span>
                    </div>
                    ${showHighLevelDetails && riskScore !== undefined ? `
                    <div class="prop-row">
                        <span class="prop-label">Risk Score</span>
                        <span class="prop-value">${riskScore}${riskReasonText}</span>
                    </div>` : ''}
                    ${showHighLevelDetails && node.dx !== undefined ? `
                    <div class="prop-row">
                        <span class="prop-label">Movement</span>
                        <span class="prop-value">Change: ${node.dx.toFixed(1)}, ${node.dy.toFixed(1)}</span>
                    </div>` : ''}
                </div>

                ${renderChangeSummary(node)}
                ${renderTopUserInputs ? renderUserInputSummary(node) : ''}
                ${renderProperties(node)}
                ${connectionsBlock}
            `;

            detailsPaneInspector.innerHTML = html;
            bindInspectorActions();
            applyPortDetailsVisibility();

            const scriptEntries = getScriptPropertyEntries(node);
            const scriptComponent = isScriptComponent(node);
            const changedScriptEntries = scriptEntries.filter(entry => entry.isChanged);
            const showScriptTab = scriptEntries.length > 0;
            setScriptTabVisibility(showScriptTab, scriptEntries.length);

            if (scriptEntries.length === 0) {
                state.scriptContext = null;
                renderEmptyScriptPane(scriptComponent
                    ? 'No script source found for this component.'
                    : 'No script source found for this component');
            } else {
                const selectedScriptKey = state.scriptContext && state.scriptContext.nodeId === node.id
                    ? state.scriptContext.key
                    : (changedScriptEntries[0]?.key || scriptEntries[0].key);

                renderScriptPane(node, scriptEntries, selectedScriptKey);
            }
        }

        function bindInspectorActions() {
            detailsPaneInspector.querySelectorAll('.script-diff-open').forEach(btn => {
                btn.addEventListener('click', () => {
                    const encoded = btn.dataset.scriptKey || '';
                    const key = decodeURIComponent(encoded);
                    openScriptDiff(key);
                });
            });

            detailsPaneInspector.querySelectorAll('.cluster-preview-open').forEach(btn => {
                btn.addEventListener('click', () => openClusterPreview());
            });
        }

        function isLikelyScriptPropertyKey(key) {
            if (!key) return false;
            if (isScriptNoiseKey(key)) return false;
            return isExplicitScriptKey(key) || SCRIPT_KEY_PATTERN.test(String(key));
        }

        function looksLikeScriptContent(value) {
            const text = String(value ?? '');
            if (!text || text.length < 20) return false;
            const hasCodeIndicators = /(def\s+\w+\s*\(|using\s+[A-Za-z0-9_.]+;|public\s+class|return\s+.+;|for\s*\(|while\s*\(|if\s*\(|\/\/|#\!|import\s+\w+|class\s+\w+|function\s+\w+|=>|\{|\}|\bvar\b|\blet\b|\bconst\b)/.test(text);
            if (hasCodeIndicators) return true;

            // Multiline alone is too noisy (e.g. editor metadata). Require assignment/call-like syntax too.
            if (text.includes('\n')) {
                return /[A-Za-z_][A-Za-z0-9_]*\s*(=|\(|\[)/.test(text);
            }

            return false;
        }

        function isTrueish(value) {
            if (value === true) return true;
            if (typeof value === 'string') {
                const v = value.trim().toLowerCase();
                return v === 'true' || v === '1' || v === 'yes';
            }
            if (typeof value === 'number') return value !== 0;
            return false;
        }

        function isScriptComponent(node) {
            const name = String(node?.name || '');
            const nickname = String(node?.nickname || '');
            if (SCRIPT_COMPONENT_PATTERN.test(name) || SCRIPT_COMPONENT_PATTERN.test(nickname)) {
                return true;
            }

            const props = node?.properties || {};
            const oldProps = node?.propertiesOld || {};
            const keys = [...Object.keys(props), ...Object.keys(oldProps)];
            return keys.some(k => isLikelyScriptPropertyKey(k));
        }

        function normalizeScriptForCompare(value) {
            return String(value ?? '')
                .replace(/\r\n/g, '\n')
                .replace(/[ \t]+\n/g, '\n')
                .trimEnd();
        }

        function getScriptPropertyEntries(node) {
            const props = node.properties || {};
            const oldProps = node.propertiesOld || {};
            const nodeStatus = String(node?.status || 'same').toLowerCase();
            const keys = new Set([...Object.keys(props), ...Object.keys(oldProps)]);
            const entries = [];

            keys.forEach(key => {
                const hasNew = Object.prototype.hasOwnProperty.call(props, key);
                const hasOld = Object.prototype.hasOwnProperty.call(oldProps, key);
                const rawNewValue = hasNew ? String(props[key] ?? '') : '';
                const rawOldValue = hasOld ? String(oldProps[key] ?? '') : '';

                const includeKey = !isScriptNoiseKey(key) &&
                    (isLikelyScriptPropertyKey(key) ||
                        looksLikeScriptContent(rawNewValue) ||
                        looksLikeScriptContent(rawOldValue));
                if (!includeKey) return;

                let oldValue = rawOldValue;
                let newValue = rawNewValue;
                let isChanged = false;

                if (hasOld && hasNew) {
                    isChanged = normalizeScriptForCompare(rawOldValue) !== normalizeScriptForCompare(rawNewValue);
                } else if (!hasOld && hasNew) {
                    if (nodeStatus === 'added') {
                        isChanged = true;
                    } else if (nodeStatus === 'removed') {
                        oldValue = rawNewValue;
                        newValue = '';
                        isChanged = true;
                    } else if (nodeStatus === 'modified') {
                        oldValue = '';
                        newValue = rawNewValue;
                        isChanged = true;
                    } else {
                        oldValue = rawNewValue;
                        isChanged = false;
                    }
                } else if (hasOld && !hasNew) {
                    isChanged = true;
                }

                entries.push({
                    key,
                    oldValue,
                    newValue,
                    isChanged,
                    language: detectScriptLanguage(node, key, newValue || oldValue)
                });
            });

            entries.sort((a, b) => compareScriptPropertyEntryOrder(a.key, b.key));

            return entries;
        }

        /**
         * Rhino 7 C# script components expose three blocks: Using, Script, Additional.
         * Rhino 8 often collapses to a single source field. Order: Using → Script → (other keys) → Additional last.
         */
        function getScriptPropertySortTier(key) {
            const k = normalizeScriptKey(key);
            if (k === 'usingsource') return 0;
            if (k === 'scriptsource') return 1;
            if (k === 'additionalsource') return 3;
            return 2;
        }

        function compareScriptPropertyEntryOrder(keyA, keyB) {
            const ta = getScriptPropertySortTier(keyA);
            const tb = getScriptPropertySortTier(keyB);
            if (ta !== tb) return ta - tb;
            return String(keyA || '').localeCompare(String(keyB || ''), undefined, { sensitivity: 'base' });
        }

        function normalizeScriptKey(key) {
            return String(key || '').trim().toLowerCase();
        }

        function isExplicitScriptKey(key) {
            return SCRIPT_PROPERTY_KEYS.has(normalizeScriptKey(key));
        }

        function isScriptNoiseKey(key) {
            return SCRIPT_NOISE_KEYS.has(normalizeScriptKey(key));
        }

        function isPrimaryScriptEntryKey(key) {
            const normalized = normalizeScriptKey(key);
            return normalized === 'scriptsource' ||
                normalized === 'additionalsource' ||
                normalized === 'usingsource' ||
                normalized === 'sourcecode' ||
                normalized === 'code' ||
                normalized === 'script' ||
                normalized === 'pythonscript' ||
                normalized === 'pythoncode' ||
                normalized === 'csharpcode' ||
                normalized === 'vbcode';
        }

        function getChangedScriptEntries(entries) {
            return Array.isArray(entries) ? entries.filter(entry => entry?.isChanged) : [];
        }

        function getRequestedScriptEntry(entries, requestedKey) {
            const availableEntries = Array.isArray(entries) ? entries.filter(Boolean) : [];
            if (availableEntries.length === 0) return null;
            if (requestedKey && requestedKey !== '*') {
                const requestedEntry = availableEntries.find(entry => entry.key === requestedKey);
                if (requestedEntry) return requestedEntry;
            }

            return availableEntries.find(entry => entry.isChanged && isPrimaryScriptEntryKey(entry.key)) ||
                availableEntries.find(entry => isPrimaryScriptEntryKey(entry.key)) ||
                availableEntries.find(entry => entry.isChanged) ||
                availableEntries[0];
        }

        function isScriptModalOpen() {
            return !!scriptModal && scriptModal.classList.contains('visible');
        }

        function renderExpandedScriptDiff(node, entry) {
            if (!scriptModalTitle || !scriptModalSubtitle || !scriptModalBody || !node || !entry) return;

            const nodeName = String(node.nickname || node.name || node.id || 'Script');
            const allEntries = getScriptPropertyEntries(node);
            const focusKey = entry.key;

            scriptModalTitle.textContent = nodeName;

            const renderSingleBlock = (e) => {
                const language = e.language || detectScriptLanguage(node, e.key, e.newValue || e.oldValue);
                const renderMode = e.isChanged ? SCRIPT_DIFF_RENDER_MODE.FULL_INLINE : SCRIPT_DIFF_RENDER_MODE.VIEW_ONLY;
                return `
                <div class="script-modal-shell">
                    <div class="diff-container">
                        ${computeDiff(e.oldValue, e.newValue, {
                            language,
                            mode: renderMode
                        })}
                    </div>
                </div>
            `;
            };

            if (allEntries.length <= 1) {
                const e = allEntries[0] || entry;
                const language = e.language || detectScriptLanguage(node, e.key, e.newValue || e.oldValue);
                scriptModalSubtitle.textContent = `${e.key} · ${language} · ${e.isChanged ? 'Full inline diff' : 'Source view'}`;
                scriptModalBody.innerHTML = renderSingleBlock(e);
                return;
            }

            scriptModalSubtitle.textContent = `${allEntries.length} script properties · full document order`;

            const blocks = allEntries.map((e) => {
                const language = e.language || detectScriptLanguage(node, e.key, e.newValue || e.oldValue);
                const renderMode = e.isChanged ? SCRIPT_DIFF_RENDER_MODE.FULL_INLINE : SCRIPT_DIFF_RENDER_MODE.VIEW_ONLY;
                const modeLabel = e.isChanged ? 'Full inline diff' : 'Source view';
                const isFocus = e.key === focusKey;
                return `
            <section class="script-modal-section" ${isFocus ? 'data-script-section-focus="true"' : ''}>
                <div class="script-modal-section-header">${escapeHtml(e.key)} · ${escapeHtml(language)} · ${escapeHtml(modeLabel)}</div>
                <div class="diff-container">
                    ${computeDiff(e.oldValue, e.newValue, {
                        language,
                        mode: renderMode
                    })}
                </div>
            </section>
                `;
            }).join('');

            scriptModalBody.innerHTML = `
                <div class="script-modal-shell script-modal-shell--multi">
                    ${blocks}
                </div>
            `;

            window.requestAnimationFrame(() => {
                const focusSection = scriptModalBody.querySelector('[data-script-section-focus="true"]')
                    || scriptModalBody.querySelector('.script-modal-section');
                if (focusSection && typeof focusSection.scrollIntoView === 'function') {
                    focusSection.scrollIntoView({ block: 'start', behavior: 'auto' });
                }
            });
        }

        function openExpandedScriptDiff() {
            if (!scriptModal) return;

            const selectedNode = state.nodes.find(n => n.id === state.selected);
            if (!selectedNode) return;

            const scriptEntries = getScriptPropertyEntries(selectedNode);
            const requestedKey = state.scriptContext && state.scriptContext.nodeId === selectedNode.id
                ? state.scriptContext.key
                : null;
            const selectedEntry = getRequestedScriptEntry(scriptEntries, requestedKey);
            if (!selectedEntry) return;

            const currentFocus = document.activeElement;
            scriptModalReturnFocus = currentFocus instanceof HTMLElement ? currentFocus : null;
            renderExpandedScriptDiff(selectedNode, selectedEntry);
            if (mainContainer) mainContainer.setAttribute('inert', '');
            // Bug fix #2: Use 'visible' to match CSS (was 'open')
            scriptModal.classList.add('visible');
            document.body.style.overflow = 'hidden';

            window.setTimeout(() => {
                if (btnScriptModalClose) btnScriptModalClose.focus();
            }, 0);
        }

        function closeExpandedScriptDiff(options = {}) {
            if (!scriptModal) return;

            const { restoreFocus = true } = options;
            const wasOpen = scriptModal.classList.contains('visible');
            scriptModal.classList.remove('visible');
            if (scriptModalBody) {
                scriptModalBody.innerHTML = '';
            }
            if (mainContainer) {
                mainContainer.removeAttribute('inert');
            }
            document.body.style.overflow = state.spotlightOpen ? 'hidden' : '';

            const returnTarget = scriptModalReturnFocus;
            scriptModalReturnFocus = null;
            if (!restoreFocus || !wasOpen) return;

            if (returnTarget instanceof HTMLElement && returnTarget.isConnected) {
                returnTarget.focus();
            } else if (detailsTabScript && detailsTabScript.offsetParent !== null) {
                detailsTabScript.focus();
            }
        }

        function renderScriptPane(node, entries, requestedKey) {
            if (!entries.length) {
                renderEmptyScriptPane('No script source found for this component');
                return;
            }

            const selectedEntry = getRequestedScriptEntry(entries, requestedKey);
            if (!selectedEntry) {
                renderEmptyScriptPane('No script source found for this component');
                return;
            }

            const language = selectedEntry.language || detectScriptLanguage(node, selectedEntry.key, selectedEntry.newValue || selectedEntry.oldValue);
            const renderMode = selectedEntry.isChanged
                ? SCRIPT_DIFF_RENDER_MODE.FULL_INLINE
                : SCRIPT_DIFF_RENDER_MODE.VIEW_ONLY;
            const modeLabel = selectedEntry.isChanged ? 'Diff' : 'Source';
            const stateLabel = selectedEntry.isChanged ? 'Modified' : 'Unchanged';
            const entrySelectorHtml = entries.length > 1 ? `
                <div style="display:flex; flex-wrap:wrap; gap:8px; margin-bottom:12px;">
                    ${entries.map(entry => {
                        const isActive = entry.key === selectedEntry.key;
                        const tone = entry.isChanged ? 'var(--modified)' : 'var(--text-secondary)';
                        const background = isActive ? 'var(--accent-color)' : 'var(--bg-card)';
                        const foreground = isActive ? 'white' : 'var(--text-primary)';
                        const border = isActive ? 'var(--accent-color)' : 'var(--border-color)';
                        const status = entry.isChanged ? 'diff' : 'source';
                        return `
                            <button type="button"
                                data-script-key-select="${encodeURIComponent(entry.key)}"
                                style="display:inline-flex; align-items:center; gap:6px; padding:6px 10px; border-radius:999px; border:1px solid ${border}; background:${background}; color:${foreground}; cursor:pointer; font-size:11px; line-height:1.2;">
                                <span>${escapeHtml(entry.key)}</span>
                                <span style="color:${isActive ? 'rgba(255,255,255,0.84)' : tone}; font-weight:600;">${status}</span>
                            </button>
                        `;
                    }).join('')}
                </div>
            ` : '';

            state.scriptContext = {
                nodeId: node.id,
                key: selectedEntry.key
            };

            detailsPaneScript.innerHTML = `
                <div class="script-pane-shell">
                    <div class="script-pane-toolbar">
                        <div style="display:flex; align-items:center; gap:8px; min-width:0; flex-wrap:wrap;">
                            <span style="font-size:12px; font-weight:600; color:var(--text-primary);">${escapeHtml(selectedEntry.key)}</span>
                            <span style="font-size:11px; color:var(--text-secondary);">${escapeHtml(language)} · ${modeLabel} · ${stateLabel}</span>
                        </div>
                        <button type="button" class="script-expand-btn" data-script-expand title="Expand script" aria-label="Expand script">
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                                <path d="M15 3h6v6"></path>
                                <path d="M9 21H3v-6"></path>
                                <path d="M21 3l-7 7"></path>
                                <path d="M3 21l7-7"></path>
                            </svg>
                            <span class="visually-hidden">Expand script</span>
                        </button>
                    </div>
                    ${entrySelectorHtml}
                    <div class="diff-container">
                        ${computeDiff(selectedEntry.oldValue, selectedEntry.newValue, {
                            language,
                            mode: renderMode
                        })}
                    </div>
                </div>
            `;

            const expandButton = detailsPaneScript.querySelector('[data-script-expand]');
            if (expandButton) {
                expandButton.addEventListener('click', () => openExpandedScriptDiff());
            }

            detailsPaneScript.querySelectorAll('[data-script-key-select]').forEach(button => {
                button.addEventListener('click', () => {
                    const encoded = button.dataset.scriptKeySelect || '';
                    const nextKey = decodeURIComponent(encoded);
                    renderScriptPane(node, entries, nextKey);
                });
            });

            if (isScriptModalOpen()) {
                renderExpandedScriptDiff(node, selectedEntry);
            }
        }

        function isClusterLikeNode(node) {
            const props = node?.properties || {};
            const oldProps = node?.propertiesOld || {};
            return isTrueish(props.isCluster) ||
                isTrueish(props.IsCluster) ||
                isTrueish(oldProps.isCluster) ||
                isTrueish(oldProps.IsCluster) ||
                !!(props.clusterHash || props.ClusterHash || oldProps.clusterHash || oldProps.ClusterHash) ||
                !!(props.clusterPreviewGraph || props.ClusterPreviewGraph || oldProps.clusterPreviewGraph || oldProps.ClusterPreviewGraph) ||
                /cluster/i.test(String(node?.name || '')) ||
                /cluster/i.test(String(node?.nickname || ''));
        }

        function getClusterPreviewData(node) {
            const props = node.properties || {};
            const oldProps = node.propertiesOld || {};

            const isCluster = isClusterLikeNode(node);
            if (!isCluster) {
                return { isCluster: false };
            }

            const status = props.clusterPreviewStatus || props.ClusterPreviewStatus ||
                oldProps.clusterPreviewStatus || oldProps.ClusterPreviewStatus || 'unavailable';
            const message = props.clusterPreviewMessage || props.ClusterPreviewMessage ||
                oldProps.clusterPreviewMessage || oldProps.ClusterPreviewMessage || 'Cluster internals are not available in this diff.';

            const oldGraphRaw = oldProps.clusterPreviewGraph || oldProps.ClusterPreviewGraph || '';
            const newGraphRaw = props.clusterPreviewGraph || props.ClusterPreviewGraph || '';

            return {
                isCluster: true,
                status: String(status),
                message: String(message),
                oldGraph: parseClusterPreviewGraph(oldGraphRaw),
                newGraph: parseClusterPreviewGraph(newGraphRaw),
                oldHash: oldProps.clusterHash || oldProps.ClusterHash || props.clusterHash || props.ClusterHash || '',
                newHash: props.clusterHash || props.ClusterHash || oldProps.clusterHash || oldProps.ClusterHash || '',
                oldSize: oldProps.clusterSize || oldProps.ClusterSize || props.clusterSize || props.ClusterSize || '',
                newSize: props.clusterSize || props.ClusterSize || oldProps.clusterSize || oldProps.ClusterSize || ''
            };
        }

        function clusterInternalsChanged(node, preview) {
            const nodeStatus = String(node?.status || '').toLowerCase();
            if (nodeStatus === 'added' || nodeStatus === 'removed') return true;

            if (hasExplicitClusterInternalsChange(preview)) return true;

            return nodeStatus === 'modified';
        }

        function parseClusterPreviewGraph(raw) {
            if (!raw) return null;
            try {
                const parsed = typeof raw === 'string' ? JSON.parse(raw) : raw;
                const nodes = Array.isArray(parsed.nodes) ? parsed.nodes : (Array.isArray(parsed.Nodes) ? parsed.Nodes : []);
                const edges = Array.isArray(parsed.edges) ? parsed.edges : (Array.isArray(parsed.Edges) ? parsed.Edges : []);
                const truncated = parsed.truncated !== undefined ? !!parsed.truncated : !!parsed.Truncated;
                return { nodes, edges, truncated };
            } catch {
                return null;
            }
        }

        function normalizedClusterPreviewValue(value, fallback = '') {
            if (value === null || value === undefined) return fallback;
            return String(value).trim();
        }

        function stableClusterPreviewObjectSignature(obj) {
            const source = (obj && typeof obj === 'object') ? obj : {};
            const keys = Object.keys(source).sort((a, b) => a.localeCompare(b));
            const normalized = {};
            keys.forEach(key => {
                normalized[key] = normalizedClusterPreviewValue(source[key]);
            });
            return JSON.stringify(normalized);
        }

        function stablePortOptionSignature(options) {
            return stableClusterPreviewObjectSignature(normalizePortOptionSignatureMap(options));
        }

        function stableClusterPreviewPortSignature(port, fallbackKind = '') {
            const wireDisplayRaw = port?.wireDisplay ?? port?.WireDisplay ?? 0;
            const wireDisplay = parseWireDisplayMode(wireDisplayRaw);
            return JSON.stringify({
                id: normalizedClusterPreviewValue(port?.id ?? port?.Id),
                name: normalizedClusterPreviewValue(port?.name ?? port?.Name),
                nickname: normalizedClusterPreviewValue(port?.nickname ?? port?.Nickname),
                kind: normalizedClusterPreviewValue(port?.kind ?? port?.Kind, fallbackKind),
                type: normalizedClusterPreviewValue(port?.type ?? port?.Type),
                value: normalizedClusterPreviewValue(port?.value ?? port?.Value),
                options: stablePortOptionSignature(port?.options ?? port?.Options),
                wireDisplay
            });
        }

        function stableClusterPreviewPortsSignature(ports, fallbackKind = '') {
            if (!Array.isArray(ports) || ports.length === 0) return '[]';
            const normalized = ports.map((port, index) => ({
                key: normalizedClusterPreviewValue(port?.id ?? port?.Id) || `idx:${index}`,
                sig: stableClusterPreviewPortSignature(port, fallbackKind)
            }));
            normalized.sort((a, b) => a.key.localeCompare(b.key));
            return JSON.stringify(normalized);
        }

        function clusterPreviewNodeSignature(node) {
            if (!node) return '';

            const x = Number(node.x ?? node.X ?? 0);
            const y = Number(node.y ?? node.Y ?? 0);
            const w = Number(node.w ?? node.W ?? 110);
            const h = Number(node.h ?? node.H ?? 38);
            const properties = node.properties || node.Properties || {};
            const inputs = node.inputs || node.Inputs || [];
            const outputs = node.outputs || node.Outputs || [];

            return JSON.stringify({
                name: normalizedClusterPreviewValue(node.name ?? node.Name),
                nickname: normalizedClusterPreviewValue(node.nickname ?? node.Nickname),
                x: Number.isFinite(x) ? Number(x.toFixed(3)) : 0,
                y: Number.isFinite(y) ? Number(y.toFixed(3)) : 0,
                w: Number.isFinite(w) ? Number(w.toFixed(3)) : 110,
                h: Number.isFinite(h) ? Number(h.toFixed(3)) : 38,
                isCluster: isTrueish(node.isCluster ?? node.IsCluster),
                clusterHash: normalizedClusterPreviewValue(node.clusterHash ?? node.ClusterHash),
                nestedClusterCount: Number(node.nestedClusterCount ?? node.NestedClusterCount ?? 0),
                nestedClusterChanges: Number(node.nestedClusterChanges ?? node.NestedClusterChanges ?? 0),
                properties: stableClusterPreviewObjectSignature(properties),
                inputs: stableClusterPreviewPortsSignature(inputs, 'input'),
                outputs: stableClusterPreviewPortsSignature(outputs, 'output')
            });
        }

        function clusterPreviewNodeChanged(oldNode, newNode) {
            return clusterPreviewNodeSignature(oldNode) !== clusterPreviewNodeSignature(newNode);
        }

        function normalizeClusterPreviewPortList(ports, defaultKind) {
            if (!Array.isArray(ports)) return [];
            return ports.map((p, index) => {
                const id = normalizedClusterPreviewValue(p?.id ?? p?.Id);
                const name = normalizedClusterPreviewValue(p?.name ?? p?.Name);
                const nickname = normalizedClusterPreviewValue(p?.nickname ?? p?.Nickname);
                const kind = normalizedClusterPreviewValue(p?.kind ?? p?.Kind, defaultKind);
                const type = normalizedClusterPreviewValue(p?.type ?? p?.Type);
                const value = normalizedClusterPreviewValue(p?.value ?? p?.Value);
                const options = normalizePortOptionMap(p?.options ?? p?.Options);
                const wireDisplay = parseWireDisplayMode(p?.wireDisplay ?? p?.WireDisplay ?? p?.wireDisplayMode ?? p?.WireDisplayMode ?? 0);
                const key = id ? `id:${canonicalizeId(id)}` : `idx:${index}`;
                return { id, name, nickname, kind, type, value, options, wireDisplay, key };
            });
        }

        function mergeClusterPreviewPorts(oldPorts, newPorts, defaultKind) {
            const oldList = normalizeClusterPreviewPortList(oldPorts, defaultKind);
            const newList = normalizeClusterPreviewPortList(newPorts, defaultKind);
            const oldMap = new Map(oldList.map(p => [p.key, p]));
            const newMap = new Map(newList.map(p => [p.key, p]));
            const orderedKeys = [];
            const seen = new Set();

            oldList.forEach(p => {
                if (!seen.has(p.key)) {
                    orderedKeys.push(p.key);
                    seen.add(p.key);
                }
            });
            newList.forEach(p => {
                if (!seen.has(p.key)) {
                    orderedKeys.push(p.key);
                    seen.add(p.key);
                }
            });

            return orderedKeys.map(key => {
                const o = oldMap.get(key);
                const n = newMap.get(key);
                if (o && n) {
                    const valueChanged = o.value !== n.value;
                    const optionsChanged = stablePortOptionSignature(o.options) !== stablePortOptionSignature(n.options);
                    const changed = valueChanged ||
                        o.name !== n.name ||
                        o.nickname !== n.nickname ||
                        o.kind !== n.kind ||
                        o.type !== n.type ||
                        optionsChanged ||
                        o.wireDisplay !== n.wireDisplay;
                    return {
                        id: n.id || o.id || '',
                        name: n.name || o.name || '',
                        nickname: n.nickname || o.nickname || '',
                        kind: n.kind || o.kind || defaultKind,
                        type: n.type || o.type || '',
                        value: n.value || '',
                        valueOld: o.value || '',
                        valueNew: n.value || '',
                        valueChanged,
                        options: n.options || {},
                        optionsChanged,
                        optionsOld: optionsChanged ? o.options : {},
                        optionsNew: optionsChanged ? n.options : {},
                        status: changed ? 'modified' : 'same',
                        wireDisplay: n.wireDisplay
                    };
                }

                if (n) {
                    return {
                        id: n.id || '',
                        name: n.name || '',
                        nickname: n.nickname || '',
                        kind: n.kind || defaultKind,
                        type: n.type || '',
                        value: n.value || '',
                        options: n.options || {},
                        optionsChanged: false,
                        status: 'added',
                        wireDisplay: n.wireDisplay
                    };
                }

                return {
                    id: o?.id || '',
                    name: o?.name || '',
                    nickname: o?.nickname || '',
                    kind: o?.kind || defaultKind,
                    type: o?.type || '',
                    value: o?.value || '',
                    options: o?.options || {},
                    optionsChanged: false,
                    status: 'removed',
                    wireDisplay: o?.wireDisplay ?? 0
                };
            });
        }

        function buildClusterPreviewPropertiesOld(oldProps, newProps) {
            const oldObj = normalizePropertyMap(oldProps);
            const newObj = normalizePropertyMap(newProps);
            const previous = {};
            Object.keys(oldObj).forEach(key => {
                if (Object.prototype.hasOwnProperty.call(newObj, key) && oldObj[key] !== newObj[key]) {
                    previous[key] = oldObj[key];
                }
            });
            return previous;
        }

        function buildClusterDiffGraph(oldGraph, newGraph) {
            const oldNodes = new Map();
            const newNodes = new Map();

            (oldGraph?.nodes || []).forEach((n, i) => {
                const id = String(n.id || n.Id || `old_node_${i}`);
                oldNodes.set(id, n);
            });

            (newGraph?.nodes || []).forEach((n, i) => {
                const id = String(n.id || n.Id || `new_node_${i}`);
                newNodes.set(id, n);
            });

            const nodeIds = new Set([...oldNodes.keys(), ...newNodes.keys()]);
            const nodes = [];

            nodeIds.forEach(id => {
                const o = oldNodes.get(id);
                const n = newNodes.get(id);
                const base = n || o || {};
                const nodeStatus = o && n
                    ? (clusterPreviewNodeChanged(o, n) ? 'modified' : 'same')
                    : (n ? 'added' : 'removed');
                const oldProperties = o?.properties || o?.Properties || {};
                const newProperties = n?.properties || n?.Properties || {};
                const oldInputs = o?.inputs || o?.Inputs || [];
                const newInputs = n?.inputs || n?.Inputs || [];
                const oldOutputs = o?.outputs || o?.Outputs || [];
                const newOutputs = n?.outputs || n?.Outputs || [];
                nodes.push({
                    id: String(id),
                    name: base.nickname || base.Nickname || base.name || base.Name || id,
                    x: Number(base.x ?? base.X ?? 0),
                    y: Number(base.y ?? base.Y ?? 0),
                    w: Number(base.w ?? base.W ?? 110),
                    h: Number(base.h ?? base.H ?? 38),
                    properties: (n ? newProperties : oldProperties),
                    propertiesOld: (o && n && nodeStatus === 'modified')
                        ? buildClusterPreviewPropertiesOld(oldProperties, newProperties)
                        : {},
                    inputs: mergeClusterPreviewPorts(oldInputs, newInputs, 'input'),
                    outputs: mergeClusterPreviewPorts(oldOutputs, newOutputs, 'output'),
                    status: nodeStatus
                });
            });

            const edgeKey = edge => {
                const source = edge.source || edge.Source || '';
                const sourcePort = edge.sourcePort || edge.SourcePort || '';
                const target = edge.target || edge.Target || '';
                const targetPort = edge.targetPort || edge.TargetPort || '';
                return `${source}:${sourcePort}->${target}:${targetPort}`;
            };

            const oldEdges = new Map((oldGraph?.edges || []).map(e => [edgeKey(e), e]));
            const newEdges = new Map((newGraph?.edges || []).map(e => [edgeKey(e), e]));
            const edgeKeys = new Set([...oldEdges.keys(), ...newEdges.keys()]);
            const edges = [];

            edgeKeys.forEach(key => {
                const o = oldEdges.get(key);
                const n = newEdges.get(key);
                const base = n || o || {};
                const source = base.source || base.Source;
                const target = base.target || base.Target;
                if (!source || !target) return;

                edges.push({
                    source: String(source),
                    sourcePort: base.sourcePort || base.SourcePort || '',
                    target: String(target),
                    targetPort: base.targetPort || base.TargetPort || '',
                    status: o && n ? 'same' : (n ? 'added' : 'removed')
                });
            });

            return { nodes, edges };
        }

        function openClusterGraphTab(node, preview) {
            if (!node) return false;
            const clusterPreview = preview && preview.isCluster ? preview : getClusterPreviewData(node);
            if (!clusterPreview.isCluster) return false;

            const parentTab = getActiveGraphTab();
            const parentTabId = parentTab?.id || ROOT_GRAPH_TAB_ID;
            const existingTab = state.graphTabs.find(t =>
                t.type === 'cluster' &&
                t.sourceNodeId === node.id &&
                t.parentTabId === parentTabId
            );
            const internalsChanged = clusterInternalsChanged(node, clusterPreview);
            const nodeStatus = String(node.status || '').toLowerCase();

            let oldGraph = clusterPreview.oldGraph;
            let newGraph = clusterPreview.newGraph;
            const hasBothSides = !!oldGraph && !!newGraph;

            if (!hasBothSides) {
                // Avoid misleading "all added/removed" views when we only have one side for unchanged nodes.
                if (nodeStatus !== 'added' && nodeStatus !== 'removed') {
                    const stable = newGraph || oldGraph;
                    oldGraph = stable;
                    newGraph = stable;
                }
            }

            if (!internalsChanged) {
                const stable = newGraph || oldGraph;
                oldGraph = stable;
                newGraph = stable;
            }

            const diffGraph = buildClusterDiffGraph(oldGraph, newGraph);
            const normalizedNodes = diffGraph.nodes.map(n => normalizeNode({
                ...n,
                name: n.name || n.id || 'Cluster Component',
                nickname: n.name || n.id || 'Cluster Component'
            }));
            const { componentNodes, groupNodes } = splitComponentAndGroupNodes(normalizedNodes);
            applyModificationStatusToNodes(componentNodes, showHighLevelModificationDetails());
            const renderableGroups = buildRenderableGroups(groupNodes, componentNodes);

            const nodeMap = buildNodeLookupMap(componentNodes);
            const edges = diffGraph.edges
                .map(normalizeEdge)
                .map(e => {
                    const sourceNode = nodeMap.get(e.source) || nodeMap.get(canonicalizeId(e.source));
                    const targetNode = nodeMap.get(e.target) || nodeMap.get(canonicalizeId(e.target));
                    if (!sourceNode || !targetNode) return null;

                    return {
                        ...e,
                        source: sourceNode.id,
                        target: targetNode.id,
                        sourceNode,
                        targetNode
                    };
                })
                .filter(e => !!e);
            const hasVisibleInternalDiff = componentNodes.some(n => n.status !== 'same') ||
                edges.some(e => e.status !== 'same');

            const baseName = getGraphNodeTabLabel(node);
            const parentSegments = Array.isArray(parentTab?.breadcrumbSegments) && parentTab.breadcrumbSegments.length
                ? parentTab.breadcrumbSegments
                : ['Main'];
            const breadcrumbSegments = [...parentSegments, baseName];
            const tabTitle = breadcrumbSegments.join(' / ');

            let targetTab = existingTab;
            if (!targetTab) {
                targetTab = createGraphTab({
                    id: `graph-cluster-${state.nextGraphTabId++}`,
                    title: tabTitle,
                    type: 'cluster',
                    sourceNodeId: node.id,
                    parentTabId,
                    breadcrumbSegments,
                    nodes: componentNodes,
                    groups: renderableGroups,
                    edges
                });
                state.graphTabs.push(targetTab);
            } else {
                targetTab.title = tabTitle;
                targetTab.parentTabId = parentTabId;
                targetTab.breadcrumbSegments = breadcrumbSegments;
                targetTab.nodes = componentNodes;
                targetTab.groups = renderableGroups;
                targetTab.edges = edges;
                targetTab.bounds = calculateBoundsForNodes(componentNodes);
                targetTab.view = null;
                targetTab.selected = null;
            }

            switchGraphTab(targetTab.id, { fit: true });

            const wasTruncated = !!oldGraph?.truncated || !!newGraph?.truncated;
            if (wasTruncated) {
                showPerf(clusterPreview.message || `Cluster preview truncated to ${diffGraph.nodes.length} nodes / ${diffGraph.edges.length} edges.`);
                window.setTimeout(() => {
                    if (!state.processingData && !state.largeModelMode) {
                        showPerf('');
                    }
                }, 3200);
            }

            if (componentNodes.length === 0 && renderableGroups.length === 0) {
                showPerf(clusterPreview.message || 'No internal cluster graph available for this component.');
                window.setTimeout(() => {
                    if (!state.processingData && !state.largeModelMode) {
                        showPerf('');
                    }
                }, 2200);
            } else if (internalsChanged && !hasVisibleInternalDiff) {
                const fallbackMessage = wasTruncated
                    ? 'Cluster internals changed, but preview is truncated so exact changes are not fully visible.'
                    : 'Cluster internals changed, but this preview has no explicit node/edge deltas.';
                showPerf(clusterPreview.message || fallbackMessage);
                window.setTimeout(() => {
                    if (!state.processingData && !state.largeModelMode) {
                        showPerf('');
                    }
                }, 2600);
            }

            return true;
        }

        function openClusterPreview() {
            const selectedNode = state.nodes.find(n => n.id === state.selected);
            if (!selectedNode) return;
            openClusterGraphTab(selectedNode);
        }

        function getPropertyValueIgnoreCase(props, key) {
            if (!props || typeof props !== 'object') return { found: false, value: '', actualKey: key };
            if (Object.prototype.hasOwnProperty.call(props, key)) {
                return { found: true, value: String(props[key] ?? ''), actualKey: key };
            }

            const target = String(key || '').toLowerCase();
            for (const [candidateKey, candidateValue] of Object.entries(props)) {
                if (String(candidateKey || '').toLowerCase() === target) {
                    return { found: true, value: String(candidateValue ?? ''), actualKey: candidateKey };
                }
            }

            return { found: false, value: '', actualKey: key };
        }

        function normalizePropertyKeyName(key) {
            return String(key || '').trim().toLowerCase();
        }

        function isBooleanPortOptionKey(key) {
            const normalized = normalizePropertyKeyName(key);
            return BOOLEAN_PORT_OPTION_CANONICAL_KEYS.some(candidate => normalizePropertyKeyName(candidate) === normalized);
        }

        function formatPortOptionToken(key, value, showDefault = false) {
            const label = String(key || '').trim();
            if (!label) return '';

            const normalizedValue = String(value ?? '').trim();
            if (!normalizedValue) return showDefault ? `${label}: default` : '';
            if (normalizedValue === 'true') return label;
            if (normalizedValue === 'false') return `${label}: false`;
            return `${label}: ${normalizedValue}`;
        }

        function getPortOptionDisplayValue(optionMap, key) {
            if (Object.prototype.hasOwnProperty.call(optionMap, key)) {
                return optionMap[key];
            }

            return isBooleanPortOptionKey(key) ? 'false' : '';
        }

        function renderPortOptionText(options) {
            const normalized = normalizePortOptionMap(options);
            const keys = Object.keys(normalized).sort((a, b) => a.localeCompare(b));
            return keys
                .map(key => formatPortOptionToken(key, normalized[key], false))
                .filter(Boolean)
                .join(', ');
        }

        function renderPortOptionDiffText(oldOptions, newOptions) {
            const oldMap = normalizePortOptionMap(oldOptions);
            const newMap = normalizePortOptionMap(newOptions);
            const keys = Array.from(new Set([...Object.keys(oldMap), ...Object.keys(newMap)]))
                .sort((a, b) => a.localeCompare(b));

            return keys
                .map(key => {
                    const oldValue = getPortOptionDisplayValue(oldMap, key);
                    const newValue = getPortOptionDisplayValue(newMap, key);
                    if (oldValue === newValue) return '';
                    return `${formatPortOptionToken(key, oldValue, true)} <svg class="ico" width="12" height="12" style="margin:0 2px;"><use href="/vendor/phosphor/icons.svg#ph-arrow-right"></use></svg> ${formatPortOptionToken(key, newValue, true)}`;
                })
                .filter(Boolean)
                .join(', ');
        }

        const PANEL_VALUE_PROPERTY_KEYS = new Set([
            'usertext',
            'panelcontent',
            'text',
            'string',
            'value'
        ]);

        const USER_INPUT_PRIORITY = new Map([
            ['usertext', 1],
            ['panelcontent', 2],
            ['text', 3],
            ['string', 4],
            ['value', 5],
            ['url', 6],
            ['uri', 7],
            ['path', 8],
            ['filepath', 9],
            ['expression', 10],
            ['note', 11],
            ['comment', 12],
            ['message', 13]
        ]);

        const USER_INPUT_EXCLUDE_KEYS = new Set([
            'guid',
            'instanceguid',
            'source',
            'sourcecount',
            'bounds',
            'pivot',
            'scrollratio',
            'optional'
        ]);

        function isPanelValuePropertyKey(key) {
            return PANEL_VALUE_PROPERTY_KEYS.has(normalizePropertyKeyName(key));
        }

        function isUserInputPropertyKey(key) {
            const normalized = normalizePropertyKeyName(key);
            if (!normalized || USER_INPUT_EXCLUDE_KEYS.has(normalized)) return false;
            if (USER_INPUT_PRIORITY.has(normalized)) return true;
            return /(user.?text|panel.?content|url|uri|file.?path|^path$|note|comment|message|prompt)/i.test(normalized);
        }

        function isPanelLikeNode(node) {
            const name = String(node?.name || '').toLowerCase();
            const nickname = String(node?.nickname || '').toLowerCase();
            if (name.includes('panel') || nickname.includes('panel')) return true;

            const props = node?.properties || {};
            return Object.keys(props).some(k => isPanelValuePropertyKey(k));
        }

        function getPanelValueDetails(node) {
            if (!node) {
                return { hasAny: false, newValue: '', oldValue: '', changed: false };
            }

            const props = node.properties || {};
            const oldProps = node.propertiesOld || {};
            const candidates = ['UserText', 'PanelContent', 'Text', 'String', 'Value'];

            let newValue = '';
            let oldValue = '';
            let hasNew = false;
            let hasOld = false;

            for (const key of candidates) {
                const lookup = getPropertyValueIgnoreCase(props, key);
                if (lookup.found) {
                    hasNew = true;
                    newValue = String(lookup.value ?? '');
                    break;
                }
            }

            for (const key of candidates) {
                const lookup = getPropertyValueIgnoreCase(oldProps, key);
                if (lookup.found) {
                    hasOld = true;
                    oldValue = String(lookup.value ?? '');
                    break;
                }
            }

            const hasAny = hasNew || hasOld;
            const changed = hasNew && hasOld ? oldValue !== newValue : (hasNew ? newValue.length > 0 : oldValue.length > 0);
            return { hasAny, hasNew, hasOld, newValue, oldValue, changed };
        }

        function getUserInputEntries(node) {
            const props = node?.properties || {};
            const oldProps = node?.propertiesOld || {};
            const keys = new Set([...Object.keys(props), ...Object.keys(oldProps)]);
            const entries = [];

            keys.forEach(key => {
                if (!isUserInputPropertyKey(key)) return;
                if (isClusterMetadataPropertyKey(key)) return;
                if (isScriptNoiseKey(key)) return;

                const newLookup = getPropertyValueIgnoreCase(props, key);
                const oldLookup = getPropertyValueIgnoreCase(oldProps, key);
                const hasNew = newLookup.found;
                const hasOld = oldLookup.found;
                const newValue = hasNew ? String(newLookup.value ?? '') : '';
                const oldValue = hasOld ? String(oldLookup.value ?? '') : '';

                if (!hasNew && !hasOld) return;
                if (!newValue.length && !oldValue.length) return;

                entries.push({
                    key: newLookup.actualKey || oldLookup.actualKey || key,
                    normalizedKey: normalizePropertyKeyName(key),
                    hasNew,
                    hasOld,
                    newValue,
                    oldValue
                });
            });

            entries.sort((a, b) => {
                const pa = USER_INPUT_PRIORITY.get(a.normalizedKey) || 999;
                const pb = USER_INPUT_PRIORITY.get(b.normalizedKey) || 999;
                if (pa !== pb) return pa - pb;
                return a.key.localeCompare(b.key);
            });

            return entries;
        }

        function renderUserInputSummary(node) {
            const entries = getUserInputEntries(node);
            if (!entries.length) return '';

            const rows = entries.map(entry => {
                if (entry.hasOld && entry.hasNew && entry.oldValue !== entry.newValue) {
                    return `
                        <div class="prop-row" style="flex-direction: column; gap: 4px; align-items: flex-start;">
                            <span class="prop-label">${escapeHtml(entry.key)}</span>
                            <div style="display: flex; align-items: center; gap: 8px; width: 100%;">
                                <span class="prop-value" style="background: rgba(200, 40, 40, 0.2); padding: 6px; border-radius: 4px; flex: 1; font-size: 11px; text-decoration: line-through; opacity: 0.7; white-space: pre-wrap;">${escapeHtml(entry.oldValue)}</span>
                                <svg class="ico" width="12" height="12" style="margin:0 4px; color:var(--text-secondary);"><use href="/vendor/phosphor/icons.svg#ph-arrow-right"></use></svg>
                                <span class="prop-value" style="background: rgba(40, 200, 40, 0.2); padding: 6px; border-radius: 4px; flex: 1; font-size: 11px; white-space: pre-wrap;">${escapeHtml(entry.newValue)}</span>
                            </div>
                        </div>
                    `;
                }

                const stableValue = entry.hasNew ? entry.newValue : entry.oldValue;
                return `
                    <div class="prop-row" style="flex-direction: column; gap: 4px; align-items: flex-start;">
                        <span class="prop-label">${escapeHtml(entry.key)}</span>
                        <span class="prop-value" style="background: rgba(0,0,0,0.2); padding: 6px; border-radius: 4px; width: 100%; white-space: pre-wrap; font-size: 11px; max-height: 180px; overflow-y: auto;">${escapeHtml(stableValue)}</span>
                    </div>
                `;
            }).join('');

            return `
                <div class="detail-card" style="border-left: 3px solid var(--accent-color);">
                    <div class="node-name" style="font-size: 12px; margin-bottom: 8px;">User Inputs</div>
                    ${rows}
                </div>
            `;
        }

        function getChangedPropertyCount(node) {
            const props = node?.properties || {};
            const oldProps = node?.propertiesOld || {};
            const originalStatus = getNodeOriginalStatus(node);
            const hasSparseOldValues = originalStatus === 'modified' || originalStatus === 'same';
            let changed = 0;

            Object.entries(props).forEach(([key, val]) => {
                if (isClusterMetadataPropertyKey(key)) return;
                const prior = getPropertyValueIgnoreCase(oldProps, key);
                const newValue = String(val ?? '');
                if (!prior.found) {
                    // Modified/same nodes usually only provide old values for changed keys.
                    if (!hasSparseOldValues && newValue.length > 0) changed++;
                    return;
                }
                if (String(prior.value ?? '') !== newValue) changed++;
            });

            return changed;
        }

        function getClusterByteCount(node, useOld = false) {
            // Helper to extract byte count from cluster properties
            const props = useOld ? (node.propertiesOld || {}) : (node.properties || {});
            
            // Try ClusterSize first
            const clusterSize = props.clusterSize || props.ClusterSize;
            if (clusterSize) return parseInt(clusterSize);
            
            // Fallback: parse ClusterDocument string like "<8029 bytes>"
            const clusterDoc = props.clusterDocument || props.ClusterDocument;
            if (clusterDoc && typeof clusterDoc === 'string') {
                const match = clusterDoc.match(/<(\d+)\s*bytes?>/i);
                if (match) return parseInt(match[1]);
            }
            
            return null;
        }

        function getClusterHash(node, useOld = false) {
            const props = useOld ? (node.propertiesOld || {}) : (node.properties || {});
            return props.clusterHash || props.ClusterHash || null;
        }

        function isClusterChanged(node) {
            // Compare cluster using size first, then hash
            const newBytes = getClusterByteCount(node, false);
            const oldBytes = getClusterByteCount(node, true);
            if (newBytes !== null && oldBytes !== null && newBytes !== oldBytes) {
                return true;
            }
            
            const newHash = getClusterHash(node, false);
            const oldHash = getClusterHash(node, true);
            if (newHash && oldHash && newHash !== oldHash) {
                return true;
            }
            
            return false;
        }

        function renderChangeSummary(node) {
            if (!node || computeEffectiveNodeStatus(node) !== 'modified') return '';

            const includeHighLevel = showHighLevelModificationDetails();
            const reasons = [];
            const backendReasons = Array.isArray(node.riskReasons) ? node.riskReasons : [];
            if (includeHighLevel) {
                backendReasons.forEach(reason => {
                    const text = String(reason || '').trim();
                    if (text && !reasons.includes(text)) reasons.push(text);
                });
            }

            const changedProps = getChangedPropertyCount(node);
            if (changedProps > 0) {
                reasons.push(`${changedProps} property value${changedProps === 1 ? '' : 's'} changed`);
            }

            if (includeHighLevel) {
                const moved = Number(node.dx || 0) !== 0 || Number(node.dy || 0) !== 0;
                if (moved) {
                    reasons.push(`Moved by ${Number(node.dx || 0).toFixed(1)}, ${Number(node.dy || 0).toFixed(1)}`);
                }

                const connChanges = Number(node.inAdded || 0) + Number(node.inRemoved || 0) + Number(node.outAdded || 0) + Number(node.outRemoved || 0);
                if (connChanges > 0) {
                    reasons.push(`Connections changed (in +${node.inAdded || 0}/-${node.inRemoved || 0}, out +${node.outAdded || 0}/-${node.outRemoved || 0})`);
                }
            }

            const scriptEntries = getScriptPropertyEntries(node);
            const changedScript = scriptEntries.filter(e => e.isChanged).length;
            if (changedScript > 0) {
                reasons.push(`${changedScript} script block${changedScript === 1 ? '' : 's'} changed`);
            }

            const clusterPreview = getClusterPreviewData(node);
            if (clusterPreview.isCluster && isClusterChanged(node)) {
                reasons.push('Cluster internals changed');
            }

            if (reasons.length === 0) {
                reasons.push(includeHighLevel
                    ? 'Marked as modified by backend diff heuristics (high-level), but no explicit value-level change was detected.'
                    : 'No explicit value-level changes detected for this component.');
            }

            const rows = reasons.map(r => `<div class="prop-row"><span class="prop-label">Change</span><span class="prop-value">${escapeHtml(r)}</span></div>`).join('');
            return `
                <div class="detail-card" style="border-left: 3px solid var(--modified);">
                    <div class="node-name" style="font-size: 12px; margin-bottom: 8px;">What Changed</div>
                    ${rows}
                </div>
            `;
        }

        function renderProperties(node) {
            if (!node.properties || Object.keys(node.properties).length === 0) return '';
            const effectiveStatus = computeEffectiveNodeStatus(node);
            const originalStatus = getNodeOriginalStatus(node);
            const hasSparseOldValues = originalStatus === 'modified' || originalStatus === 'same';
            const showOnlyChangedValues =
                !showHighLevelModificationDetails() &&
                effectiveStatus === 'modified';

            // Check if this is a cluster node
            const isCluster = isClusterLikeNode(node);
            const isPanel = isPanelLikeNode(node);
            const panelValue = getPanelValueDetails(node);
            const clusterChanged = isClusterChanged(node);

            let clusterSection = '';
            if (isCluster) {
                // Format size in bytes to KB
                const formatSize = (sizeBytes) => {
                    if (!sizeBytes) return null;
                    const bytes = parseInt(sizeBytes);
                    if (bytes < 1024) return `${bytes} bytes`;
                    return `${(bytes / 1024).toFixed(1)} KB`;
                };
                // Get byte counts with ClusterDocument fallback
                const clusterBytes = getClusterByteCount(node, false);
                const oldClusterBytes = getClusterByteCount(node, true);
                const clusterHash = getClusterHash(node, false);
                const oldClusterHash = getClusterHash(node, true);
                
                const sizeFormatted = formatSize(clusterBytes) || 'Unknown';
                const oldSizeFormatted = formatSize(oldClusterBytes);

                clusterSection = `
                    <div class="detail-card" style="border-left: 3px solid #7c4dff;">
                        <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 12px;">
                            <svg class="ico" width="20" height="20" style="color:var(--text-secondary);"><use href="/vendor/phosphor/icons.svg#ph-package"></use></svg>
                            <div class="node-name" style="font-size: 13px; margin-bottom: 0; color: #b388ff;">Cluster Component</div>
                        </div>
                        <div class="prop-row">
                            <span class="prop-label">Type</span>
                            <span class="prop-value" style="color: #b388ff;">Embedded Sub-Definition</span>
                        </div>
                        <div class="prop-row">
                            <span class="prop-label">Size</span>
                            <span class="prop-value">${sizeFormatted}</span>
                        </div>
                        ${clusterChanged ? `
                        <div style="margin-top: 8px; padding: 8px; background: rgba(245, 127, 23, 0.15); border: 1px solid rgba(245, 127, 23, 0.3); border-radius: 6px;">
                            <div style="font-size: 11px; font-weight: 600; color: var(--modified); margin-bottom: 4px; display: flex; align-items: center; gap: 4px;">
                                <svg class="ico" width="14" height="14"><use href="/vendor/phosphor/icons.svg#ph-warning"></use></svg>
                                Cluster Internals Modified
                            </div>
                            <div style="font-size: 11px; color: var(--text-secondary);">
                                The embedded sub-definition has changed.
                                ${oldSizeFormatted ? `<br>Size: ${oldSizeFormatted} <svg class="ico" width="10" height="10" style="margin:0 2px;"><use href="/vendor/phosphor/icons.svg#ph-arrow-right"></use></svg> ${sizeFormatted}` : ''}
                            </div>
                            ${oldClusterHash && clusterHash ? `
                            <div class="prop-row" style="margin-top: 6px;">
                                <span class="prop-label">Old Hash</span>
                                <span class="prop-value" style="font-size: 11px;">${oldClusterHash.substring(0, 12)}...</span>
                            </div>
                            <div class="prop-row">
                                <span class="prop-label">New Hash</span>
                                <span class="prop-value" style="font-size: 11px;">${clusterHash.substring(0, 12)}...</span>
                            </div>
                            ` : ''}
                        </div>
                        ` : `
                        <div style="margin-top: 8px; padding: 6px 8px; background: rgba(68, 183, 130, 0.1); border-radius: 4px; font-size: 11px; color: var(--success);">
                            <svg class="ico" width="16" height="16" style="color:#44B782;"><use href="/vendor/phosphor/icons.svg#ph-check"></use></svg> Cluster internals unchanged
                        </div>
                        `}
                        <button class="cluster-preview-open"
                            style="width: 100%; margin-top: 10px; background: var(--bg-card); border: 1px solid var(--border-color); color: #b388ff; padding: 8px; border-radius: 6px; cursor: pointer; font-size: 12px; font-weight: 600;">
                            Open Cluster Tab
                        </button>
                    </div>
                `;
            }

            let panelSection = '';
            if (isPanel && panelValue.hasAny) {
                if (panelValue.changed && panelValue.hasOld && panelValue.hasNew) {
                    panelSection = `
                        <div class="detail-card" style="border-left: 3px solid var(--accent-color);">
                            <div class="node-name" style="font-size: 12px; margin-bottom: 8px;">Panel Value</div>
                            <div class="prop-row" style="flex-direction: column; gap: 4px; align-items: flex-start;">
                                <span class="prop-label">UserText</span>
                                <div style="display: flex; align-items: center; gap: 8px; width: 100%;">
                                    <span class="prop-value" style="background: rgba(200, 40, 40, 0.2); padding: 6px; border-radius: 4px; flex: 1; font-size: 11px; text-decoration: line-through; opacity: 0.7; white-space: pre-wrap;">${escapeHtml(panelValue.oldValue)}</span>
                                    <svg class="ico" width="12" height="12" style="margin:0 4px; color:var(--text-secondary);"><use href="/vendor/phosphor/icons.svg#ph-arrow-right"></use></svg>
                                    <span class="prop-value" style="background: rgba(40, 200, 40, 0.2); padding: 6px; border-radius: 4px; flex: 1; font-size: 11px; white-space: pre-wrap;">${escapeHtml(panelValue.newValue)}</span>
                                </div>
                            </div>
                        </div>
                    `;
                } else {
                    const stableValue = panelValue.hasNew ? panelValue.newValue : panelValue.oldValue;
                    panelSection = `
                        <div class="detail-card" style="border-left: 3px solid var(--accent-color);">
                            <div class="node-name" style="font-size: 12px; margin-bottom: 8px;">Panel Value</div>
                            <div class="prop-row" style="flex-direction: column; gap: 4px; align-items: flex-start;">
                                <span class="prop-label">UserText</span>
                                <span class="prop-value" style="background: rgba(0,0,0,0.2); padding: 6px; border-radius: 4px; width: 100%; white-space: pre-wrap; font-size: 11px; max-height: 180px; overflow-y: auto;">${escapeHtml(stableValue)}</span>
                            </div>
                        </div>
                    `;
                }
            }

            let rows = '';
            for (const [key, val] of Object.entries(node.properties)) {
                // Skip cluster-specific keys (rendered in the dedicated section above)
                if (isClusterMetadataPropertyKey(key)) continue;
                // User-entered inputs are surfaced at the top of the inspector.
                if (isUserInputPropertyKey(key)) continue;
                // Panel UserText is rendered in a dedicated section above.
                if (isPanel && panelValue.hasAny && isPanelValuePropertyKey(key)) continue;

                // Check if this property changed (has old value)
                const oldLookup = getPropertyValueIgnoreCase(node.propertiesOld || {}, key);
                const hasOldValue = oldLookup.found;
                const oldValue = hasOldValue ? String(oldLookup.value ?? '') : '';
                const newValue = String(val ?? '');
                const valueAdded = !hasOldValue && !hasSparseOldValues && newValue.length > 0;
                const valueRemoved = hasOldValue && oldValue.length > 0 && newValue.length === 0;
                const valueChanged = hasOldValue && oldValue !== newValue;
                const hasValueDelta = valueChanged || valueAdded || valueRemoved;

                // Check if it's a script/code property
                const isScript = isLikelyScriptPropertyKey(key);

                if (isScript) {
                    const scriptChanged = hasOldValue
                        ? normalizeScriptForCompare(oldValue) !== normalizeScriptForCompare(newValue)
                        : newValue.length > 0;
                    if (showOnlyChangedValues && !scriptChanged) continue;
                    const openButton = `
                            <button class="script-diff-open" data-script-key="${encodeURIComponent(key)}"
                                style="width: 100%; background: var(--bg-card); border: 1px solid var(--border-color); color: var(--accent-color); padding: 8px; border-radius: 6px; cursor: pointer; font-size: 12px; font-weight: 500; display: flex; align-items: center; justify-content: center; gap: 6px; transition: all 0.2s;">
                                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                                    <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
                                    <polyline points="14 2 14 8 20 8"></polyline>
                                </svg>
                                ${scriptChanged ? 'Open Script Diff' : 'Open Script'}
                            </button>
                    `;
                    rows += `
                        <div class="prop-row" style="flex-direction: column; gap: 8px; align-items: flex-start; padding-bottom: 12px;">
                            <div style="display: flex; justify-content: space-between; width: 100%; align-items: center;">
                                <span class="prop-label">${escapeHtml(key)}</span>
                                <span style="font-size: 11px; color: ${scriptChanged ? 'var(--modified)' : 'var(--text-secondary)'}; font-weight: 600;">
                                    ${scriptChanged ? 'MODIFIED' : 'UNCHANGED'}
                                </span>
                            </div>
                            ${openButton}
                        </div>
                    `;
                } else if (hasValueDelta) {
                    // Regular property changed
                    if (valueAdded) {
                        rows += `
                            <div class="prop-row" style="flex-direction: column; gap: 4px; align-items: flex-start;">
                                <span class="prop-label">${escapeHtml(key)}</span>
                                <span class="prop-value" style="background: rgba(40, 200, 40, 0.2); padding: 6px; border-radius: 4px; width: 100%; white-space: pre-wrap; font-size: 11px;">${escapeHtml(newValue)}</span>
                            </div>
                        `;
                    } else if (valueRemoved) {
                        rows += `
                            <div class="prop-row" style="flex-direction: column; gap: 4px; align-items: flex-start;">
                                <span class="prop-label">${escapeHtml(key)}</span>
                                <span class="prop-value" style="background: rgba(200, 40, 40, 0.2); padding: 6px; border-radius: 4px; width: 100%; white-space: pre-wrap; font-size: 11px; text-decoration: line-through; opacity: 0.7;">${escapeHtml(oldValue)}</span>
                            </div>
                        `;
                    } else {
                        rows += `
                            <div class="prop-row" style="flex-direction: column; gap: 4px; align-items: flex-start;">
                                <span class="prop-label">${escapeHtml(key)}</span>
                                <div style="display: flex; align-items: center; gap: 8px; width: 100%;">
                                    <span class="prop-value" style="background: rgba(200, 40, 40, 0.2); padding: 6px; border-radius: 4px; flex: 1; font-size: 11px; text-decoration: line-through; opacity: 0.7;">${escapeHtml(oldValue)}</span>
                                    <svg class="ico" width="12" height="12" style="margin:0 4px; color:var(--text-secondary);"><use href="/vendor/phosphor/icons.svg#ph-arrow-right"></use></svg>
                                    <span class="prop-value" style="background: rgba(40, 200, 40, 0.2); padding: 6px; border-radius: 4px; flex: 1; font-size: 11px;">${escapeHtml(newValue)}</span>
                                </div>
                            </div>
                        `;
                    }
                } else {
                    if (showOnlyChangedValues) continue;
                    // Show single value (unchanged or new property)
                    rows += `
                        <div class="prop-row" style="flex-direction: column; gap: 4px; align-items: flex-start;">
                            <span class="prop-label">${escapeHtml(key)}</span>
                            <span class="prop-value" style="background: rgba(0,0,0,0.2); padding: 6px; border-radius: 4px; width: 100%; white-space: pre-wrap; font-size: 11px; max-height: 150px; overflow-y: auto;">${escapeHtml(newValue)}</span>
                        </div>
                    `;
                }
            }

            return `
                ${clusterSection}
                ${panelSection}
                ${rows ? `
                <div class="detail-card">
                    <div class="node-name" style="font-size: 12px; margin-bottom: 8px;">Component Values</div>
                    ${rows}
                </div>
                ` : ''}
            `;
        }

        function renderPorts(ports, title) {
            if (!ports || ports.length === 0) return '';

            const portItems = ports.map(p => {
                const statusColor = CONFIG.colors[p.status] || CONFIG.colors.same;
                const isInput = String(p.kind || p.Kind || '').toLowerCase() === 'input' || title.toLowerCase().startsWith('input');
                const rawValue = p.value ?? p.Value;
                const rawValueOld = p.valueOld ?? p.ValueOld;
                const rawValueNew = p.valueNew ?? p.ValueNew;
                const hasValue = rawValue !== null && rawValue !== undefined && String(rawValue).length > 0;
                const optionText = renderPortOptionText(p.options ?? p.Options);
                const optionDiffText = renderPortOptionDiffText(p.optionsOld ?? p.OptionsOld, p.optionsNew ?? p.OptionsNew);
                const portName = escapeHtml(p.nickname || p.name || p.id || '');
                const portId = escapeHtml(String(p.id || ''));
                const portStatus = escapeHtml(String(p.status || 'same'));
                const wireDisplayLine = isInput
                    ? `<div class="port-value">Wire: ${wireDisplayModeLabel(p.wireDisplay ?? p.WireDisplay)}</div>`
                    : '';
                const optionLine = optionDiffText
                    ? `<div class="port-value">Options: ${escapeHtml(optionDiffText)}</div>`
                    : (optionText ? `<div class="port-value">Options: ${escapeHtml(optionText)}</div>` : '');
                const valueLine = p.valueChanged
                    ? `<div class="port-value">Change: ${escapeHtml(String(rawValueOld ?? ''))} <svg class="ico" width="10" height="10" style="margin:0 2px;"><use href="/vendor/phosphor/icons.svg#ph-arrow-right"></use></svg> ${escapeHtml(String(rawValueNew ?? ''))}</div>`
                    : (hasValue ? `<div class="port-value">${escapeHtml(String(rawValue))}</div>` : '');

                return `
                    <div class="port-item">
                        <div class="port-meta">
                            <div class="port-name">${portName}</div>
                            ${valueLine || ''}
                            ${optionLine || ''}
                            ${wireDisplayLine}
                            <div class="port-value" style="opacity:0.7;">${portId}</div>
                        </div>
                        <span class="port-status" style="background:${statusColor}; color:white;">${portStatus}</span>
                    </div>
                `;
            }).join('');

            return `
                <div class="port-section">
                    <h4>${title}</h4>
                    <div class="port-list">
                        ${portItems}
                    </div>
                </div>
            `;
        }

        function updateStats() {
            const counts = { added: 0, removed: 0, modified: 0 };
            state.nodes.forEach(n => {
                if (counts[n.status] !== undefined) counts[n.status]++;
            });

            document.getElementById('stat-added').textContent = counts.added;
            document.getElementById('stat-removed').textContent = counts.removed;
            document.getElementById('stat-modified').textContent = counts.modified;
        }

        function escapeHtml(text) {
            if (!text) return '';
            return text
                .replace(/&/g, "&amp;")
                .replace(/</g, "&lt;")
                .replace(/>/g, "&gt;")
                .replace(/"/g, "&quot;")
                .replace(/'/g, "&#039;");
        }

        // Script Diff Logic
        function openScriptDiff(key, switchTab = true) {
            const selectedNode = state.nodes.find(n => n.id === state.selected);
            if (!selectedNode) return;

            const scriptEntries = getScriptPropertyEntries(selectedNode);
            if (scriptEntries.length === 0) {
                setScriptTabVisibility(false, 0);
                renderEmptyScriptPane('No script source found for this component.');
                return;
            }

            setScriptTabVisibility(true, scriptEntries.length);
            renderScriptPane(selectedNode, scriptEntries, key);

            if (switchTab) {
                setDetailsTab('script');
            }
        }

        function openScriptView(key) {
            openScriptDiff(key, true);
        }

        const SCRIPT_DIFF_RENDER_MODE = Object.freeze({
            SIDEBAR_CHANGED_ONLY: 'sidebar-changed-only',
            FULL_INLINE: 'full-inline',
            VIEW_ONLY: 'view-only'
        });

        // Simple line-based diff (Myers algorithm simplified or just line comparison)
        function computeDiff(oldText, newText, options = {}) {
            const {
                language = 'Other',
                mode = SCRIPT_DIFF_RENDER_MODE.FULL_INLINE
            } = options;

            const isViewOnly = mode === SCRIPT_DIFF_RENDER_MODE.VIEW_ONLY;
            const showChangedOnly = mode === SCRIPT_DIFF_RENDER_MODE.SIDEBAR_CHANGED_ONLY;
            const oldLines = String(oldText ?? '').split(/\r?\n/);
            const newLines = String(newText ?? '').split(/\r?\n/);

            if (isViewOnly) {
                return renderUnifiedDiff(newLines.map(l => ({ type: 'same', content: l })), 1, language, showChangedOnly);
            }

            // Simple LCS-based diff implementation
            const matrix = Array(oldLines.length + 1).fill(null).map(() => Array(newLines.length + 1).fill(0));

            for (let i = 1; i <= oldLines.length; i++) {
                for (let j = 1; j <= newLines.length; j++) {
                    if (oldLines[i - 1] === newLines[j - 1]) {
                        matrix[i][j] = matrix[i - 1][j - 1] + 1;
                    } else {
                        matrix[i][j] = Math.max(matrix[i - 1][j], matrix[i][j - 1]);
                    }
                }
            }

            // Backtrack to find diff
            let i = oldLines.length;
            let j = newLines.length;
            const changes = [];

            while (i > 0 || j > 0) {
                if (i > 0 && j > 0 && oldLines[i - 1] === newLines[j - 1]) {
                    changes.unshift({ type: 'same', content: oldLines[i - 1] });
                    i--;
                    j--;
                } else if (i > 0 && j > 0) {
                    // Prefer deletions when LCS scores tie so a replacement reads as removed line then added line.
                    if (matrix[i - 1][j] >= matrix[i][j - 1]) {
                        changes.unshift({ type: 'removed', content: oldLines[i - 1] });
                        i--;
                    } else {
                        changes.unshift({ type: 'added', content: newLines[j - 1] });
                        j--;
                    }
                } else if (j > 0) {
                    changes.unshift({ type: 'added', content: newLines[j - 1] });
                    j--;
                } else {
                    changes.unshift({ type: 'removed', content: oldLines[i - 1] });
                    i--;
                }
            }

            return renderUnifiedDiff(changes, 1, language, showChangedOnly);
        }

        function renderUnifiedDiff(changes, startLine = 1, language = 'Other', showChangedOnly = false) {
            let html = '<div class="diff-table">';
            let oldLineNum = startLine;
            let newLineNum = startLine;
            let renderedCount = 0;

            changes.forEach(change => {
                let oldNumStr = '';
                let newNumStr = '';
                let className = '';
                let symbol = ' ';

                if (change.type === 'same') {
                    oldNumStr = oldLineNum++;
                    newNumStr = newLineNum++;
                    className = '';
                    symbol = ' ';
                } else if (change.type === 'added') {
                    newNumStr = newLineNum++;
                    // Bug fix #3: Use 'diff-line added' to match CSS (was 'diff-added')
                    className = 'diff-line added';
                    symbol = '+';
                } else if (change.type === 'removed') {
                    oldNumStr = oldLineNum++;
                    // Bug fix #3: Use 'diff-line removed' to match CSS (was 'diff-removed')
                    className = 'diff-line removed';
                    symbol = '-';
                }

                if (showChangedOnly && change.type === 'same') {
                    return;
                }

                const highlightedLine = highlightCodeLine(change.content, language);
                renderedCount += 1;
                html += `
                    <div class="diff-line ${className}">
                        <div class="diff-num">${oldNumStr}</div>
                        <div class="diff-num">${newNumStr}</div>
                        <div class="diff-code">
                            <span class="diff-code-sign">${escapeHtml(symbol + ' ')}</span>
                            <span class="diff-code-content">${highlightedLine}</span>
                        </div>
                    </div>
                `;
            });

            if (renderedCount === 0) {
                html += `
                    <div class="diff-line">
                        <div class="diff-num"></div>
                        <div class="diff-num"></div>
                        <div class="diff-code">
                            <span class="diff-code-content">No changed lines.</span>
                        </div>
                    </div>
                `;
            }

            html += '</div>';
            return html;
        }

        function shouldBlockInspectShortcut(event) {
            const key = String(event?.key || '').toLowerCase();
            return key === 'f12' ||
                (event.ctrlKey && event.shiftKey && (key === 'i' || key === 'j' || key === 'c')) ||
                (event.ctrlKey && key === 'u');
        }

        function suppressInspectAccess(event) {
            event.preventDefault();
            event.stopPropagation();
        }

        document.addEventListener('contextmenu', suppressInspectAccess);
        document.addEventListener('keydown', event => {
            if (shouldBlockInspectShortcut(event)) {
                suppressInspectAccess(event);
            }
        }, true);

        // Start
        init();

