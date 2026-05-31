import asyncio
from mcp.client.session import ClientSession
from mcp.client.streamable_http import StreamableHTTPClientTransport

async def main():
    async with StreamableHTTPClientTransport("http://127.0.0.1:8488/mcp", headers={"X-Api-Key": "my-tenant-key-123"}) as transport:
        async with ClientSession(transport) as session:
            await session.initialize()
            
            res1 = await session.call_tool("test_tool_async", {})
            print(res1)
            
            res2 = await session.call_tool("test_tool_sync", {})
            print(res2)

asyncio.run(main())
