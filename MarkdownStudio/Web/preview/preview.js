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

  var KNOWN_THEMES = ['theme-daylight', 'theme-midnight', 'theme-sepia',
                      'theme-solarized-light', 'theme-solarized-dark'];

  var md = window.markdownit({
    html: false,
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
  var xrayContextTarget = null;

  function findBlock(el) {
    while (el && el.nodeType === 1 && el !== document.body) {
      if (el.hasAttribute && el.hasAttribute('data-line') && el.hasAttribute('data-line-end')) return el;
      el = el.parentElement;
    }
    return null;
  }

  function ensureXrayMenu() {
    if (xrayMenu) return xrayMenu;
    xrayMenu = document.createElement('div');
    xrayMenu.className = 'mds-context-menu';
    xrayMenu.innerHTML =
      '<button class="mds-cm-item" data-action="xray" type="button">' +
        '<span class="mds-cm-icon">⌧</span>' +
        '<span class="mds-cm-label">X-ray edit</span>' +
        '<kbd class="mds-cm-kbd">Ctrl+E</kbd>' +
      '</button>';
    document.body.appendChild(xrayMenu);
    xrayMenu.addEventListener('mousedown', function (e) { e.stopPropagation(); });
    xrayMenu.addEventListener('click', function (e) {
      var btn = e.target.closest('[data-action]');
      if (!btn) return;
      hideXrayMenu();
      if (btn.getAttribute('data-action') === 'xray' && xrayContextTarget) {
        startXrayEdit(xrayContextTarget);
      }
    });
    return xrayMenu;
  }

  function showXrayMenu(x, y, target) {
    xrayContextTarget = target;
    var menu = ensureXrayMenu();
    menu.style.left = '0px';
    menu.style.top  = '0px';
    menu.classList.add('visible');
    // Now we have a size, clamp inside the viewport.
    var rect = menu.getBoundingClientRect();
    var maxX = window.innerWidth  - rect.width  - 4;
    var maxY = window.innerHeight - rect.height - 4;
    menu.style.left = Math.max(0, Math.min(x, maxX)) + 'px';
    menu.style.top  = Math.max(0, Math.min(y, maxY)) + 'px';
  }

  function hideXrayMenu() {
    if (xrayMenu) xrayMenu.classList.remove('visible');
    xrayContextTarget = null;
  }

  document.addEventListener('contextmenu', function (e) {
    var block = findBlock(e.target);
    if (!block) return;
    e.preventDefault();
    showXrayMenu(e.clientX, e.clientY, block);
  });
  document.addEventListener('mousedown', function (e) {
    if (xrayMenu && xrayMenu.classList.contains('visible')) hideXrayMenu();
  }, true);
  window.addEventListener('blur', hideXrayMenu);
  window.addEventListener('scroll', hideXrayMenu, true);
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') hideXrayMenu();
    // Ctrl+E starts x-ray on the block under the mouse, like the menu does.
    if (e.key === 'e' && (e.ctrlKey || e.metaKey) && !e.shiftKey && !e.altKey) {
      var hovered = document.querySelector('[data-line]:hover');
      var block = findBlock(hovered || document.activeElement);
      if (block) { e.preventDefault(); startXrayEdit(block); }
    }
  });

  function startXrayEdit(element) {
    if (xrayActive) return;
    var startLine = parseInt(element.getAttribute('data-line'),     10);
    var endLine   = parseInt(element.getAttribute('data-line-end'), 10);
    if (isNaN(startLine) || isNaN(endLine) || endLine <= startLine) return;

    var lines = currentSource.split('\n');
    var safeStart = Math.max(0, Math.min(startLine, lines.length));
    var safeEnd   = Math.max(safeStart + 1, Math.min(endLine, lines.length));
    var raw = lines.slice(safeStart, safeEnd).join('\n');

    openXrayEditor(element, safeStart, safeEnd, raw);
  }

  function openXrayEditor(element, startLine, endLine, rawText) {
    var wrap = document.createElement('div');
    wrap.className = 'mds-xray';
    wrap.innerHTML =
      '<div class="mds-xray-toolbar">' +
        '<span class="mds-xray-label">X-ray editing source lines ' +
          (startLine + 1) + '–' + endLine + '</span>' +
        '<button class="mds-xray-save"   type="button">Save</button>' +
        '<button class="mds-xray-cancel" type="button">Cancel</button>' +
        '<span class="mds-xray-hint">Ctrl+Enter save · Esc cancel</span>' +
      '</div>' +
      '<textarea class="mds-xray-text" spellcheck="false" wrap="off"></textarea>';

    element.insertAdjacentElement('beforebegin', wrap);
    element.style.display = 'none';

    var textarea = wrap.querySelector('.mds-xray-text');
    textarea.value = rawText;

    function autoResize() {
      textarea.style.height = 'auto';
      textarea.style.height = Math.max(40, textarea.scrollHeight + 2) + 'px';
    }
    autoResize();
    textarea.addEventListener('input', autoResize);
    textarea.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') { e.preventDefault(); cancelXrayEditor(); }
      if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); saveXrayEditor(); }
    });

    wrap.querySelector('.mds-xray-save'  ).addEventListener('click', saveXrayEditor);
    wrap.querySelector('.mds-xray-cancel').addEventListener('click', cancelXrayEditor);

    xrayState = {
      wrap: wrap,
      hiddenElement: element,
      startLine: startLine,
      endLine:   endLine,
    };
    xrayActive = true;

    // Put cursor at start; user can Ctrl+A to select all if they want.
    setTimeout(function () { textarea.focus(); textarea.setSelectionRange(0, 0); }, 0);
  }

  function saveXrayEditor() {
    if (!xrayActive || !xrayState) return;
    var text = xrayState.wrap.querySelector('.mds-xray-text').value;
    // Convert the markdown-it style range (0-based inclusive start, 0-based
    // exclusive end) to Monaco's 1-based inclusive convention.
    var monacoStart = xrayState.startLine + 1;
    var monacoEnd   = xrayState.endLine;
    postToHost({
      type: 'xrayApply',
      startLine: monacoStart,
      endLine:   monacoEnd,
      text:      text,
    });
    // Mark inactive so the upcoming render() (triggered by Monaco's change
    // event) can rebuild the DOM and our wrap disappears cleanly.
    xrayActive = false;
    if (xrayState.wrap) xrayState.wrap.classList.add('mds-xray-saving');
    xrayState = null;
  }

  function cancelXrayEditor() {
    if (!xrayActive || !xrayState) return;
    var st = xrayState;
    xrayState = null;
    xrayActive = false;
    if (st.wrap && st.wrap.parentNode) st.wrap.parentNode.removeChild(st.wrap);
    if (st.hiddenElement) st.hiddenElement.style.display = '';
  }

  window.host = {
    render: render,
    setTheme: setTheme,
    scrollToLine: function (line) {
      lastSyncIn = Date.now();
      scrollToSourceLine(line);
    },
  };

  // Forward F11 to the host so distraction-free mode toggles even when the
  // preview WebView2 has focus.
  window.addEventListener('keydown', function (e) {
    if (e.key === 'F11') {
      e.preventDefault();
      e.stopPropagation();
      postToHost({ type: 'toggleFocus' });
    }
  }, true);

  postToHost({ type: 'ready' });
})();
