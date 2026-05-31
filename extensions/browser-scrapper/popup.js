/**
 * Briefapp Context Scrapper — Popup Script
 */

document.addEventListener('DOMContentLoaded', async () => {
  // ── Elementos ────────────────────────────────────────────────────────
  const ingestBtn = document.getElementById('ingest-btn');
  const statusLabel = document.getElementById('status-label');
  const statusBadge = document.getElementById('status-badge');
  const pulse = document.getElementById('pulse');
  const previewArea = document.getElementById('preview-area');
  const previewHolder = document.getElementById('preview-placeholder');
  const charCount = document.getElementById('char-count');
  const endpointDisplay = document.getElementById('endpoint-display');
  const selectionHint = document.getElementById('selection-hint');
  const historySection = document.getElementById('history-section');
  const historyList = document.getElementById('history-list');
  const clearHistoryBtn = document.getElementById('clear-history');

  const settingsBtn = document.getElementById('settings-btn');
  const homeView = document.getElementById('home-view');
  const settingsView = document.getElementById('settings-view');
  const backBtn = document.getElementById('back-btn');
  const saveBtn = document.getElementById('save-settings');
  const urlInput = document.getElementById('briefapp-url');
  const boxIdInput = document.getElementById('box-id');
  const apiKeyInput = document.getElementById('api-key');

  // ── Carrega configurações ─────────────────────────────────────────────
  const loadSettings = async () => {
    const cfg = await chrome.storage.local.get(['briefapp_url', 'box_id', 'api_key', 'history']);
    const url = cfg.briefapp_url || 'http://76.13.238.113:8481/mcp';
    const host = url.replace(/https?:\/\//, '').split('/')[0];
    endpointDisplay.textContent = host.length > 28 ? host.slice(0, 28) + '…' : host;
    urlInput.value = url;
    boxIdInput.value = cfg.box_id || '';
    apiKeyInput.value = cfg.api_key || '';
    renderHistory(cfg.history || []);
  };

  const renderHistory = (hist) => {
    if (!hist.length) { historySection.style.display = 'none'; return; }
    historySection.style.display = 'block';
    historyList.innerHTML = hist.slice(0, 5).map(e => `
      <li class="history-item" title="${e.url}">
        <span class="history-icon">${e.selection ? '✂️' : '📄'}</span>
        <span class="history-text">${e.title.slice(0, 35) || e.url.slice(0, 35)}</span>
        <span class="history-chars">${(e.chars / 1000).toFixed(1)}k</span>
      </li>
    `).join('');
  };

  await loadSettings();

  // ── Navegação Settings ────────────────────────────────────────────────
  settingsBtn.addEventListener('click', () => {
    homeView.classList.add('hidden');
    settingsView.classList.remove('hidden');
  });

  backBtn.addEventListener('click', () => {
    settingsView.classList.add('hidden');
    homeView.classList.remove('hidden');
  });

  saveBtn.addEventListener('click', async () => {
    await chrome.storage.local.set({
      briefapp_url: urlInput.value.trim() || 'http://76.13.238.113:8481/mcp',
      box_id: boxIdInput.value.trim(),
      api_key: apiKeyInput.value.trim(),
    });
    saveBtn.textContent = '✓ Salvo!';
    saveBtn.classList.add('saved');
    setTimeout(async () => {
      saveBtn.textContent = '💾 Salvar Configurações';
      saveBtn.classList.remove('saved');
      settingsView.classList.add('hidden');
      homeView.classList.remove('hidden');
      await loadSettings();
    }, 1200);
  });

  // ── Ingest ────────────────────────────────────────────────────────────
  const setStatus = (text, state = 'idle') => {
    statusLabel.textContent = text;
    statusBadge.className = `status-badge status-${state}`;
    pulse.style.display = state === 'idle' ? '' : 'none';
  };

  ingestBtn.addEventListener('click', async () => {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    setStatus('Extraindo...', 'working');
    ingestBtn.disabled = true;
    ingestBtn.querySelector('.btn-text').textContent = 'Trabalhando...';
    charCount.style.display = 'none';
    previewHolder && (previewHolder.style.display = '');
    previewArea.innerHTML = `<p class="placeholder">Lendo página...</p>`;

    try {
      // Injeta o content script se necessário (páginas sem acesso declarativo)
      let response;
      try {
        response = await chrome.tabs.sendMessage(tab.id, { action: 'SCRAPE_CONTEXT' });
      } catch {
        await chrome.scripting.executeScript({ target: { tabId: tab.id }, files: ['content.js'] });
        response = await chrome.tabs.sendMessage(tab.id, { action: 'SCRAPE_CONTEXT' });
      }

      if (!response?.success) throw new Error(response?.error || 'Falha na extração');

      const { data } = response;

      // Mostra preview
      previewArea.innerHTML = `
        <div class="preview-content">
          <div class="preview-mode">${data.selection ? '✂️ Trecho selecionado' : '📄 Página inteira'}</div>
          <div class="preview-title">${data.title.slice(0, 55)}</div>
          <div class="preview-snippet">${data.bodyText.slice(0, 120).replace(/\n/g, ' ')}…</div>
        </div>
      `;
      charCount.textContent = `${(data.charCount / 1000).toFixed(1)}k chars`;
      charCount.style.display = '';

      setStatus('Enviando...', 'sending');

      const ingestResp = await chrome.runtime.sendMessage({ type: 'INGEST_DATA', payload: data });

      if (!ingestResp?.success) throw new Error(ingestResp?.error || 'Erro no ingest');

      setStatus('Enviado! ✓', 'success');
      selectionHint.style.display = 'none';
      await loadSettings(); // Atualiza histórico

    } catch (err) {
      setStatus('Erro ✗', 'error');
      previewArea.innerHTML = `<p class="preview-error">${err.message.slice(0, 120)}</p>`;
      console.error('[Briefapp]', err);
    } finally {
      setTimeout(() => {
        setStatus('Pronto', 'idle');
        ingestBtn.disabled = false;
        ingestBtn.querySelector('.btn-text').textContent = 'Ingest Page';
        selectionHint.style.display = '';
      }, 3000);
    }
  });

  // ── Limpar histórico ──────────────────────────────────────────────────
  clearHistoryBtn.addEventListener('click', async () => {
    await chrome.runtime.sendMessage({ type: 'CLEAR_HISTORY' });
    renderHistory([]);
  });
});
