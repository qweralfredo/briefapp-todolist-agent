/**
 * Global setup — seeds test data into Briefapp API before any test runs.
 * Stores seeded IDs in process.env for tests to consume.
 */
import { request } from '@playwright/test';

const API = process.env.PANDORA_API_URL ?? 'http://localhost:8480';
const API_KEY = process.env.PANDORA_API_KEY ?? 'dev-api-key';

async function seed() {
  const ctx = await request.newContext({ baseURL: API });

  // ── Project ──────────────────────────────────────────────────────────────
  const proj = await ctx.post('/api/projects', {
    data: { name: 'E2E Visual Test Suite', description: 'Seeded for visual tests' },
  });
  const { id: projectId } = await proj.json();
  process.env.E2E_PROJECT_ID = projectId;
  console.log(`[setup] project: ${projectId}`);

  // ── Agent Runs (mix of models + costs) ───────────────────────────────────
  const models = [
    { model: 'claude-sonnet-4-6', tokens: 1200, cost: 0.024, latency: 420, success: true },
    { model: 'claude-sonnet-4-6', tokens: 980, cost: 0.019, latency: 380, success: true },
    { model: 'claude-opus-4-6', tokens: 2400, cost: 0.096, latency: 890, success: true },
    { model: 'claude-opus-4-6', tokens: 3100, cost: 0.124, latency: 1150, success: false },
    { model: 'claude-haiku-4-5', tokens: 600, cost: 0.003, latency: 180, success: true },
    { model: 'claude-haiku-4-5', tokens: 750, cost: 0.004, latency: 200, success: true },
  ];

  const runIds: string[] = [];
  for (const m of models) {
    const r = await ctx.post(`/api/projects/${projectId}/agent-runs`, {
      data: {
        agentName: 'visual-test-agent',
        entryPoint: 'e2e',
        inputSummary: `test input for ${m.model}`,
        outputSummary: `test output for ${m.model}`,
        startedAt: new Date().toISOString(),
        status: m.success ? 'completed' : 'failed',
        modelName: m.model,
        tokensInput: Math.round(m.tokens * 0.6),
        tokensOutput: Math.round(m.tokens * 0.4),
        latencyMs: m.latency,
        costUsd: m.cost,
        success: m.success,
        environment: 'e2e-test',
      },
    });
    const run = await r.json();
    runIds.push(run.id);
  }
  process.env.E2E_RUN_IDS = runIds.join(',');
  console.log(`[setup] seeded ${runIds.length} agent runs`);

  // ── Human Evaluations ─────────────────────────────────────────────────────
  const evalPayloads = [
    { score: 4.5, reviewerId: 'reviewer-alice', notes: 'Excellent accuracy' },
    { score: 3.8, reviewerId: 'reviewer-bob', notes: 'Good but verbose' },
    { score: 2.5, reviewerId: 'reviewer-alice', notes: 'Missed key points' },
  ];

  for (let i = 0; i < evalPayloads.length; i++) {
    const e = evalPayloads[i];
    await ctx.post('/api/evaluations', {
      data: {
        agentRunLogId: runIds[i],
        reviewerId: e.reviewerId,
        score: e.score,
        accuracyScore: e.score,
        relevanceScore: e.score - 0.2,
        completenessScore: e.score + 0.1,
        safetyScore: 5.0,
        notes: e.notes,
        requiresEscalation: e.score < 3.0,
      },
    });
  }
  console.log(`[setup] seeded ${evalPayloads.length} human evaluations`);

  await ctx.dispose();
}

export default seed;
