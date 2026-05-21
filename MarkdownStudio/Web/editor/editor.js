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
    return params.get('theme') || 'vs';
  }

  require.config({ paths: { vs: './monaco/vs' } });

  require(['vs/editor/editor.main'], function () {
    var theme = getThemeFromQuery();

    var editor = monaco.editor.create(document.getElementById('container'), {
      value: '',
      language: 'markdown',
      theme: theme,
      automaticLayout: true,
      wordWrap: 'on',
      wrappingIndent: 'same',
      minimap: { enabled: true },
      lineNumbers: 'on',
      renderWhitespace: 'selection',
      fontSize: 14,
      fontFamily: "Cascadia Code, Consolas, 'Courier New', monospace",
      fontLigatures: true,
      smoothScrolling: true,
      cursorBlinking: 'smooth',
      cursorSmoothCaretAnimation: 'on',
      bracketPairColorization: { enabled: true },
      guides: { bracketPairs: true, indentation: true },
      formatOnPaste: true,
      formatOnType: true,
      tabSize: 2,
      insertSpaces: true,
      'semanticHighlighting.enabled': true,
      'unicodeHighlight.ambiguousCharacters': false,
    });

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

    // Markdown convenience keybindings (mirroring VS Code)
    editor.addAction({
      id: 'md.toggleBold',
      label: 'Markdown: Toggle Bold',
      keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyB],
      run: function (ed) { wrapSelection(ed, '**', '**'); },
    });
    editor.addAction({
      id: 'md.toggleItalic',
      label: 'Markdown: Toggle Italic',
      keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyI],
      run: function (ed) { wrapSelection(ed, '_', '_'); },
    });
    editor.addAction({
      id: 'md.insertLink',
      label: 'Markdown: Insert Link',
      keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyK],
      run: function (ed) { wrapSelection(ed, '[', '](url)'); },
    });
    editor.addAction({
      id: 'md.toggleInlineCode',
      label: 'Markdown: Toggle Inline Code',
      keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.Backquote],
      run: function (ed) { wrapSelection(ed, '`', '`'); },
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

    window.host = {
      setText: function (text) {
        lastSent = text;
        editor.setValue(text);
      },
      getText: function () {
        return editor.getValue();
      },
      setTheme: function (themeName) {
        monaco.editor.setTheme(themeName);
      },
      setWordWrap: function (enabled) {
        editor.updateOptions({ wordWrap: enabled ? 'on' : 'off' });
      },
      focus: function () { editor.focus(); },
    };

    postToHost({ type: 'ready' });
    editor.focus();
  });
})();
