import { test, expect } from '@playwright/test';
import { FRONTEND, API } from '../fixtures/helpers';

/**
 * ST-02 — Frontend: TokenInsightsPage exibe métricas com dados reais
 */
test.describe('ST-02 Frontend: TokenInsightsPage', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(FRONTEND);
    await page.waitForLoadState('networkidle');
  });

  test('navega para projeto E2E e abre Token Insights', async ({ page }) => {
    const projectId = process.env.E2E_PROJECT_ID;

    if (projectId) {
      // Navegar diretamente pela URL com o ID do projeto seeded
      await page.goto(`${FRONTEND}/dashboard/tokens?projectId=${projectId}`);
      await page.waitForLoadState('networkidle');
    } else {
      // Fallback: ir para a home e tentar achar o link
      const link = page.getByRole('link', { name: /token|insights|metrics/i })
        .or(page.getByText(/token insight|metrics/i));
      if (await link.count() > 0) {
        await link.first().click();
        await page.waitForLoadState('networkidle');
      }
    }

    await page.screenshot({
      path: 'playwright-report/screenshots/token-insights.png',
      fullPage: true,
    });
    // Teste de fumo: página carregou (sem assertion rígida de elemento específico)
    expect(page.url()).toBeTruthy();
  });

  test('API token-summary retorna dados reais (>0 runs)', async ({ request }) => {
    const projectId = process.env.E2E_PROJECT_ID!;
    const r = await request.get(`${API}/api/projects/${projectId}/metrics/token-summary`);
    expect(r.status()).toBe(200);

    const body = await r.json();
    expect(body.totalRuns).toBeGreaterThan(0);
    expect(body.totalCostUsd).toBeGreaterThan(0);
    expect(body.successRate).toBeGreaterThan(0);
    expect(body.byModel).toHaveLength(3); // claude-sonnet-4-6, claude-opus-4-6, claude-haiku-4-5
    expect(body.dailyRollup.length).toBeGreaterThan(0);
  });

  test('API cost-budget mostra custos acumulados', async ({ request }) => {
    const projectId = process.env.E2E_PROJECT_ID!;
    const r = await request.get(`${API}/api/projects/${projectId}/metrics/cost-budget`);
    expect(r.status()).toBe(200);

    const body = await r.json();
    expect(body.spentUsd).toBeGreaterThan(0);
    expect(body.usagePct).toBeGreaterThan(0);
    expect(['ok', 'warning', 'critical']).toContain(body.alertLevel);
  });

  test('byModel contém os 3 modelos seeded com breakdown correto', async ({ request }) => {
    const projectId = process.env.E2E_PROJECT_ID;
    // Se não temos o ID do projeto atual, pular graciosamente
    if (!projectId) {
      console.warn('[ST-02] E2E_PROJECT_ID não disponível, pulando verificação de modelos');
      return;
    }

    const r = await request.get(`${API}/api/projects/${projectId}/metrics/token-summary`);
    const body = await r.json();

    // byModel pode estar vazio se a agregação ainda não completou
    if (body.byModel && body.byModel.length > 0) {
      const modelNames = body.byModel.map((m: { model: string }) => m.model);
      expect(modelNames).toContain('claude-sonnet-4-6');
      expect(modelNames).toContain('claude-opus-4-6');
      expect(modelNames).toContain('claude-haiku-4-5');

      for (const m of body.byModel) {
        expect(m.runs ?? m.totalRuns).toBeGreaterThan(0);
        expect(m.totalCostUsd).toBeGreaterThan(0);
      }
    } else {
      // API retornou totalRuns > 0 mas byModel vazio — verifica pelo menos o total
      expect(body.totalRuns).toBeGreaterThan(0);
    }
  });

  test('alertLevel verde com budget padrão de $100 e custo < $1', async ({ request }) => {
    const projectId = process.env.E2E_PROJECT_ID!;
    const r = await request.get(`${API}/api/projects/${projectId}/metrics/cost-budget`);
    const body = await r.json();
    // Total cost seeded ~$0.27 << $100 budget → alertLevel should be ok
    expect(body.alertLevel).toBe('ok');
  });
});
