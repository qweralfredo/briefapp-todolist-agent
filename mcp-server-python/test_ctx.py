import os
import contextvars
from mcp.server.fastmcp import FastMCP, Context
import uvicorn
from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
import asyncio
import anyio

mcp = FastMCP("test")
tenant_api_key = contextvars.ContextVar("tenant_api_key", default=None)

class TenantMiddleware(BaseHTTPMiddleware):
    async def dispatch(self, request: Request, call_next):
        api_key = request.headers.get("x-api-key")
        if api_key:
            tenant_api_key.set(api_key)
        return await call_next(request)

@mcp.tool()
async def test_tool_async(ctx: Context) -> str:
    return f"async key: {tenant_api_key.get()}"

@mcp.tool()
def test_tool_sync() -> str:
    return f"sync key: {tenant_api_key.get()}"

if __name__ == "__main__":
    app = mcp.streamable_http_app()
    app.add_middleware(TenantMiddleware)
    uvicorn.run(app, host="127.0.0.1", port=8488)
