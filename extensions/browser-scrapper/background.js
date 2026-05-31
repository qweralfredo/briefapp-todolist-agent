/**
 * Briefapp Context Scrapper — Background Service Worker
 * Gerencia comunicação, ingest API e histórico de envios.
 */

const DEFAULT_URL = 'http://76.13.238.113:8481/mcp';
const DEFAULT_BOX_ID = '';

// Inicializa storage com defaults
chrome.runtime.onInstalled.addListener(() => {
  chrome.storage.local.get(['briefapp_url', 'box_id', 'history'], (result) => {
    const updates = {};
    if (!result.briefapp_url) updates.briefapp_url = DEFAULT_URL;
    if (result.box_id === undefined) updates.box_id = DEFAULT_BOX_ID;
    if (!result.history) updates.history = [];
    if (Object.keys(updates).length > 0) {
      chrome.storage.local.set(updates);
    }
  });
});

/**
 * Envia contexto ao Briefapp Box via context_box_ingest (MCP JSON-RPC).
 */
async function sendToBriefapp(data) {
  const settings = await chrome.storage.local.get(['briefapp_url', 'box_id', 'api_key']);
  const url = settings.briefapp_url || DEFAULT_URL;
  const boxId = settings.box_id || '';

  // Monta o payload MCP (tools/call → context_box_ingest)
  const mcpPayload = {
    jsonrpc: '2.0',
    id: Date.now(),
    method: 'tools/call',
    params: {
      name: 'context_box_ingest',
      arguments: {
        box_id: boxId,
        content: data.bodyText,
        source_url: data.url,
        title: data.title,
        metadata: {
          description: data.meta?.description || '',
          og_title: data.meta?.ogTitle || '',
          scraped_at: data.timestamp,
          selection: data.selection || false,
        }
      }
    }
  };

  const headers = {
    'Content-Type': 'application/json',
    'Accept': 'application/json, text/event-stream',
  };
  if (settings.api_key) {
    headers['X-Briefapp-Api-Key'] = settings.api_key;
  }

  try {
    const response = await fetch(url, {
      method: 'POST',
      headers,
      body: JSON.stringify(mcpPayload)
    });

    const rawText = await response.text();

    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${rawText.slice(0, 150)}`);
    }

    // Salva no histórico
    const { history = [] } = await chrome.storage.local.get('history');
    const entry = {
      id: Date.now(),
      title: data.title,
      url: data.url,
      timestamp: data.timestamp,
      chars: data.bodyText.length,
      selection: data.selection || false,
    };
    history.unshift(entry);
    await chrome.storage.local.set({ history: history.slice(0, 20) }); // máx 20

    // Exibe notificação nativa
    chrome.notifications.create(`briefapp_${entry.id}`, {
      type: 'basic',
      iconUrl: 'icons/icon48.png',
      title: 'Briefapp Box ✓',
      message: `"${data.title.slice(0, 50)}" enviado com sucesso!`,
      priority: 1,
    });

    return { success: true, chars: data.bodyText.length };
  } catch (error) {
    chrome.notifications.create(`briefapp_err_${Date.now()}`, {
      type: 'basic',
      iconUrl: 'icons/icon48.png',
      title: 'Briefapp Box — Erro',
      message: error.message.slice(0, 100),
      priority: 2,
    });
    return { success: false, error: error.message };
  }
}

// Listener principal
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === 'INGEST_DATA') {
    sendToBriefapp(message.payload).then(sendResponse);
    return true;
  }

  if (message.type === 'CLEAR_HISTORY') {
    chrome.storage.local.set({ history: [] }).then(() => sendResponse({ ok: true }));
    return true;
  }

  return true;
});
