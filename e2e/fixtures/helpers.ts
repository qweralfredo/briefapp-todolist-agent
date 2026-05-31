import { APIRequestContext, expect } from '@playwright/test';

export const API = process.env.PANDORA_API_URL ?? 'http://localhost:8480';
export const API_KEY = process.env.PANDORA_API_KEY ?? 'dev-api-key';
export const GRAFANA = process.env.GRAFANA_URL ?? 'http://localhost:3002';
export const DEVLAKE = process.env.DEVLAKE_URL ?? 'http://localhost:8082';
export const FRONTEND = process.env.FRONTEND_URL ?? 'http://localhost:8400';

export const GF_AUTH = Buffer.from('admin:briefapp-grafana').toString('base64');

export async function grafanaDashboard(api: APIRequestContext, uid: string) {
  const r = await api.get(`${GRAFANA}/api/dashboards/uid/${uid}`, {
    headers: { Authorization: `Basic ${GF_AUTH}` },
  });
  expect(r.status()).toBe(200);
  return r.json();
}

export async function grafanaQuery(
  api: APIRequestContext,
  rawSql: string,
): Promise<{ rows: unknown[][] }> {
  // Get datasource uid
  const ds = await api.get(`${GRAFANA}/api/datasources`, {
    headers: { Authorization: `Basic ${GF_AUTH}` },
  });
  const datasources = await ds.json();
  const mysql = datasources.find((d: { type: string }) => d.type === 'mysql');
  if (!mysql) throw new Error('MySQL datasource not found in Grafana');

  const r = await api.post(`${GRAFANA}/api/ds/query`, {
    headers: {
      Authorization: `Basic ${GF_AUTH}`,
      'Content-Type': 'application/json',
    },
    data: {
      queries: [
        {
          refId: 'A',
          datasource: { uid: mysql.uid, type: 'mysql' },
          rawSql,
          format: 'table',
        },
      ],
      from: 'now-30d',
      to: 'now',
    },
  });
  const body = await r.json();
  const frames = body?.results?.A?.frames ?? [];
  const rows = frames[0]?.data?.values ?? [];
  return { rows };
}
