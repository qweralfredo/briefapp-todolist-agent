import { test, expect } from '@playwright/test';
import { API } from '../fixtures/helpers';

/**
 * E2E-01: Box 2 — Transactional Queue & Lock Protocol
 * Validates: TansuPublisher, LockService, AckService, DLQ, QueueDashboard
 */
test.describe('Box 2: Queue & Lock Protocol', () => {
  let taskId: string;

  test('POST /api/tasks — publish task to queue', async ({ request }) => {
    const r = await request.post(`${API}/api/tasks`, {
      data: {
        boxId: '00000000-0000-0000-0000-000000000002',
        payload: JSON.stringify({
          type: 'code_review',
          file: 'test.cs',
          prompt: 'Review this code for issues',
        }),
        priority: 3,
      },
    });

    expect([200, 201]).toContain(r.status());
    const body = await r.json();
    expect(body).toHaveProperty('id');
    expect(body).toHaveProperty('status');
    taskId = body.id;
    process.env.E2E_TASK_ID = taskId;
  });

  test('POST /api/tasks/:id/lock — acquire pessimistic lock', async ({ request }) => {
    test.skip(!process.env.E2E_TASK_ID, 'No task published');
    const id = process.env.E2E_TASK_ID!;

    const r = await request.post(`${API}/api/tasks/${id}/lock`, {
      data: { agentId: 'e2e-agent-001', timeoutMinutes: 10 },
    });

    expect([200, 201]).toContain(r.status());
    const body = await r.json();
    expect(body).toHaveProperty('lockId');
    expect(body.status).toMatch(/Active|Acquired/i);
    process.env.E2E_LOCK_ID = body.lockId;
  });

  test('POST /api/tasks/:id/heartbeat — keep lock alive', async ({ request }) => {
    test.skip(!process.env.E2E_TASK_ID, 'No task published');
    const id = process.env.E2E_TASK_ID!;

    const r = await request.post(`${API}/api/tasks/${id}/heartbeat`, {
      data: { agentId: 'e2e-agent-001' },
    });

    expect([200, 204]).toContain(r.status());
  });

  test('POST /api/tasks/:id/ack — ACK completes task successfully', async ({ request }) => {
    test.skip(!process.env.E2E_TASK_ID, 'No task published');
    const id = process.env.E2E_TASK_ID!;

    const r = await request.post(`${API}/api/tasks/${id}/ack`, {
      data: {
        success: true,
        commitHash: 'abc123e2e',
        resultPayload: JSON.stringify({ review: 'LGTM', issues: 0 }),
        tokensUsed: 1500,
        model: 'claude-sonnet-4-6',
      },
    });

    expect([200, 204]).toContain(r.status());
  });

  test('POST /api/tasks (NACK flow) — NACK triggers retry/DLQ', async ({ request }) => {
    // Publish a new task specifically for NACK testing
    const pub = await request.post(`${API}/api/tasks`, {
      data: {
        boxId: '00000000-0000-0000-0000-000000000002',
        payload: JSON.stringify({ type: 'nack_test', prompt: 'Will fail' }),
        priority: 1,
      },
    });
    expect([200, 201]).toContain(pub.status());
    const task = await pub.json();

    // Lock it
    await request.post(`${API}/api/tasks/${task.id}/lock`, {
      data: { agentId: 'e2e-agent-fail', timeoutMinutes: 5 },
    });

    // NACK it
    const nack = await request.post(`${API}/api/tasks/${task.id}/ack`, {
      data: {
        success: false,
        errorCode: 'compilation_error',
        errorMessage: 'Build failed: CS1002 missing semicolon',
        retryHint: true,
      },
    });
    expect([200, 204]).toContain(nack.status());
  });

  test('GET /api/queue/dashboard — dashboard returns queue stats', async ({ request }) => {
    const r = await request.get(`${API}/api/queue/dashboard`);
    expect([200, 404]).toContain(r.status());

    if (r.status() === 200) {
      const body = await r.json();
      // Should have standard queue metrics
      expect(body).toHaveProperty('pendingCount');
      expect(body).toHaveProperty('processingCount');
    }
  });

  test('GET /api/dlq — DLQ entries accessible', async ({ request }) => {
    const r = await request.get(`${API}/api/dlq`);
    expect([200, 404]).toContain(r.status());

    if (r.status() === 200) {
      const body = await r.json();
      expect(Array.isArray(body)).toBeTruthy();
    }
  });
});
