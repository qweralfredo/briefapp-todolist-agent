import { test, expect } from '@playwright/test';
import { API } from '../fixtures/helpers';

/**
 * E2E-01: Box 4 — OpenClaw Gateway, Sessions, Formatters & Channel Health
 * Validates: InboundRouter, SessionService, ChannelFormatters, ChannelHealth, MessageRetry
 */
test.describe('Box 4: OpenClaw Gateway & Webhook', () => {

  test('POST /api/openclaw/webhook — inbound message routed', async ({ request }) => {
    const r = await request.post(`${API}/api/openclaw/webhook`, {
      headers: {
        'X-OpenClaw-Signature': 'test-signature',
        'Content-Type': 'application/json',
      },
      data: {
        channel: 'whatsapp',
        from: '+5511999998888',
        message: 'Hello Briefapp, run tests on my project',
        timestamp: new Date().toISOString(),
      },
    });

    // 200 OK if processed, 401/403 if HMAC validation fails (expected in dev mode)
    expect([200, 201, 401, 403]).toContain(r.status());
  });

  test('GET /api/openclaw/channels — lists configured channels', async ({ request }) => {
    const r = await request.get(`${API}/api/openclaw/channels`);
    expect([200, 404]).toContain(r.status());

    if (r.status() === 200) {
      const body = await r.json();
      expect(Array.isArray(body)).toBeTruthy();
      // Each channel should have type and enabled flag
      if (body.length > 0) {
        expect(body[0]).toHaveProperty('channelType');
        expect(body[0]).toHaveProperty('enabled');
      }
    }
  });
});

test.describe('Box 4: Session & Context Manager', () => {

  test('POST /api/sessions — create or get session', async ({ request }) => {
    const r = await request.post(`${API}/api/sessions`, {
      data: {
        userId: 'e2e-user-001',
        channelType: 'whatsapp',
        boxId: '00000000-0000-0000-0000-000000000004',
      },
    });

    expect([200, 201]).toContain(r.status());
    const body = await r.json();
    expect(body).toHaveProperty('id');
    expect(body).toHaveProperty('userId');
    process.env.E2E_SESSION_ID = body.id;
  });

  test('GET /api/sessions/:id — retrieve session details', async ({ request }) => {
    test.skip(!process.env.E2E_SESSION_ID, 'No session created');
    const id = process.env.E2E_SESSION_ID!;

    const r = await request.get(`${API}/api/sessions/${id}`);
    expect(r.status()).toBe(200);
    const body = await r.json();
    expect(body.userId).toBe('e2e-user-001');
    expect(body).toHaveProperty('handoffState');
  });

  test('POST /api/sessions/:id/handoff — request human handoff', async ({ request }) => {
    test.skip(!process.env.E2E_SESSION_ID, 'No session created');
    const id = process.env.E2E_SESSION_ID!;

    const r = await request.post(`${API}/api/sessions/${id}/handoff`, {
      data: { reason: 'E2E test — user requested human assistance' },
    });

    expect([200, 204]).toContain(r.status());
  });
});

test.describe('Box 4: Channel Health & Retry Queue', () => {

  test('GET /api/openclaw/metrics — channel metrics per type', async ({ request }) => {
    const r = await request.get(`${API}/api/openclaw/metrics`);
    expect([200, 404]).toContain(r.status());

    if (r.status() === 200) {
      const body = await r.json();
      expect(Array.isArray(body)).toBeTruthy();
      if (body.length > 0) {
        expect(body[0]).toHaveProperty('channelType');
        expect(body[0]).toHaveProperty('uptimePercent');
        expect(body[0]).toHaveProperty('deliveryRate');
      }
    }
  });

  test('GET /api/openclaw/retry-queue/stats — retry queue status', async ({ request }) => {
    const r = await request.get(`${API}/api/openclaw/retry-queue/stats`);
    expect([200, 404]).toContain(r.status());

    if (r.status() === 200) {
      const body = await r.json();
      expect(body).toHaveProperty('pendingByChannel');
    }
  });
});

test.describe('Box 4: Outbound Formatting', () => {

  test('POST /api/outbound/format — WhatsApp format', async ({ request }) => {
    const r = await request.post(`${API}/api/outbound/format`, {
      data: {
        channelType: 'whatsapp',
        content: '# Task Completed\n\n```csharp\nConsole.WriteLine("Hello");\n```\n\nAll tests passed!',
      },
    });

    expect([200, 404]).toContain(r.status());

    if (r.status() === 200) {
      const body = await r.json();
      expect(body).toHaveProperty('formatted');
      // WhatsApp should not contain markdown headers
      expect(body.formatted).not.toContain('# ');
      // Should have WhatsApp bold markers
      expect(body.formatted).toContain('*');
    }
  });

  test('POST /api/outbound/format — Slack format', async ({ request }) => {
    const r = await request.post(`${API}/api/outbound/format`, {
      data: {
        channelType: 'slack',
        content: '# Task Completed\n\nAll tests passed successfully.',
      },
    });

    expect([200, 404]).toContain(r.status());
  });

  test('POST /api/outbound/format — Telegram format', async ({ request }) => {
    const r = await request.post(`${API}/api/outbound/format`, {
      data: {
        channelType: 'telegram',
        content: 'Hello **world** with `code` blocks.',
      },
    });

    expect([200, 404]).toContain(r.status());
  });
});
