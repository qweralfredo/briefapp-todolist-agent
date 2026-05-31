# Backup e Restore PostgreSQL

## Backup em disco local fora do Docker

Os backups sao gerados por script PowerShell no host (Windows), nao por container.

- `ops/postgres/backups`

Comando:

```powershell
powershell -ExecutionPolicy Bypass -File .\ops\scripts\backup-postgres.ps1
```

## Persistencia do banco

Dados do Postgres:
- `ops/postgres/data`

## Restore manual

Com stack subida, restaure um arquivo dump com:

```powershell
powershell -ExecutionPolicy Bypass -File .\ops\scripts\restore-postgres.ps1 -FilePath .\ops\postgres\backups\SEU_ARQUIVO.sql
```

## Verificacao

1. Suba a stack
   - `docker compose up -d`
2. Verifique API
   - `curl http://localhost:8480/health`
3. Acesse UI
   - `http://localhost:8400`
