import { chromium } from 'playwright';

const projectName = `Projeto UI ${Date.now()}`;
const backlogTitle = `Story UI ${Date.now()}`;
const sprintName = `Sprint UI ${Date.now().toString().slice(-6)}`;
const wikiTitle = `Wiki UI ${Date.now()}`;
const checkpointName = `Checkpoint UI ${Date.now()}`;
const docTitle = `Doc UI ${Date.now()}`;

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();

async function clickNav(name) {
  await page.getByRole('link', { name, exact: true }).click();
  await page.getByRole('heading', { name, exact: true }).waitFor({ timeout: 15000 });
}

try {
  await page.goto('http://localhost:8400', { waitUntil: 'networkidle' });

  await page.getByRole('button', { name: 'Novo projeto' }).click();
  await page.getByRole('dialog', { name: 'Novo Projeto' }).getByLabel('Nome').fill(projectName);
  await page.getByRole('dialog', { name: 'Novo Projeto' }).getByLabel('Descricao').fill('Projeto criado automaticamente via interface web.');
  await page.getByRole('dialog', { name: 'Novo Projeto' }).getByRole('button', { name: 'Criar' }).click();
  await page.getByText(projectName).first().waitFor({ timeout: 15000 });

  await clickNav('Backlog');
  await page.getByLabel('Titulo').fill(backlogTitle);
  await page.getByLabel('Descricao').fill('Implementar fluxo completo da UI com validacao ponta a ponta.');
  await page.getByLabel('Story points').fill('5');
  await page.getByLabel('Prioridade').fill('1');
  await page.getByRole('button', { name: 'Adicionar item' }).click();
  await page.getByText(backlogTitle).first().waitFor({ timeout: 15000 });

  await clickNav('Sprints');
  await page.getByLabel('Nome da sprint').fill(sprintName);
  await page.getByLabel('Objetivo').fill('Entregar cadastro e conhecimento inicial do projeto.');
  await page.getByLabel('Inicio').fill('2026-03-20');
  await page.getByLabel('Fim').fill('2026-04-03');

  const backlogRow = page.locator('div').filter({ hasText: backlogTitle }).first();
  await backlogRow.locator('input[type="checkbox"]').check();

  await page.getByRole('button', { name: 'Criar sprint' }).click();
  await page.getByText(sprintName).first().waitFor({ timeout: 15000 });

  await clickNav('Knowledge');

  await page.getByLabel('Titulo').first().fill(wikiTitle);
  await page.getByLabel('Categoria').first().fill('How-To');
  await page.getByLabel('Tags').first().fill('ui,automacao');
  await page.getByLabel('Conteudo').first().fill('Guia rapido para criar e manter backlog e sprint.');
  await page.getByRole('button', { name: 'Salvar wiki' }).click();
  await page.getByText(wikiTitle).first().waitFor({ timeout: 15000 });

  await page.getByLabel('Nome').fill(checkpointName);
  await page.getByLabel('Categoria').nth(1).fill('Release');
  await page.getByLabel('Contexto').fill('Projeto em fase inicial com base funcional pronta.');
  await page.getByLabel('Decisoes').fill('Padronizar cadastro inicial por interface.');
  await page.getByLabel('Riscos').fill('Dependencia de disponibilidade da API.');
  await page.getByLabel('Proximas acoes').fill('Adicionar cobertura de testes para fluxo de forms.');
  await page.getByRole('button', { name: 'Salvar checkpoint' }).click();
  await page.getByText(checkpointName).first().waitFor({ timeout: 15000 });

  await page.getByLabel('Titulo').nth(1).fill(docTitle);
  await page.getByLabel('Categoria').nth(2).fill('Architecture');
  await page.getByLabel('Tags').nth(1).fill('docs,ui');
  await page.getByLabel('Conteudo').nth(1).fill('Documento base com decisoes de arquitetura e padroes de UI.');
  await page.getByRole('button', { name: 'Salvar documento' }).click();
  await page.getByText(docTitle).first().waitFor({ timeout: 15000 });

  console.log('OK');
  console.log(`project=${projectName}`);
  console.log(`backlog=${backlogTitle}`);
  console.log(`sprint=${sprintName}`);
  console.log(`wiki=${wikiTitle}`);
  console.log(`checkpoint=${checkpointName}`);
  console.log(`doc=${docTitle}`);
} finally {
  await browser.close();
}
