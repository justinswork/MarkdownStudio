(function () {
  'use strict';

  function postToHost(msg) {
    try {
      window.chrome.webview.postMessage(msg);
    } catch (e) {
      console.error('postMessage failed', e);
    }
  }

  function getThemeFromQuery() {
    var params = new URLSearchParams(window.location.search);
    return params.get('theme') || 'theme-daylight';
  }

  // Apply preview typography prefs to the root CSS vars and a heading-style
  // class on body. Called once on load from URL query params, then by the
  // host whenever the user changes a setting.
  var KNOWN_HEADING_CLASSES = ['headings-standard', 'headings-minimal', 'headings-display'];
  function applyPreviewOptions(opts) {
    var root = document.documentElement.style;
    if (opts.fontFamily) root.setProperty('--mds-preview-font-family', opts.fontFamily);
    if (opts.fontSize)   root.setProperty('--mds-preview-font-size',   opts.fontSize + 'px');
    if (opts.lineHeight) root.setProperty('--mds-preview-line-height', String(opts.lineHeight));
    if (opts.width)      root.setProperty('--mds-preview-width',       opts.width);
    if (opts.headingClass) {
      KNOWN_HEADING_CLASSES.forEach(function (c) { document.body.classList.remove(c); });
      document.body.classList.add(opts.headingClass);
    }
  }
  // -------- Keyboard chord matching for rebindable shortcuts --------
  //
  // Chord strings are the same format the C# side persists, e.g.
  // "Ctrl+Shift+E". We normalize KeyboardEvent.key into the same alphabet
  // (so "ArrowLeft" matches "Left", " " matches "Space", "Escape" matches
  // "Esc", and letter case is irrelevant). Declared before the seed IIFE
  // below because that seed pushes into `shortcuts`.
  var shortcuts = { start: 'Ctrl+E', apply: 'Ctrl+Enter', cancel: 'Esc' };
  function applyShortcuts(s) {
    if (!s) return;
    if (s.start)  shortcuts.start  = s.start;
    if (s.apply)  shortcuts.apply  = s.apply;
    if (s.cancel) shortcuts.cancel = s.cancel;
    // The context menu's kbd hint is built lazily from this object — drop
    // any cached menu so the next show rebuilds with the new label.
    if (typeof xrayMenu !== 'undefined' && xrayMenu && xrayMenu.parentNode) {
      xrayMenu.parentNode.removeChild(xrayMenu);
    }
    if (typeof xrayMenu !== 'undefined') xrayMenu = null;
  }

  // Initial seed from URL query params (so new tabs render with the saved
  // settings before the host has a chance to push them).
  (function seedFromQuery() {
    var p = new URLSearchParams(window.location.search);
    applyPreviewOptions({
      fontFamily:   p.get('pfFamily') || undefined,
      fontSize:     parseInt(p.get('pfSize'), 10) || undefined,
      lineHeight:   parseFloat(p.get('pfLh'))     || undefined,
      width:        p.get('pfWidth')  || undefined,
      headingClass: p.get('pfHead')   || 'headings-standard',
    });
    applyShortcuts({
      start:  p.get('xrayStart')  || 'Ctrl+E',
      apply:  p.get('xrayApply')  || 'Ctrl+Enter',
      cancel: p.get('xrayCancel') || 'Esc',
    });
  })();
  function normalizeKey(name) {
    if (name == null) return '';
    name = String(name).toLowerCase();
    if (name === 'escape')     return 'esc';
    if (name === ' ')          return 'space';
    if (name === 'arrowleft')  return 'left';
    if (name === 'arrowright') return 'right';
    if (name === 'arrowup')    return 'up';
    if (name === 'arrowdown')  return 'down';
    return name;
  }
  function parseChord(str) {
    if (!str) return null;
    var parts = String(str).split('+').map(function (p) { return p.trim(); });
    if (!parts.length) return null;
    var keyName = parts[parts.length - 1];
    var mods    = parts.slice(0, -1).map(function (m) { return m.toLowerCase(); });
    return {
      key:   normalizeKey(keyName),
      ctrl:  mods.indexOf('ctrl')  !== -1 || mods.indexOf('control') !== -1,
      shift: mods.indexOf('shift') !== -1,
      alt:   mods.indexOf('alt')   !== -1 || mods.indexOf('menu')    !== -1,
    };
  }
  function chordMatches(e, chordStr) {
    var c = parseChord(chordStr);
    if (!c || !c.key) return false;
    if (normalizeKey(e.key) !== c.key) return false;
    if (!!e.ctrlKey  !== c.ctrl)  return false;
    if (!!e.shiftKey !== c.shift) return false;
    if (!!e.altKey   !== c.alt)   return false;
    return true;
  }

  var KNOWN_THEMES = ['theme-daylight', 'theme-midnight', 'theme-sepia',
                      'theme-solarized-light', 'theme-solarized-dark'];

  var md = window.markdownit({
    // html: true lets HTML comments be stripped (instead of escaped + rendered
    // as text), and allows inline HTML like <a name="anchor"></a> in
    // documentation files to work as intended.
    html: true,
    linkify: true,
    typographer: true,
    breaks: false,
    highlight: function (str, lang) {
      if (lang === 'mermaid') {
        return '<pre class="mermaid">' + escapeHtml(str) + '</pre>';
      }
      if (lang && window.hljs && window.hljs.getLanguage(lang)) {
        try {
          return '<pre class="hljs"><code>' +
            window.hljs.highlight(str, { language: lang, ignoreIllegals: true }).value +
            '</code></pre>';
        } catch (_) {}
      }
      return '<pre class="hljs"><code>' + escapeHtml(str) + '</code></pre>';
    },
  });

  if (window.markdownitTaskLists) md.use(window.markdownitTaskLists, { enabled: true, label: true });
  if (window.markdownitFootnote) md.use(window.markdownitFootnote);
  if (window.markdownitKatex) {
    try { md.use(window.markdownitKatex); } catch (e) { console.warn('katex plugin failed', e); }
  }
  // markdown-it-emoji ships as { full, light, bare } in v3; the bare bundle
  // exposes a global `markdownitEmoji` factory we can apply directly.
  var emojiPlugin = window.markdownitEmoji
    && (window.markdownitEmoji.full || window.markdownitEmoji.light || window.markdownitEmoji);
  if (emojiPlugin) {
    try { md.use(emojiPlugin); } catch (e) { console.warn('emoji plugin failed', e); }
  }

  // Stamp top-level block tokens with their source-line range so the host
  // can map preview scroll position back to the editor and so x-ray edit
  // knows which raw-markdown lines to swap in. data-line is 0-based
  // inclusive, data-line-end is 0-based exclusive — matches markdown-it's
  // own token.map convention.
  function injectLineNumbers(tokens, idx, options, env, slf) {
    var tok = tokens[idx];
    if (tok.map && tok.level === 0) {
      tok.attrSet('data-line',     String(tok.map[0]));
      tok.attrSet('data-line-end', String(tok.map[1]));
    }
    return slf.renderToken(tokens, idx, options, env, slf);
  }
  ['paragraph_open', 'heading_open', 'bullet_list_open', 'ordered_list_open',
   'blockquote_open', 'table_open', 'hr', 'dl_open'].forEach(function (rule) {
    md.renderer.rules[rule] = injectLineNumbers;
  });
  // fence has a custom renderer (highlight wraps it). Override entirely so we
  // can attach data-line.
  md.renderer.rules.fence = function (tokens, idx, options, env, slf) {
    var token = tokens[idx];
    var info = token.info ? token.info.trim() : '';
    var lang = info.split(/\s+/g)[0] || '';
    var startLine = token.map ? token.map[0] : 0;
    var endLine   = token.map ? token.map[1] : startLine + 1;
    var attrs = ' data-line="' + startLine + '" data-line-end="' + endLine + '"';
    if (lang === 'mermaid') {
      return '<pre class="mermaid"' + attrs + '>' + escapeHtml(token.content) + '</pre>\n';
    }
    if (lang && window.hljs && window.hljs.getLanguage(lang)) {
      try {
        return '<pre class="hljs"' + attrs + '><code>' +
          window.hljs.highlight(token.content, { language: lang, ignoreIllegals: true }).value +
          '</code></pre>\n';
      } catch (_) {}
    }
    return '<pre class="hljs"' + attrs + '><code>' + escapeHtml(token.content) + '</code></pre>\n';
  };

  function escapeHtml(s) {
    return s.replace(/[&<>"']/g, function (c) {
      return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c];
    });
  }

  function setTheme(name) {
    var cls = KNOWN_THEMES.indexOf(name) >= 0 ? name : 'theme-daylight';
    KNOWN_THEMES.forEach(function (t) { document.body.classList.remove(t); });
    document.body.classList.add(cls);
    var isDark = (cls === 'theme-midnight' || cls === 'theme-solarized-dark');
    if (window.mermaid && window.mermaid.initialize) {
      window.mermaid.initialize({ startOnLoad: false, theme: isDark ? 'dark' : 'default', securityLevel: 'strict' });
    }
  }

  setTheme(getThemeFromQuery());

  // Scroll-sync state. lineMap is [{ line: 1-based, top: px }] sorted by line.
  var lineMap = [];
  var lastSyncIn = 0;
  var SYNC_LOCK_MS = 220;
  var scrollDebounce = null;

  function rebuildLineMap() {
    lineMap = [];
    var nodes = document.querySelectorAll('#content [data-line]');
    for (var i = 0; i < nodes.length; i++) {
      var n = nodes[i];
      var raw = parseInt(n.getAttribute('data-line'), 10);
      if (isNaN(raw)) continue;
      lineMap.push({ line: raw + 1, top: n.getBoundingClientRect().top + window.scrollY });
    }
    lineMap.sort(function (a, b) { return a.top - b.top; });
  }

  function scrollerHeight() {
    return Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
  }

  function scrollToSourceLine(line) {
    if (!lineMap.length) { window.scrollTo(0, 0); return; }
    var first = lineMap[0];
    if (line <= first.line) { window.scrollTo(0, 0); return; }
    var last = lineMap[lineMap.length - 1];
    if (line >= last.line) { window.scrollTo(0, scrollerHeight()); return; }
    for (var i = 0; i < lineMap.length - 1; i++) {
      var a = lineMap[i], b = lineMap[i + 1];
      if (line >= a.line && line <= b.line) {
        var span = b.line - a.line;
        var ratio = span > 0 ? (line - a.line) / span : 0;
        window.scrollTo(0, a.top + (b.top - a.top) * ratio);
        return;
      }
    }
  }

  function currentSourceLine() {
    if (!lineMap.length) return 1;
    var st = window.scrollY;
    if (st <= lineMap[0].top) return lineMap[0].line;
    for (var i = 0; i < lineMap.length - 1; i++) {
      var a = lineMap[i], b = lineMap[i + 1];
      if (st >= a.top && st < b.top) {
        var span = b.top - a.top;
        var ratio = span > 0 ? (st - a.top) / span : 0;
        return Math.round(a.line + (b.line - a.line) * ratio);
      }
    }
    return lineMap[lineMap.length - 1].line;
  }

  // -------- X-ray edit state (declared before render() so its guard works) --------
  var xrayActive = false;
  var xrayState  = null; // { wrap, hiddenElement, startLine, endLine }

  // -------- GitHub-style blockquote alerts --------
  // Recognises blockquotes whose first paragraph starts with [!NOTE], [!TIP],
  // [!IMPORTANT], [!WARNING], or [!CAUTION] and restyles them with a title
  // and accent colour, matching how github.com renders the same syntax.
  var GH_ALERT_TYPES = {
    'NOTE':      { label: 'Note',      cls: 'mds-alert-note' },
    'TIP':       { label: 'Tip',       cls: 'mds-alert-tip' },
    'IMPORTANT': { label: 'Important', cls: 'mds-alert-important' },
    'WARNING':   { label: 'Warning',   cls: 'mds-alert-warning' },
    'CAUTION':   { label: 'Caution',   cls: 'mds-alert-caution' },
  };
  function applyGitHubAlerts(article) {
    var quotes = article.querySelectorAll('blockquote');
    for (var i = 0; i < quotes.length; i++) {
      var q = quotes[i];
      if (q.classList.contains('mds-alert')) continue;
      var firstP = q.querySelector(':scope > p');
      if (!firstP) continue;
      // The first thing inside the <p> should be a literal [!TYPE] marker.
      var m = firstP.innerHTML.match(/^\s*\[!([A-Z]+)\]\s*(?:<br\s*\/?>\s*)?/);
      if (!m) continue;
      var info = GH_ALERT_TYPES[m[1]];
      if (!info) continue;
      q.classList.add('mds-alert', info.cls);
      firstP.innerHTML = firstP.innerHTML.substring(m[0].length);
      var title = document.createElement('div');
      title.className = 'mds-alert-title';
      title.textContent = info.label;
      q.insertBefore(title, q.firstChild);
      if (!firstP.innerHTML.trim()) firstP.remove();
    }
  }

  // Latest markdown source for x-ray editing. Kept in JS so x-ray can pull
  // raw lines without a round-trip to the host.
  var currentSource = '';

  var renderTimer = null;
  function render(text) {
    currentSource = text || '';
    if (renderTimer) clearTimeout(renderTimer);
    renderTimer = setTimeout(function () {
      // If an x-ray editor is open, the user is mid-edit. Don't blow it away
      // with a host-side re-render of stale content — the save flow handles
      // closing it after its own edit lands.
      if (xrayActive) return;

      var html = md.render(currentSource);
      var article = document.getElementById('content');
      article.innerHTML = html;

      applyGitHubAlerts(article);

      if (window.mermaid && window.mermaid.run) {
        try { window.mermaid.run({ nodes: article.querySelectorAll('pre.mermaid') }); }
        catch (e) { console.warn('mermaid render failed', e); }
      }

      requestAnimationFrame(rebuildLineMap);
    }, 30);
  }

  window.addEventListener('resize', function () {
    if (scrollDebounce) clearTimeout(scrollDebounce);
    scrollDebounce = setTimeout(rebuildLineMap, 80);
  });

  window.addEventListener('scroll', function () {
    if (Date.now() - lastSyncIn < SYNC_LOCK_MS) return;
    if (scrollDebounce) clearTimeout(scrollDebounce);
    scrollDebounce = setTimeout(function () {
      postToHost({ type: 'scrolled', line: currentSourceLine() });
    }, 20);
  }, { passive: true });

  document.addEventListener('click', function (e) {
    var t = e.target;
    while (t && t.tagName !== 'A') t = t.parentNode;
    if (!t || !t.getAttribute) return;
    var href = t.getAttribute('href');
    if (!href) return;
    if (/^https?:\/\//i.test(href)) {
      e.preventDefault();
      postToHost({ type: 'linkClicked', url: href });
    }
  }, true);

  // Chrome-style URL hover hint in the bottom-left corner.
  var hoverIndicator = document.getElementById('link-hover-indicator');
  function findAnchor(el) {
    while (el && el.nodeType === 1) {
      if (el.tagName === 'A' && el.getAttribute('href')) return el;
      el = el.parentElement;
    }
    return null;
  }
  document.addEventListener('mouseover', function (e) {
    var a = findAnchor(e.target);
    if (!a || !hoverIndicator) return;
    var href = a.getAttribute('href') || '';
    if (!href || href.charAt(0) === '#') {
      hoverIndicator.classList.remove('visible');
      return;
    }
    hoverIndicator.textContent = href;
    hoverIndicator.classList.add('visible');
  }, true);
  document.addEventListener('mouseout', function (e) {
    var a = findAnchor(e.target);
    if (!a || !hoverIndicator) return;
    var to = findAnchor(e.relatedTarget);
    if (to && to.getAttribute('href') === a.getAttribute('href')) return;
    hoverIndicator.classList.remove('visible');
  }, true);

  // -------- X-ray: right-click a block → edit raw markdown in place --------
  // The preview owns the source text (see currentSource), so the textarea can
  // be populated locally. On save, post xrayApply to the host and let it edit
  // Monaco; Monaco's change event will re-render the preview normally.
  var xrayMenu = null;
  var xrayContextBlocks = null;       // array of block elements the menu acts on
  var xrayContextClick  = null;       // { x, y } of the originating right-click, or null

  function findBlock(el) {
    while (el && el.nodeType === 1 && el !== document.body) {
      if (el.hasAttribute && el.hasAttribute('data-line') && el.hasAttribute('data-line-end')) return el;
      el = el.parentElement;
    }
    return null;
  }

  // Find every [data-line] block that the current text selection intersects.
  // Returns null when the selection is empty / collapsed / outside #content.
  function findBlocksInSelection() {
    var sel = window.getSelection();
    if (!sel || sel.rangeCount === 0 || sel.isCollapsed) return null;
    var range   = sel.getRangeAt(0);
    var article = document.getElementById('content');
    if (!article || !range.intersectsNode(article)) return null;
    var hits = [];
    var all  = article.querySelectorAll('[data-line][data-line-end]');
    for (var i = 0; i < all.length; i++) {
      if (range.intersectsNode(all[i])) hits.push(all[i]);
    }
    if (!hits.length) return null;
    // Keep only innermost blocks: drop anything that contains another hit.
    var leaves = [];
    for (var i = 0; i < hits.length; i++) {
      var el = hits[i], hasInnerHit = false;
      for (var j = 0; j < hits.length; j++) {
        if (i === j) continue;
        if (el.contains(hits[j])) { hasInnerHit = true; break; }
      }
      if (!hasInnerHit) leaves.push(el);
    }
    // Sort top-down by source line, just in case.
    leaves.sort(function (a, b) {
      return (parseInt(a.getAttribute('data-line'), 10) || 0) -
             (parseInt(b.getAttribute('data-line'), 10) || 0);
    });
    return leaves;
  }

  // Map a viewport (x, y) under one of the selected blocks back to a character
  // offset within the combined raw markdown source for those blocks.
  //
  // Approach: find the word under the click in the rendered DOM, count how
  // many times that same word appears in the rendered text BEFORE the clicked
  // instance, then locate the Nth occurrence of the word in the raw source
  // slice. Cursor lands at match-start + offset-within-word. Markdown syntax
  // around the word (e.g. **, [text](url)) is naturally skipped because we
  // match on visible words only.
  function clickToSourceOffset(blocks, clickX, clickY, rawText) {
    if (!document.caretRangeFromPoint) return null;
    var range = document.caretRangeFromPoint(clickX, clickY);
    if (!range) return null;
    var node = range.startContainer;
    if (!node || node.nodeType !== 3) return null;

    var inOurBlocks = false;
    for (var i = 0; i < blocks.length; i++) {
      if (blocks[i].contains(node)) { inOurBlocks = true; break; }
    }
    if (!inOurBlocks) return null;

    var text = node.textContent;
    var off  = range.startOffset;

    var wordRe = /[\p{L}\p{N}_'\-]/u;
    var wStart = off;
    while (wStart > 0           && wordRe.test(text.charAt(wStart - 1))) wStart--;
    var wEnd   = off;
    while (wEnd   < text.length && wordRe.test(text.charAt(wEnd)))       wEnd++;

    if (wStart === wEnd) return null; // click landed in whitespace / punctuation
    var word         = text.substring(wStart, wEnd);
    var offsetInWord = off - wStart;

    var escaped = word.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    // Whole-word: not flanked by another word character. Use lookarounds rather
    // than \b so we agree with our own word-char definition (Unicode + hyphen).
    var bound = '(?<![\\p{L}\\p{N}_])' + escaped + '(?![\\p{L}\\p{N}_])';

    // Count occurrences of the word in the rendered text of the blocks BEFORE
    // the clicked word's start.
    var prefixRe   = new RegExp(bound, 'gu');
    var before     = 0;
    var foundClick = false;
    for (var b = 0; b < blocks.length && !foundClick; b++) {
      var walker = document.createTreeWalker(blocks[b], NodeFilter.SHOW_TEXT, null);
      var n;
      while ((n = walker.nextNode())) {
        if (n === node) {
          var prefix = n.textContent.substring(0, wStart);
          var pm = prefix.match(prefixRe);
          before += pm ? pm.length : 0;
          foundClick = true;
          break;
        }
        var pm2 = n.textContent.match(prefixRe);
        before += pm2 ? pm2.length : 0;
      }
    }

    // Find every occurrence of the word in the raw source slice.
    var srcRe = new RegExp(bound, 'gu');
    var matches = [];
    var m;
    while ((m = srcRe.exec(rawText)) !== null) {
      matches.push(m.index);
      if (m.index === srcRe.lastIndex) srcRe.lastIndex++; // safety against zero-width
    }
    if (before >= matches.length) return null;

    return matches[before] + offsetInWord;
  }

  // Compute the source line range covering one or more blocks.
  function rangeForBlocks(blocks) {
    var minStart = Infinity, maxEnd = -Infinity;
    for (var i = 0; i < blocks.length; i++) {
      var s = parseInt(blocks[i].getAttribute('data-line'),     10);
      var e = parseInt(blocks[i].getAttribute('data-line-end'), 10);
      if (!isNaN(s) && s < minStart) minStart = s;
      if (!isNaN(e) && e > maxEnd)   maxEnd   = e;
    }
    if (minStart === Infinity || maxEnd <= minStart) return null;
    return { start: minStart, end: maxEnd };
  }

  function ensureXrayMenu() {
    if (xrayMenu) return xrayMenu;
    xrayMenu = document.createElement('div');
    xrayMenu.className = 'mds-context-menu';
    xrayMenu.innerHTML =
      '<button class="mds-cm-item" data-action="xray" type="button">' +
        '<span class="mds-cm-label">X-ray edit</span>' +
        '<kbd class="mds-cm-kbd">' + escapeHtml(shortcuts.start) + '</kbd>' +
      '</button>' +
      '<div class="mds-cm-separator"></div>' +
      '<button class="mds-cm-item" data-action="copy" type="button">' +
        '<span class="mds-cm-label">Copy</span>' +
        '<kbd class="mds-cm-kbd">Ctrl+C</kbd>' +
      '</button>' +
      '<button class="mds-cm-item" data-action="selectAll" type="button">' +
        '<span class="mds-cm-label">Select all</span>' +
        '<kbd class="mds-cm-kbd">Ctrl+A</kbd>' +
      '</button>';
    document.body.appendChild(xrayMenu);
    // Swallow mousedown / pointerdown so the outside-click dismissal doesn't
    // fire when the user is clicking the menu itself (the document handler
    // below also checks contains(), but defense-in-depth).
    ['mousedown', 'pointerdown'].forEach(function (ev) {
      xrayMenu.addEventListener(ev, function (e) { e.stopPropagation(); });
    });
    xrayMenu.addEventListener('click', function (e) {
      var btn = e.target.closest('[data-action]');
      if (!btn || btn.classList.contains('disabled')) return;
      // Capture state BEFORE hiding (hideXrayMenu clears it).
      var blocks = xrayContextBlocks;
      var click  = xrayContextClick;
      var action = btn.getAttribute('data-action');
      hideXrayMenu();
      if (action === 'xray' && blocks && blocks.length) {
        startXrayEdit(blocks, click);
      } else if (action === 'copy') {
        copySelectionToClipboard();
      } else if (action === 'selectAll') {
        selectAllPreview();
      }
    });
    return xrayMenu;
  }

  function copySelectionToClipboard() {
    var sel = window.getSelection();
    var text = sel ? sel.toString() : '';
    if (!text) return;
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).catch(function () {
          try { document.execCommand('copy'); } catch (_) {}
        });
      } else {
        document.execCommand('copy');
      }
    } catch (_) {}
  }

  function selectAllPreview() {
    var article = document.getElementById('content');
    if (!article) return;
    var range = document.createRange();
    range.selectNodeContents(article);
    var sel = window.getSelection();
    if (!sel) return;
    sel.removeAllRanges();
    sel.addRange(range);
  }

  function showXrayMenu(x, y, blocks) {
    xrayContextBlocks = blocks;
    xrayContextClick  = { x: x, y: y };
    var menu = ensureXrayMenu();
    // Reflect a count for selections >1 in the x-ray label. Disable X-ray
    // entirely when there are no blocks under the cursor.
    var xrayItem = menu.querySelector('[data-action="xray"]');
    var xrayLbl  = xrayItem ? xrayItem.querySelector('.mds-cm-label') : null;
    if (xrayItem) xrayItem.classList.toggle('disabled', !blocks || !blocks.length);
    if (xrayLbl) xrayLbl.textContent = (blocks && blocks.length > 1)
      ? 'X-ray edit (' + blocks.length + ' blocks)'
      : 'X-ray edit';
    // Copy only makes sense with a non-empty selection.
    var copyBtn = menu.querySelector('[data-action="copy"]');
    if (copyBtn) {
      var hasSel = !!(window.getSelection() && window.getSelection().toString().length);
      copyBtn.classList.toggle('disabled', !hasSel);
    }
    menu.style.left = '0px';
    menu.style.top  = '0px';
    menu.classList.add('visible');
    var rect = menu.getBoundingClientRect();
    var maxX = window.innerWidth  - rect.width  - 4;
    var maxY = window.innerHeight - rect.height - 4;
    menu.style.left = Math.max(0, Math.min(x, maxX)) + 'px';
    menu.style.top  = Math.max(0, Math.min(y, maxY)) + 'px';
  }

  function hideXrayMenu() {
    if (xrayMenu) xrayMenu.classList.remove('visible');
    xrayContextBlocks = null;
    xrayContextClick  = null;
  }

  document.addEventListener('contextmenu', function (e) {
    // Prefer a multi-block text selection; fall back to the right-clicked block.
    var blocks = findBlocksInSelection();
    if (!blocks || !blocks.length) {
      var block = findBlock(e.target);
      if (block) blocks = [block];
    }
    // Even when there's no block under the cursor (e.g. margin / blank
    // bottom space) we still show the menu so Copy / Select all are
    // reachable; X-ray will be disabled in that state.
    e.preventDefault();
    showXrayMenu(e.clientX, e.clientY, blocks || []);
  });
  document.addEventListener('mousedown', function (e) {
    if (!xrayMenu || !xrayMenu.classList.contains('visible')) return;
    // Don't dismiss when the click is inside the menu — the click handler
    // needs to fire first.
    if (xrayMenu.contains(e.target)) return;
    hideXrayMenu();
  }, true);
  window.addEventListener('blur', hideXrayMenu);
  window.addEventListener('scroll', hideXrayMenu, true);
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') hideXrayMenu();
    // Start-x-ray shortcut (default Ctrl+E) acts on the selection if any,
    // otherwise the hovered block.
    if (chordMatches(e, shortcuts.start)) {
      var blocks = findBlocksInSelection();
      if (!blocks || !blocks.length) {
        var hovered = document.querySelector('[data-line]:hover');
        var block = findBlock(hovered || document.activeElement);
        if (block) blocks = [block];
      }
      if (blocks && blocks.length) { e.preventDefault(); startXrayEdit(blocks); }
    }
  });

  function startXrayEdit(blocks, clickPoint) {
    if (xrayActive) return;
    if (!Array.isArray(blocks)) blocks = [blocks];
    if (!blocks.length) return;
    var r = rangeForBlocks(blocks);
    if (!r) return;

    var lines     = currentSource.split('\n');
    var safeStart = Math.max(0, Math.min(r.start, lines.length));
    var safeEnd   = Math.max(safeStart + 1, Math.min(r.end, lines.length));
    var raw       = lines.slice(safeStart, safeEnd).join('\n');

    var initialCaret = null;
    if (clickPoint) {
      try {
        initialCaret = clickToSourceOffset(blocks, clickPoint.x, clickPoint.y, raw);
      } catch (_) { initialCaret = null; }
    }

    openXrayEditor(blocks, safeStart, safeEnd, raw, initialCaret);
  }

  function openXrayEditor(blocks, startLine, endLine, rawText, initialCaret) {
    var anchor   = blocks[0];
    var blockCnt = blocks.length;
    var rangeLbl = blockCnt > 1
      ? 'X-ray editing source lines ' + (startLine + 1) + '–' + endLine + ' (' + blockCnt + ' blocks)'
      : 'X-ray editing source lines ' + (startLine + 1) + '–' + endLine;

    var wrap = document.createElement('div');
    wrap.className = 'mds-xray';
    wrap.innerHTML =
      '<div class="mds-xray-toolbar">' +
        '<span class="mds-xray-label">' + rangeLbl + '</span>' +
        '<button class="mds-xray-apply"  type="button">Apply</button>' +
        '<button class="mds-xray-cancel" type="button">Cancel</button>' +
        '<span class="mds-xray-hint">' + escapeHtml(shortcuts.apply) + ' apply · ' +
                                          escapeHtml(shortcuts.cancel) + ' cancel</span>' +
      '</div>' +
      '<textarea class="mds-xray-text" spellcheck="false" wrap="soft"></textarea>';

    anchor.insertAdjacentElement('beforebegin', wrap);
    for (var i = 0; i < blocks.length; i++) blocks[i].style.display = 'none';

    var textarea = wrap.querySelector('.mds-xray-text');
    textarea.value = rawText;

    function autoResize() {
      textarea.style.height = 'auto';
      textarea.style.height = Math.max(40, textarea.scrollHeight + 2) + 'px';
    }
    autoResize();
    textarea.addEventListener('input', autoResize);
    textarea.addEventListener('keydown', function (e) {
      if (chordMatches(e, shortcuts.cancel)) { e.preventDefault(); cancelXrayEditor(); }
      else if (chordMatches(e, shortcuts.apply)) { e.preventDefault(); applyXrayEditor(); }
    });

    wrap.querySelector('.mds-xray-apply' ).addEventListener('click', applyXrayEditor);
    wrap.querySelector('.mds-xray-cancel').addEventListener('click', cancelXrayEditor);

    xrayState = {
      wrap: wrap,
      hiddenElements: blocks.slice(),
      startLine: startLine,
      endLine:   endLine,
    };
    xrayActive = true;

    // Clear any text selection so the textarea doesn't open with it.
    try { window.getSelection().removeAllRanges(); } catch (_) {}

    var caret = (typeof initialCaret === 'number' && initialCaret >= 0)
      ? Math.min(initialCaret, rawText.length)
      : 0;
    setTimeout(function () {
      textarea.focus();
      textarea.setSelectionRange(caret, caret);
    }, 0);
  }

  function applyXrayEditor() {
    if (!xrayActive || !xrayState) return;
    var text = xrayState.wrap.querySelector('.mds-xray-text').value;
    // Convert the markdown-it style range (0-based inclusive start, 0-based
    // exclusive end) to Monaco's 1-based inclusive convention.
    var monacoStart = xrayState.startLine + 1;
    var monacoEnd   = xrayState.endLine;
    // The message type stays "xrayApply" — that's the wire-level contract
    // with the host; the renamed JS function and UI labels are local.
    postToHost({
      type: 'xrayApply',
      startLine: monacoStart,
      endLine:   monacoEnd,
      text:      text,
    });
    // Mark inactive so the upcoming render() (triggered by Monaco's change
    // event) can rebuild the DOM and our wrap disappears cleanly. The buffer
    // is updated in-memory only; the user still has to Ctrl+S to save to disk.
    xrayActive = false;
    if (xrayState.wrap) xrayState.wrap.classList.add('mds-xray-applying');
    xrayState = null;
  }

  function cancelXrayEditor() {
    if (!xrayActive || !xrayState) return;
    var st = xrayState;
    xrayState = null;
    xrayActive = false;
    if (st.wrap && st.wrap.parentNode) st.wrap.parentNode.removeChild(st.wrap);
    if (st.hiddenElements) {
      for (var i = 0; i < st.hiddenElements.length; i++) st.hiddenElements[i].style.display = '';
    }
  }

  window.host = {
    render: render,
    setTheme: setTheme,
    setPreviewOptions: applyPreviewOptions,
    setShortcuts: applyShortcuts,
    scrollToLine: function (line) {
      lastSyncIn = Date.now();
      scrollToSourceLine(line);
    },
  };

  // Forward host shortcuts that WebView2's underlying Edge runtime would
  // otherwise consume before they reach the WinUI accelerator. F11 is
  // Edge's fullscreen accelerator and Ctrl+S is its "Save Page" — without
  // intercepting them here, neither toggle-focus nor save fires when the
  // preview pane has focus. We don't touch Ctrl+E / Esc / Ctrl+Enter:
  // those go through chordMatches() and the x-ray flow.
  window.addEventListener('keydown', function (e) {
    if (e.key === 'F11') {
      e.preventDefault();
      e.stopPropagation();
      postToHost({ type: 'toggleFocus' });
      return;
    }
    var key = (e.key || '').toLowerCase();
    if (key === 's' && (e.ctrlKey || e.metaKey) && !e.altKey) {
      e.preventDefault();
      e.stopPropagation();
      postToHost({ type: e.shiftKey ? 'saveAs' : 'save' });
    }
  }, true);

  postToHost({ type: 'ready' });
})();
