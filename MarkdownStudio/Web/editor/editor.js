(function () {
  'use strict';

  function postToHost(msg) {
    try {
      window.chrome.webview.postMessage(msg);
    } catch (e) {
      console.error('postMessage failed', e);
    }
  }

  function getQueryParams() {
    var params = new URLSearchParams(window.location.search);
    var size = parseInt(params.get('size'), 10);
    var tab  = parseInt(params.get('tab'),  10);
    return {
      theme:  params.get('theme')  || 'ms-daylight',
      family: params.get('family') || "Cascadia Code, Consolas, 'Courier New', monospace",
      size:   isNaN(size) ? 14 : size,
      tab:    isNaN(tab)  ? 2  : tab,
      ws:     params.get('ws') === '1',
    };
  }

  require.config({ paths: { vs: './monaco/vs' } });

  require(['vs/editor/editor.main'], function () {
    // Custom themes that match the app's AppTheme palette
    monaco.editor.defineTheme('ms-daylight', {
      base: 'vs', inherit: true, rules: [],
      colors: {
        'editor.background': '#FCFCFC',
        'editor.foreground': '#14161C',
        'editorLineNumber.foreground': '#A8AAB0',
        'editorLineNumber.activeForeground': '#14161C',
        'editor.lineHighlightBackground': '#F0F0F2',
        'editor.selectionBackground': '#CCE4FF',
        'editorCursor.foreground': '#007ACC',
      },
    });
    monaco.editor.defineTheme('ms-midnight', {
      base: 'vs-dark', inherit: true, rules: [],
      colors: {
        'editor.background': '#14161C',
        'editor.foreground': '#E8E8EC',
        'editorLineNumber.foreground': '#5A5E68',
        'editorLineNumber.activeForeground': '#E8E8EC',
        'editor.lineHighlightBackground': '#1E212B',
        'editor.selectionBackground': '#264F78',
        'editorCursor.foreground': '#569CD6',
      },
    });
    monaco.editor.defineTheme('ms-sepia', {
      base: 'vs', inherit: true,
      rules: [{ token: '', foreground: '40231F' }],
      colors: {
        'editor.background': '#FCF6E8',
        'editor.foreground': '#40231F',
        'editorLineNumber.foreground': '#A89476',
        'editorLineNumber.activeForeground': '#40231F',
        'editor.lineHighlightBackground': '#F2EAD2',
        'editor.selectionBackground': '#E8D8B0',
        'editorCursor.foreground': '#A56623',
      },
    });
    monaco.editor.defineTheme('ms-solarized-light', {
      base: 'vs', inherit: true, rules: [],
      colors: {
        'editor.background': '#FDF6E3',
        'editor.foreground': '#586E75',
        'editorLineNumber.foreground': '#93A1A1',
        'editorLineNumber.activeForeground': '#586E75',
        'editor.lineHighlightBackground': '#EEE8D5',
        'editor.selectionBackground': '#D8D1B0',
        'editorCursor.foreground': '#268BD2',
      },
    });
    monaco.editor.defineTheme('ms-solarized-dark', {
      base: 'vs-dark', inherit: true, rules: [],
      colors: {
        'editor.background': '#002B36',
        'editor.foreground': '#EEE8D5',
        'editorLineNumber.foreground': '#586E75',
        'editorLineNumber.activeForeground': '#EEE8D5',
        'editor.lineHighlightBackground': '#073642',
        'editor.selectionBackground': '#14546A',
        'editorCursor.foreground': '#268BD2',
      },
    });

    var q = getQueryParams();

    var editor = monaco.editor.create(document.getElementById('container'), {
      value: '',
      language: 'markdown',
      theme: q.theme,
      automaticLayout: true,
      wordWrap: 'on',
      wrappingIndent: 'same',
      minimap: { enabled: true, renderCharacters: false },
      lineNumbers: 'on',
      renderWhitespace: q.ws ? 'all' : 'selection',
      fontSize: q.size,
      lineHeight: Math.round(q.size * 1.55),
      fontFamily: q.family,
      fontLigatures: true,
      smoothScrolling: true,
      cursorBlinking: 'smooth',
      cursorSmoothCaretAnimation: 'on',
      bracketPairColorization: { enabled: true },
      guides: { bracketPairs: true, indentation: true },
      formatOnPaste: true,
      formatOnType: true,
      tabSize: q.tab,
      insertSpaces: true,
      detectIndentation: false,
      padding: { top: 16, bottom: 16 },
      scrollBeyondLastLine: false,
      'semanticHighlighting.enabled': true,
      'unicodeHighlight.ambiguousCharacters': false,
    });

    // Apply tabSize to the model explicitly. editor.create accepts it but
    // detectIndentation: false also disables the auto-detect pass that
    // would normally seed this — set it ourselves so it actually sticks.
    var currentTabSize = q.tab;
    function applyTabSize(size) {
      var model = editor.getModel();
      if (model) model.updateOptions({ tabSize: size, insertSpaces: true });
    }
    applyTabSize(currentTabSize);

    var lastSent = '';
    var debounceTimer = null;
    editor.onDidChangeModelContent(function () {
      var text = editor.getValue();
      if (text === lastSent) return;
      lastSent = text;
      if (debounceTimer) clearTimeout(debounceTimer);
      debounceTimer = setTimeout(function () {
        postToHost({ type: 'changed', text: text });
      }, 80);
    });

    // Scroll-sync: notify host of editor scrolls so it can mirror in preview.
    var lastSyncIn = 0;
    var SYNC_LOCK_MS = 220;
    var scrollDebounce = null;
    editor.onDidScrollChange(function () {
      if (Date.now() - lastSyncIn < SYNC_LOCK_MS) return;
      if (scrollDebounce) clearTimeout(scrollDebounce);
      scrollDebounce = setTimeout(function () {
        var ranges = editor.getVisibleRanges();
        if (!ranges || !ranges.length) return;
        postToHost({ type: 'scrolled', line: ranges[0].startLineNumber });
      }, 20);
    });

    function wrapSelection(ed, before, after) {
      var sel = ed.getSelection();
      var model = ed.getModel();
      if (!sel || !model) return;
      var text = model.getValueInRange(sel);
      var replacement = before + text + after;
      ed.executeEdits('md-wrap', [{ range: sel, text: replacement, forceMoveMarkers: true }]);
      ed.focus();
    }

    editor.addAction({
      id: 'md.toggleBold', label: 'Markdown: Toggle Bold',
      keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyB],
      run: function (ed) { wrapSelection(ed, '**', '**'); },
    });
    editor.addAction({
      id: 'md.toggleItalic', label: 'Markdown: Toggle Italic',
      keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyI],
      run: function (ed) { wrapSelection(ed, '_', '_'); },
    });
    editor.addAction({
      id: 'md.insertLink', label: 'Markdown: Insert Link',
      keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyK],
      run: function (ed) { wrapSelection(ed, '[', '](url)'); },
    });
    editor.addAction({
      id: 'md.toggleInlineCode', label: 'Markdown: Toggle Inline Code',
      keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.Backquote],
      run: function (ed) { wrapSelection(ed, '`', '`'); },
    });

    window.host = {
      setText: function (text) {
        lastSent = text;
        editor.setValue(text);
        // setValue can reset model options on some Monaco versions; reapply.
        applyTabSize(currentTabSize);
      },
      getText: function () { return editor.getValue(); },
      setTheme: function (themeName) { monaco.editor.setTheme(themeName); },
      setWordWrap: function (enabled) { editor.updateOptions({ wordWrap: enabled ? 'on' : 'off' }); },
      setFontOptions: function (family, size, tabSize) {
        var opts = {};
        if (family) opts.fontFamily = family;
        if (size)   { opts.fontSize = size; opts.lineHeight = Math.round(size * 1.55); }
        editor.updateOptions(opts);
        if (tabSize) {
          currentTabSize = tabSize;
          applyTabSize(tabSize);
        }
      },
      setRenderWhitespace: function (showAll) {
        editor.updateOptions({ renderWhitespace: showAll ? 'all' : 'selection' });
      },
      revealLine: function (lineNumber, query) {
        try {
          // Mark this as a programmatic scroll so the resulting scroll event
          // doesn't bounce back as a "scrolled" sync message (host will scroll
          // the preview explicitly to the actual target line).
          lastSyncIn = Date.now();
          editor.revealLineInCenter(lineNumber, 1 /* ScrollType.Immediate */);
          editor.setPosition({ lineNumber: lineNumber, column: 1 });
          editor.focus();
          if (query) {
            var model = editor.getModel();
            if (model) {
              var content = model.getLineContent(lineNumber);
              var idx = content.toLowerCase().indexOf(query.toLowerCase());
              if (idx >= 0) {
                var range = new monaco.Range(
                  lineNumber, idx + 1,
                  lineNumber, idx + 1 + query.length);
                editor.setSelection(range);
              }
            }
          }
        } catch (_) {}
      },
      openFind: function () {
        try {
          editor.focus();
          var action = editor.getAction('actions.find');
          if (action) action.run();
        } catch (_) {}
      },
      // Replace a range of source lines (1-based inclusive) with new text. Used
      // by the preview's x-ray edit feature. Triggers the regular change-event
      // pipeline so the preview re-renders normally.
      replaceLines: function (startLine, endLine, newText) {
        try {
          var model = editor.getModel();
          if (!model) return;
          var lineCount = model.getLineCount();
          if (startLine < 1) startLine = 1;
          if (endLine > lineCount) endLine = lineCount;
          if (endLine < startLine) endLine = startLine;
          var range = new monaco.Range(
            startLine, 1,
            endLine,   model.getLineMaxColumn(endLine));
          editor.executeEdits('xray', [{
            range: range,
            text: newText == null ? '' : newText,
            forceMoveMarkers: true,
          }]);
        } catch (e) { console.error('replaceLines failed', e); }
      },
      focus: function () { editor.focus(); },
      scrollToLine: function (line) {
        try {
          lastSyncIn = Date.now();
          var top = editor.getTopForLineNumber(line);
          editor.setScrollTop(top, 1 /* Immediate */);
        } catch (_) {}
      },
    };

    // Forward F11 to the host so distraction-free mode toggles even when the
    // WebView2 has focus (WebView2 otherwise swallows the key).
    window.addEventListener('keydown', function (e) {
      if (e.key === 'F11') {
        e.preventDefault();
        e.stopPropagation();
        postToHost({ type: 'toggleFocus' });
      }
    }, true);

    postToHost({ type: 'ready' });
    editor.focus();
  });
})();
