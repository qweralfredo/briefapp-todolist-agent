"""
MCP White Label Factory — Dynamic MCP Server Spawner per Box.

Each Box gets its own isolated MCP server instance with dedicated:
- Resources (context-rag, memory, logs)
- Tools (filtered by installed plugins)
- Endpoint (port or route-based isolation)
- Authentication (Box API key)

Architecture:
    BoxRegistry (tracks active instances)
      └── MCPFactory (spawns/stops instances)
           └── MCPTemplateEngine (generates server config)
"""
import os
import json
import signal
import asyncio
import logging
import subprocess
import uuid
from dataclasses import dataclass, field, asdict
from datetime import datetime, timezone
from enum import Enum
from typing import Optional, Any
from pathlib import Path

import httpx

logger = logging.getLogger(__name__)

API_BASE_URL = os.getenv("PANDORA_API_BASE_URL", "http://127.0.0.1:8480")
CONTEXT_API_BASE_URL = os.getenv("PANDORA_CONTEXT_API_BASE_URL", "http://127.0.0.1:8482/api/context")
TIMEOUT_SECONDS = float(os.getenv("PANDORA_API_TIMEOUT", "30"))
MCP_BASE_PORT = int(os.getenv("MCP_WL_BASE_PORT", "9000"))
MCP_TEMPLATES_DIR = os.getenv("MCP_WL_TEMPLATES_DIR", os.path.join(os.path.dirname(__file__), "templates"))


class InstanceStatus(str, Enum):
    STARTING = "starting"
    RUNNING = "running"
    STOPPED = "stopped"
    FAILED = "failed"
    UNHEALTHY = "unhealthy"


@dataclass
class MCPInstance:
    """Tracks a running MCP instance for a Box."""
    id: str
    box_id: str
    box_name: str
    port: int
    status: InstanceStatus = InstanceStatus.STARTING
    pid: Optional[int] = None
    endpoint: str = ""
    tools_count: int = 0
    resources_count: int = 0
    created_at: str = field(default_factory=lambda: datetime.now(timezone.utc).isoformat())
    last_health_check: Optional[str] = None
    health_check_failures: int = 0
    error: Optional[str] = None

    def to_dict(self) -> dict:
        return asdict(self)


class MCPTemplateEngine:
    """
    Generates MCP server configurations dynamically per Box.
    
    The template defines which tools, resources, and authentication
    are available for a specific Box's MCP server.
    """

    # Core tools available to every Box MCP
    CORE_TOOLS = [
        "context_box_ingest",
        "context_box_query",
        "context_box_list",
        "context_box_delete",
    ]

    # Core resources available to every Box MCP
    CORE_RESOURCES = [
        "briefapp://boxes/{box_id}/context-rag",
        "briefapp://boxes/{box_id}/memory",
        "briefapp://boxes/{box_id}/config",
    ]

    def generate_config(
        self,
        box_id: str,
        box_name: str,
        port: int,
        api_key: Optional[str] = None,
        installed_plugins: Optional[list[dict]] = None,
        custom_tools: Optional[list[str]] = None,
    ) -> dict:
        """
        Generate a complete MCP server configuration for a Box.
        
        Returns a config dict that can be used to start the box MCP server.
        """
        tools = list(self.CORE_TOOLS)
        resources = [r.format(box_id=box_id) for r in self.CORE_RESOURCES]

        # Add plugin-contributed tools
        if installed_plugins:
            for plugin in installed_plugins:
                plugin_tools = plugin.get("mcp_tools", [])
                tools.extend(plugin_tools)
                plugin_resources = plugin.get("mcp_resources", [])
                resources.extend(
                    r.format(box_id=box_id) for r in plugin_resources
                )

        # Add custom tools if any
        if custom_tools:
            tools.extend(custom_tools)

        config = {
            "server_name": f"briefapp-box-{box_id[:8]}",
            "box_id": box_id,
            "box_name": box_name,
            "host": "0.0.0.0",
            "port": port,
            "transport": "sse",
            "api_base_url": API_BASE_URL,
            "context_api_base_url": CONTEXT_API_BASE_URL,
            "api_key": api_key,
            "tools": tools,
            "resources": resources,
            "tools_count": len(tools),
            "resources_count": len(resources),
        }

        return config


