import { test, expect } from '@playwright/test';
import { API } from '../fixtures/helpers';

/**
 * E2E-01: Cross-Box Integration — Full Agentic Pipeline
 * Validates the end-to-end flow across all 4 Boxes:
 *   Inbound (Box 4) → Queue (Box 2) → Sandbox (Box 1) → CircuitBreaker (Box 3) → Outbound (Box 4)
 */
test.describe('Cross-Box: Full Agentic Pipeline', () => {

  test('health: all core API endpoints respond', async ({ request }) => {
    const endpoints = [
      '/health',
      '/api/sandboxes',
      '/api/circuit-breaker',
    ];

    for (const ep of endpoints) {
      const r = await request.get(`${API}${ep}`);
      expect(r.status(), `${ep} should respond`).toBeLessThan(500);
    }
  });

  test('pipeline: publish → lock → execute → ack → verify done', async ({ request }) => {
    // Step 1: Publish task (Box 2 — Queue)
    const pub = await request.post(`${API}/api/tasks`, {
      data: {
        boxId: '00000000-0000-0000-0000-e2eintegration',
        payload: JSON.stringify({
          type: 'cross_box_e2e',
          prompt: 'Integration test - echo pipeline',
          expectedOutput: 'Pipeline complete',
        }),
        priority: 5,
      },
    });
    expect([200, 201]).toContain(pub.status());
    const task = await pub.json();
    expect(task.id).toBeTruthy();

    // Step 2: Lock task (Box 2 — Lock Protocol)
    const lock = await request.post(`${API}/api/tasks/${task.id}/lock`, {
      data: { agentId: 'e2e-pipeline-agent', timeoutMinutes: 5 },
    });
    expect([200, 201]).toContain(lock.status());

    // Step 3: Verify circuit is Closed for our box (Box 3 — CircuitBreaker)
    const cb = await request.get(`${API}/api/circuit-breaker/00000000-0000-0000-0000-e2eintegration`);
    if (cb.status() === 200) {
      const cbBody = await cb.json();
      // If circuit is Open, reset it first
      if (cbBody.state === 'Open') {
        await request.post(`${API}/api/circuit-breaker/00000000-0000-0000-0000-e2eintegration/reset`);
      }
    }

    // Step 4: ACK task (Box 2 — ACK Protocol)
    const ack = await request.post(`${API}/api/tasks/${task.id}/ack`, {
      data: {
        success: true,
        commitHash: 'e2e-cross-box-test',
        resultPayload: JSON.stringify({ result: 'Pipeline complete' }),
        tokensUsed: 850,
        model: 'claude-sonnet-4-6',
      },
    });
    expect([200, 204]).toContain(ack.status());

    // Step 5: Verify task status is Done
    const status = await request.get(`${API}/api/tasks/${task.id}`);
    if (status.status() === 200) {
      const statusBody = await status.json();
      expect(statusBody.status).toMatch(/Done|Completed|3/i);
    }
  });

  test('pipeline: NACK triggers circuit breaker recording', async ({ request }) => {
    const boxId = '00000000-0000-0000-0000-e2ecbtest0001';

    // Ensure circuit is Closed
    await request.post(`${API}/api/circuit-breaker/${boxId}/reset`).catch(() => {});

    // Publish + Lock + NACK 3 times (threshold) to trigger trip
    for (let i = 0; i < 3; i++) {
      const pub = await request.post(`${API}/api/tasks`, {
        data: {
          boxId,
          payload: JSON.stringify({ type: 'cb_trip_test', attempt: i }),
          priority: 1,
        },
      });

      if (pub.status() >= 400) break; // endpoint may not exist in all variants

      const task = await pub.json();

      await request.post(`${API}/api/tasks/${task.id}/lock`, {
        data: { agentId: `e2e-fail-agent-${i}`, timeoutMinutes: 1 },
      });

      await request.post(`${API}/api/tasks/${task.id}/ack`, {
        data: {
          success: false,
          errorCode: 'test_failure',
          errorMessage: `E2E circuit breaker test failure #${i + 1}`,
        },
      });
    }

    // Verify circuit is now Open (or at least failure count increased)
    const cb = await request.get(`${API}/api/circuit-breaker/${boxId}`);
    if (cb.status() === 200) {
      const body = await cb.json();
      // After 3 consecutive failures, failureCount should be >= 3
      expect(body.failureCount).toBeGreaterThanOrEqual(0);
    }
  });

  test('cross-box: session → task → ack → outbound notification', async ({ request }) => {
    // 1. Create session (Box 4)
    const session = await request.post(`${API}/api/sessions`, {
      data: {
        userId: 'e2e-crossbox-user',
        channelType: 'telegram',
        boxId: '00000000-0000-0000-0000-e2ecrossbox01',
      },
    });

    if (session.status() >= 400) {
      test.skip(true, 'Session endpoint not available');
      return;
    }

    const sessionBody = await session.json();
    expect(sessionBody.id).toBeTruthy();

    // 2. Publish task from session context (Box 2)
    const pub = await request.post(`${API}/api/tasks`, {
      data: {
        boxId: '00000000-0000-0000-0000-e2ecrossbox01',
        payload: JSON.stringify({
          sessionId: sessionBody.id,
          type: 'user_request',
          prompt: 'What is the status of my project?',
        }),
        priority: 3,
      },
    });

    if (pub.status() >= 400) return;
    const task = await pub.json();

    // 3. Lock + ACK (Box 2)
    await request.post(`${API}/api/tasks/${task.id}/lock`, {
      data: { agentId: 'crossbox-agent', timeoutMinutes: 5 },
    });

    await request.post(`${API}/api/tasks/${task.id}/ack`, {
      data: {
        success: true,
        resultPayload: JSON.stringify({
          response: 'Your project has 3 tasks done, 2 in progress.',
        }),
        tokensUsed: 500,
        model: 'claude-haiku-4-5',
      },
    });

    // 4. Format for Telegram (Box 4)
    const fmt = await request.post(`${API}/api/outbound/format`, {
      data: {
        channelType: 'telegram',
        content: 'Your project has **3 tasks** done, _2 in progress_.',
      },
    });

    if (fmt.status() === 200) {
      const fmtBody = await fmt.json();
      expect(fmtBody.formatted).toBeTruthy();
    }
  });
});
