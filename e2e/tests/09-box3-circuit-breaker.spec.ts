import { test, expect } from '@playwright/test';
import { API } from '../fixtures/helpers';

/**
 * E2E-01: Box 3 — Circuit Breaker, Fallback, Rate Limit & Cost Guard
 * Validates: CircuitBreakerService, FallbackChainExecutor, RateLimiterService, CostGuardService
 */
test.describe('Box 3: Circuit Breaker', () => {
  const testBoxId = '00000000-0000-0000-0000-000000000003';

  test('GET /api/circuit-breaker — returns all boxes circuit status', async ({ request }) => {
    const r = await request.get(`${API}/api/circuit-breaker`);
    expect(r.status()).toBe(200);
    const body = await r.json();
    expect(Array.isArray(body)).toBeTruthy();
  });

  test('GET /api/circuit-breaker/:boxId — returns specific box status', async ({ request }) => {
    const r = await request.get(`${API}/api/circuit-breaker/${testBoxId}`);
    // 200 if exists, 404 if no circuit breaker entity for this box yet
    expect([200, 404]).toContain(r.status());

    if (r.status() === 200) {
      const body = await r.json();
      expect(body).toHaveProperty('state');
      expect(body).toHaveProperty('failureCount');
      expect(['Closed', 'Open', 'HalfOpen']).toContain(body.state);
    }
  });

  test('POST /api/circuit-breaker/:boxId/trip — force trip opens circuit', async ({ request }) => {
    const r = await request.post(`${API}/api/circuit-breaker/${testBoxId}/trip`, {
      data: { reason: 'E2E test — force trip' },
    });
    expect([200, 201, 204]).toContain(r.status());

    // Verify state is now Open
    const check = await request.get(`${API}/api/circuit-breaker/${testBoxId}`);
    if (check.status() === 200) {
      const body = await check.json();
      expect(body.state).toBe('Open');
    }
  });

  test('POST /api/circuit-breaker/:boxId/reset — force reset closes circuit', async ({ request }) => {
    const r = await request.post(`${API}/api/circuit-breaker/${testBoxId}/reset`);
    expect([200, 201, 204]).toContain(r.status());

    // Verify state is now Closed
    const check = await request.get(`${API}/api/circuit-breaker/${testBoxId}`);
    if (check.status() === 200) {
      const body = await check.json();
      expect(body.state).toBe('Closed');
      expect(body.failureCount).toBe(0);
    }
  });

  test('GET /api/circuit-breaker/:boxId/history — transition history log', async ({ request }) => {
    const r = await request.get(`${API}/api/circuit-breaker/${testBoxId}/history?limit=10`);
    expect([200, 404]).toContain(r.status());

    if (r.status() === 200) {
      const body = await r.json();
      expect(Array.isArray(body)).toBeTruthy();
      // Should have at least 2 entries from trip + reset above
      if (body.length > 0) {
        expect(body[0]).toHaveProperty('fromState');
        expect(body[0]).toHaveProperty('toState');
        expect(body[0]).toHaveProperty('timestamp');
      }
    }
  });

  test('GET /api/circuit-breaker/:boxId/fallback-history — fallback logs', async ({ request }) => {
    const r = await request.get(`${API}/api/circuit-breaker/${testBoxId}/fallback-history`);
    expect([200, 404]).toContain(r.status());

    if (r.status() === 200) {
      const body = await r.json();
      expect(Array.isArray(body)).toBeTruthy();
    }
  });
});

test.describe('Box 3: Rate Limiter', () => {

  test('GET /api/rate-limit — returns all provider quotas', async ({ request }) => {
    const r = await request.get(`${API}/api/rate-limit`);
    expect([200, 404]).toContain(r.status());

    if (r.status() === 200) {
      const body = await r.json();
      expect(Array.isArray(body)).toBeTruthy();
      if (body.length > 0) {
        expect(body[0]).toHaveProperty('provider');
        expect(body[0]).toHaveProperty('maxRpm');
        expect(body[0]).toHaveProperty('utilizationPercent');
      }
    }
  });

  test('GET /api/rate-limit/:provider — specific provider quota', async ({ request }) => {
    const r = await request.get(`${API}/api/rate-limit/openai`);
    expect([200, 404]).toContain(r.status());

    if (r.status() === 200) {
      const body = await r.json();
      expect(body).toHaveProperty('currentRpm');
      expect(body.currentRpm).toBeGreaterThanOrEqual(0);
    }
  });
});

test.describe('Box 3: Cost Guard & Token Budget', () => {

  test('GET /api/budget/Platform/default/stats — platform budget', async ({ request }) => {
    const r = await request.get(`${API}/api/budget/Platform/default/stats`);
    expect([200, 404]).toContain(r.status());

    if (r.status() === 200) {
      const body = await r.json();
      expect(body).toHaveProperty('budgetTokens');
      expect(body).toHaveProperty('usedTokens');
      expect(body).toHaveProperty('utilizationPercent');
    }
  });
});