class MCPRegistry:
    """
    Central registry tracking all active Box MCP instances.
    
    In production, this would be backed by PostgreSQL table:
    box_mcp_instances (box_id, port, pid, status, last_health_check, created_at)
    """

    def __init__(self):
        self._instances: dict[str, MCPInstance] = {}  # box_id -> instance
        self._port_pool: set[int] = set()  # allocated ports
        self._health_task: Optional[asyncio.Task] = None

    def register(self, instance: MCPInstance):
        """Register a new MCP instance."""
        self._instances[instance.box_id] = instance
        self._port_pool.add(instance.port)
        logger.info(f"Registered MCP instance for box {instance.box_id} on port {instance.port}")

    def unregister(self, box_id: str):
        """Remove an MCP instance from the registry."""
        instance = self._instances.pop(box_id, None)
        if instance:
            self._port_pool.discard(instance.port)
            logger.info(f"Unregistered MCP instance for box {box_id}")

    def get(self, box_id: str) -> Optional[MCPInstance]:
        return self._instances.get(box_id)

    def list_all(self) -> list[dict]:
        return [inst.to_dict() for inst in self._instances.values()]

    def allocate_port(self) -> int:
        """Find next available port starting from MCP_BASE_PORT."""
        port = MCP_BASE_PORT
        while port in self._port_pool:
            port += 1
        return port

    def get_stats(self) -> dict:
        instances = list(self._instances.values())
        return {
            "total_instances": len(instances),
            "running": sum(1 for i in instances if i.status == InstanceStatus.RUNNING),
            "stopped": sum(1 for i in instances if i.status == InstanceStatus.STOPPED),
            "failed": sum(1 for i in instances if i.status == InstanceStatus.FAILED),
            "unhealthy": sum(1 for i in instances if i.status == InstanceStatus.UNHEALTHY),
            "ports_allocated": sorted(self._port_pool),
        }

    async def start_health_checker(self, interval_seconds: int = 30):
        """Start background health checker for all active instances."""
        self._health_task = asyncio.create_task(
            self._health_loop(interval_seconds)
        )

    async def stop_health_checker(self):
        if self._health_task:
            self._health_task.cancel()
            try:
                await self._health_task
            except asyncio.CancelledError:
                pass

    async def _health_loop(self, interval: int):
        while True:
            await asyncio.sleep(interval)
            for box_id, instance in list(self._instances.items()):
                if instance.status in (InstanceStatus.RUNNING, InstanceStatus.UNHEALTHY):
                    healthy = await self._ping(instance)
                    instance.last_health_check = datetime.now(timezone.utc).isoformat()
                    if healthy:
                        instance.status = InstanceStatus.RUNNING
                        instance.health_check_failures = 0
                    else:
                        instance.health_check_failures += 1
                        if instance.health_check_failures >= 3:
                            instance.status = InstanceStatus.UNHEALTHY
                            logger.warning(
                                f"MCP instance for box {box_id} is UNHEALTHY "
                                f"({instance.health_check_failures} consecutive failures)"
                            )

    async def _ping(self, instance: MCPInstance) -> bool:
        """Ping an MCP instance to check if it's alive."""
        try:
            async with httpx.AsyncClient(timeout=5.0) as client:
                resp = await client.get(f"http://127.0.0.1:{instance.port}/sse")
                return resp.status_code < 500
        except Exception:
            return False


