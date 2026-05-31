/**
 * Briefapp Context Scrapper — Content Script
 * Extrai conteúdo da página (inteiro ou seleção) e converte para Markdown.
 */

/** Converte HTML em Markdown simplificado adequado para RAG */
function htmlToMarkdown(element) {
  let md = '';
  element.childNodes.forEach(node => {
    if (node.nodeType === Node.TEXT_NODE) {
      const text = node.textContent.replace(/\s+/g, ' ').trim();
      if (text) md += text + ' ';
    } else if (node.nodeType === Node.ELEMENT_NODE) {
      const tag = node.tagName.toLowerCase();
      switch (tag) {
        case 'h1': md += '\n# ' + htmlToMarkdown(node).trim() + '\n\n'; break;
        case 'h2': md += '\n## ' + htmlToMarkdown(node).trim() + '\n\n'; break;
        case 'h3': md += '\n### ' + htmlToMarkdown(node).trim() + '\n\n'; break;
        case 'h4': md += '\n#### ' + htmlToMarkdown(node).trim() + '\n\n'; break;
        case 'b':
        case 'strong': md += '**' + htmlToMarkdown(node).trim() + '**'; break;
        case 'i':
        case 'em': md += '*' + htmlToMarkdown(node).trim() + '*'; break;
        case 'li': md += '\n- ' + htmlToMarkdown(node).trim(); break;
        case 'p': md += '\n\n' + htmlToMarkdown(node) + '\n'; break;
        case 'br': md += '\n'; break;
        case 'a': md += `[${htmlToMarkdown(node).trim()}](${node.href})`; break;
        case 'code': md += '`' + node.textContent.trim() + '`'; break;
        case 'pre': md += '\n```\n' + node.textContent.trim() + '\n```\n'; break;
        case 'blockquote': md += '\n> ' + htmlToMarkdown(node).trim() + '\n'; break;
        case 'table': md += extractTable(node); break;
        case 'script':
        case 'style':
        case 'noscript':
        case 'svg':
        case 'canvas': break; // ignora
        default: md += htmlToMarkdown(node);
      }
    }
  });
  return md.replace(/\n{3,}/g, '\n\n').trim();
}

/** Extrai tabela HTML como Markdown */
function extractTable(table) {
  const rows = Array.from(table.querySelectorAll('tr'));
  if (!rows.length) return '';
  let md = '\n';
  rows.forEach((row, i) => {
    const cells = Array.from(row.querySelectorAll('th, td'))
      .map(c => c.textContent.replace(/\|/g, '\\|').trim());
    md += '| ' + cells.join(' | ') + ' |\n';
    if (i === 0) md += '| ' + cells.map(() => '---').join(' | ') + ' |\n';
  });
  return md + '\n';
}

/** Obtém texto da seleção atual do usuário (se houver) */
function getSelectedText() {
  const sel = window.getSelection();
  if (!sel || sel.isCollapsed) return null;
  const range = sel.getRangeAt(0);
  const div = document.createElement('div');
  div.appendChild(range.cloneContents());
  return htmlToMarkdown(div);
}

/** Extrai página inteira (clone limpo) */
function getFullPage() {
  const docClone = document.cloneNode(true);
  const remove = ['script', 'style', 'noscript', 'iframe', 'nav', 'footer',
    'header', 'aside', 'svg', 'canvas', 'form', 'button', 'input'];
  remove.forEach(tag => docClone.querySelectorAll(tag).forEach(el => el.remove()));
  docClone.querySelectorAll('[style*="display:none"], [style*="display: none"], [hidden]')
    .forEach(el => el.remove());

  // Preferência pelo main ou article se existir
  const main = docClone.querySelector('main, article, [role="main"]') || docClone.body;
  return htmlToMarkdown(main).slice(0, 12000);
}

chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.action === 'SCRAPE_CONTEXT') {
    try {
      const selectionText = getSelectedText();
      const isSelection = !!selectionText && selectionText.length > 20;
      const bodyText = isSelection ? selectionText.slice(0, 12000) : getFullPage();

      const pageData = {
        title: document.title,
        url: window.location.href,
        timestamp: new Date().toISOString(),
        meta: {
          description: document.querySelector('meta[name="description"]')?.content || '',
          ogTitle: document.querySelector('meta[property="og:title"]')?.content || '',
        },
        bodyText,
        selection: isSelection,
        charCount: bodyText.length,
      };
      sendResponse({ success: true, data: pageData });
    } catch (error) {
      sendResponse({ success: false, error: error.message });
    }
  }
  return true;
});
