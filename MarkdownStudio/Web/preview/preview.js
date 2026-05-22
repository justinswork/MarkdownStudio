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

  // Stamp top-level block tokens with their source-line number so the host
  // can map preview scroll position back to the editor.
  function injectLineNumbers(tokens, idx, options, env, slf) {
    var tok = tokens[idx];
    if (tok.map && tok.level === 0) {
      tok.attrSet('data-line', String(tok.map[0]));
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
    var line = token.map ? token.map[0] : 0;
    var attrs = ' data-line="' + line + '"';
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

  var renderTimer = null;
  function render(text) {
    if (renderTimer) clearTimeout(renderTimer);
    renderTimer = setTimeout(function () {
      var html = md.render(text || '');
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
