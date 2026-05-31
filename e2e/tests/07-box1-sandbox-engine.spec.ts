import { test, expect } from '@playwright/test';
import { API } from '../fixtures/helpers';

/**
 * E2E-01: Box 1 — Sandbox Engine Integration Tests
 * Validates: SandboxService, WorkspaceService, NetworkPolicy, MetricsCollector
 * Depends on: Sandbox:Enabled=true in appsettings
 */
test.describe('Box 1: Sandbox Engine', () => {

  test('GET /api/sandboxes — returns list (possibly empty)', async ({ request }) => {
    const r = await request.get(`${API}/api/sandboxes`);
    expect(r.status()).toBe(200);
    const body = await r.json();
    expect(Array.isArray(body)).toBeTruthy();
  });

  test('GET /health — API is healthy', async ({ request }) => {
    const r = await request.get(`${API}/health`);
    expect(r.status()).toBe(200);
    const body = await r.json();
    expect(body.status).toBe('ok');
  });

  test('POST /api/sandboxes — create sandbox returns valid entity', async ({ request }) => {
    const r = await request.post(`${API}/api/sandboxes`, {
      data: {
        boxId: '00000000-0000-0000-0000-000000000001',
        image: 'alpine:latest',
        command: 'echo hello',
        cpuLimit: 1.0,
        memoryLimitMb: 256,
        timeoutMinutes: 5,
        networkMode: 'Offline',
      },
    });

    // 201 Created or 200 OK depending on controller implementation
    expect([200, 201]).toContain(r.status());
    const body = await r.json();
    expect(body).toHaveProperty('id');
    expect(body).toHaveProperty('status');

    // Save for next tests
    process.env.E2E_SANDBOX_ID = body.id;
  });

  test('GET /api/sandboxes/:id/metrics — returns metrics structure', async ({ request }) => {
    const sandboxId = process.env.E2E_SANDBOX_ID;
    test.skip(!sandboxId, 'No sandbox created in previous test');

    const r = await request.get(`${API}/api/sandboxes/${sandboxId}/metrics`);
    // May return 200 with data or 404 if sandbox already terminated
    expect([200, 404]).toContain(r.status());

    if (r.status() === 200) {
      const body = await r.json();
      // Metrics should have standard shape
      expect(body).toHaveProperty('cpuPercent');
      expect(body).toHaveProperty('memoryUsedMb');
    }
  });

  test('GET /api/sandboxes/:id/network-policy — returns policy config', async ({ request }) => {
    const sandboxId = process.env.E2E_SANDBOX_ID;
    test.skip(!sandboxId, 'No sandbox created in previous test');

    const r = await request.get(`${API}/api/sandboxes/${sandboxId}/network-policy`);
    expect([200, 404]).toContain(r.status());

    if (r.status() === 200) {
      const body = await r.json();
      expect(body).toHaveProperty('networkMode');
    }
  });
});
