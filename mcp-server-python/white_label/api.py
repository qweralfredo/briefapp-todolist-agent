"""
MCP White Label API Router.

Exposes HTTP endpoints for managing Box MCP instances:
- POST   /api/boxes/{boxId}/mcp/spawn   → Start MCP for a box
- DELETE /api/boxes/{boxId}/mcp/stop    → Stop MCP for a box
- GET    /api/boxes/{boxId}/mcp/status  → Get MCP instance status
- GET    /api/mcp-registry              → List all active instances
- GET    /api/mcp-registry/stats        → Registry statistics

This router is meant to be mounted on the main Briefapp API
or as a standalone management FastAPI app.
"""
from fastapi import APIRouter, HTTPException
from fastapi.responses import JSONResponse
from pydantic import BaseModel
from typing import Optional

from white_label.factory import mcp_factory, registry

router = APIRouter()


class SpawnRequest(BaseModel):
    box_name: str
    api_key: Optional[str] = None
    plugins: Optional[list[dict]] = None


@router.post("/boxes/{box_id}/mcp/spawn")
async def spawn_mcp(box_id: str, request: SpawnRequest):
    """Spawn a new MCP server instance for a Box."""
    instance = await mcp_factory.spawn(
        box_id=box_id,
        box_name=request.box_name,
        api_key=request.api_key,
        plugins=request.plugins,
    )
    return JSONResponse(
        status_code=201 if instance.status.value == "running" else 500,
        content={
            "status": instance.status.value,
            "instance": instance.to_dict(),
        },
    )


@router.delete("/boxes/{box_id}/mcp/stop")
async def stop_mcp(box_id: str):
    """Stop the MCP server instance for a Box."""
    success = await mcp_factory.stop(box_id)
    if not success:
        raise HTTPException(
            status_code=404,
            detail=f"No MCP instance found for box {box_id}",
        )
    return JSONResponse(content={"status": "stopped", "box_id": box_id})


@router.get("/boxes/{box_id}/mcp/status")
async def get_mcp_status(box_id: str):
    """Get the status of a Box MCP instance."""
    status = mcp_factory.get_status(box_id)
    if not status:
        raise HTTPException(
            status_code=404,
            detail=f"No MCP instance found for box {box_id}",
        )
    return JSONResponse(content=status)


@router.get("/mcp-registry")
async def list_instances():
    """List all active MCP instances."""
    return JSONResponse(content={"instances": registry.list_all()})


@router.get("/mcp-registry/stats")
async def registry_stats():
    """Get aggregate statistics for the MCP registry."""
    return JSONResponse(content=registry.get_stats())
