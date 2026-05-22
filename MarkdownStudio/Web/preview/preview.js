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
    }, 30);
  }

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

  window.host = {
    render: render,
    setTheme: setTheme,
  };

  postToHost({ type: 'ready' });
})();