class MCPFactory:
    """
    Factory that spawns and manages MCP server processes per Box.
    
    Each Box gets an isolated FastMCP process running on a dedicated port.
    The factory manages the complete lifecycle: spawn → monitor → stop.
    """

    def __init__(self, registry: MCPRegistry, template_engine: MCPTemplateEngine):
        self.registry = registry
        self.template = template_engine
        self._processes: dict[str, subprocess.Popen] = {}  # box_id -> process

    async def spawn(
        self,
        box_id: str,
        box_name: str,
        api_key: Optional[str] = None,
        plugins: Optional[list[dict]] = None,
    ) -> MCPInstance:
        """
        Spawn a new MCP server for a Box.
        Returns the MCPInstance with port and status.
        """
        # Check if already running
        existing = self.registry.get(box_id)
        if existing and existing.status == InstanceStatus.RUNNING:
            return existing

        # Allocate port and generate config
        port = self.registry.allocate_port()
        config = self.template.generate_config(
            box_id=box_id,
            box_name=box_name,
            port=port,
            api_key=api_key,
            installed_plugins=plugins,
        )

        # Create instance record
        instance = MCPInstance(
            id=str(uuid.uuid4()),
            box_id=box_id,
            box_name=box_name,
            port=port,
            endpoint=f"http://127.0.0.1:{port}/sse",
            tools_count=config["tools_count"],
            resources_count=config["resources_count"],
        )

        # Write config to temp file for the subprocess
        config_dir = Path(MCP_TEMPLATES_DIR)
        config_dir.mkdir(parents=True, exist_ok=True)
        config_path = config_dir / f"box_{box_id[:8]}_config.json"
        config_path.write_text(json.dumps(config, indent=2))

        try:
            # Spawn the MCP server process
            server_script = os.path.join(
                os.path.dirname(os.path.dirname(__file__)),
                "server.py"
            )
            
            env = os.environ.copy()
            env.update({
                "PANDORA_API_BASE_URL": config["api_base_url"],
                "PANDORA_CONTEXT_API_BASE_URL": config["context_api_base_url"],
                "PANDORA_MCP_TRANSPORT": "sse",
                "PANDORA_MCP_HOST": "0.0.0.0",
                "PANDORA_MCP_PORT": str(port),
                "PANDORA_MCP_MOUNT_PATH": f"/box/{box_id[:8]}",
                "PANDORA_BOX_ID": box_id,
                "PANDORA_BOX_NAME": box_name,
            })
            
            if api_key:
                env["PANDORA_BOX_API_KEY"] = api_key

            process = subprocess.Popen(
                ["python", server_script],
                env=env,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )

            instance.pid = process.pid
            instance.status = InstanceStatus.RUNNING
            self._processes[box_id] = process
            self.registry.register(instance)

            logger.info(
                f"Spawned MCP instance for box '{box_name}' ({box_id}) "
                f"on port {port}, PID={process.pid}"
            )

        except Exception as e:
            instance.status = InstanceStatus.FAILED
            instance.error = str(e)
            self.registry.register(instance)
            logger.error(f"Failed to spawn MCP for box {box_id}: {e}")

        return instance

    async def stop(self, box_id: str) -> bool:
        """Stop the MCP server for a Box."""
        instance = self.registry.get(box_id)
        if not instance:
            return False

        process = self._processes.pop(box_id, None)
        if process and process.poll() is None:
            try:
                process.terminate()
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
            logger.info(f"Stopped MCP instance for box {box_id} (PID={instance.pid})")

        instance.status = InstanceStatus.STOPPED
        self.registry.unregister(box_id)
        
        # Clean up config file
        config_path = Path(MCP_TEMPLATES_DIR) / f"box_{box_id[:8]}_config.json"
        if config_path.exists():
            config_path.unlink()

        return True

    async def restart(self, box_id: str, **kwargs) -> Optional[MCPInstance]:
        """Restart a Box MCP server."""
        instance = self.registry.get(box_id)
        if instance:
            await self.stop(box_id)
        return await self.spawn(box_id, **kwargs)

    def get_status(self, box_id: str) -> Optional[dict]:
        """Get full status of a Box MCP instance."""
        instance = self.registry.get(box_id)
        if not instance:
            return None

        status = instance.to_dict()

        # Check if process is still alive
        process = self._processes.get(box_id)
        if process:
            poll = process.poll()
            if poll is not None:
                instance.status = InstanceStatus.FAILED
                instance.error = f"Process exited with code {poll}"
                status = instance.to_dict()

        return status


# ── Singleton Instances ──────────────────────────────────────
registry = MCPRegistry()
template_engine = MCPTemplateEngine()
mcp_factory = MCPFactory(registry, template_engine)
