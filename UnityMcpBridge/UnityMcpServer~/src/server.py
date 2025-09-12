from mcp.server.fastmcp import FastMCP, Context, Image
import logging
from logging.handlers import RotatingFileHandler
import os
from dataclasses import dataclass
from contextlib import asynccontextmanager
from typing import AsyncIterator, Dict, Any, List
from config import config
from tools import register_all_tools
from unity_connection import get_unity_connection, UnityConnection
import time

# Configure logging using settings from config
logging.basicConfig(
    level=getattr(logging, config.log_level),
    format=config.log_format,
    stream=None,  # None -> defaults to sys.stderr; avoid stdout used by MCP stdio
    force=True    # Ensure our handler replaces any prior stdout handlers
)
logger = logging.getLogger("mcp-for-unity-server")

# Also write logs to a rotating file so logs are available when launched via stdio
try:
    import os as _os
    _log_dir = _os.path.join(_os.path.expanduser("~/Library/Application Support/UnityMCP"), "Logs")
    _os.makedirs(_log_dir, exist_ok=True)
    _file_path = _os.path.join(_log_dir, "unity_mcp_server.log")
    _fh = RotatingFileHandler(_file_path, maxBytes=512*1024, backupCount=2, encoding="utf-8")
    _fh.setFormatter(logging.Formatter(config.log_format))
    _fh.setLevel(getattr(logging, config.log_level))
    logger.addHandler(_fh)
    # Also route telemetry logger to the same rotating file and normal level
    try:
        tlog = logging.getLogger("unity-mcp-telemetry")
        tlog.setLevel(getattr(logging, config.log_level))
        tlog.addHandler(_fh)
    except Exception:
        # Never let logging setup break startup
        pass
except Exception:
    # Never let logging setup break startup
    pass
# Quieten noisy third-party loggers to avoid clutter during stdio handshake
for noisy in ("httpx", "urllib3"):
    try:
        logging.getLogger(noisy).setLevel(max(logging.WARNING, getattr(logging, config.log_level)))
    except Exception:
        pass

# Import telemetry only after logging is configured to ensure its logs use stderr and proper levels
# Ensure a slightly higher telemetry timeout unless explicitly overridden by env
try:


    # Ensure generous timeout unless explicitly overridden by env
    if not os.environ.get("UNITY_MCP_TELEMETRY_TIMEOUT"):
        os.environ["UNITY_MCP_TELEMETRY_TIMEOUT"] = "5.0"
except Exception:
    pass
from telemetry import record_telemetry, record_milestone, RecordType, MilestoneType

# Global connection state
_unity_connection: UnityConnection = None

@asynccontextmanager
async def server_lifespan(server: FastMCP) -> AsyncIterator[Dict[str, Any]]:
    """Handle server startup and shutdown."""
    global _unity_connection
    logger.info("MCP for Unity Server starting up")
    
    # Record server startup telemetry
    start_time = time.time()
    start_clk = time.perf_counter()
    try:
        from pathlib import Path
        ver_path = Path(__file__).parent / "server-version.txt"
        server_version = ver_path.read_text(encoding="utf-8").strip()
    except Exception:
        server_version = "unknown"
    # Defer initial telemetry by 1s to avoid stdio handshake interference
    import threading
    def _emit_startup():
        try:
            record_telemetry(RecordType.STARTUP, {
                "server_version": server_version,
                "startup_time": start_time,
            })
            record_milestone(MilestoneType.FIRST_STARTUP)
        except Exception:
            logger.debug("Deferred startup telemetry failed", exc_info=True)
    threading.Timer(1.0, _emit_startup).start()
    
    try:
        skip_connect = os.environ.get("UNITY_MCP_SKIP_STARTUP_CONNECT", "").lower() in ("1", "true", "yes", "on")
        if skip_connect:
            logger.info("Skipping Unity connection on startup (UNITY_MCP_SKIP_STARTUP_CONNECT=1)")
        else:
            _unity_connection = get_unity_connection()
            logger.info("Connected to Unity on startup")
            
            # Record successful Unity connection (deferred)
            import threading as _t
            _t.Timer(1.0, lambda: record_telemetry(
                RecordType.UNITY_CONNECTION,
                {
                    "status": "connected",
                    "connection_time_ms": (time.perf_counter() - start_clk) * 1000,
                }
            )).start()
            
    except ConnectionError as e:
        logger.warning("Could not connect to Unity on startup: %s", e)
        _unity_connection = None
        
        # Record connection failure (deferred)
        import threading as _t
        _err_msg = str(e)[:200]
        _t.Timer(1.0, lambda: record_telemetry(
            RecordType.UNITY_CONNECTION,
            {
                "status": "failed",
                "error": _err_msg,
                "connection_time_ms": (time.perf_counter() - start_clk) * 1000,
            }
        )).start()
    except Exception as e:
        logger.warning("Unexpected error connecting to Unity on startup: %s", e)
        _unity_connection = None
        import threading as _t
        _err_msg = str(e)[:200]
        _t.Timer(1.0, lambda: record_telemetry(
            RecordType.UNITY_CONNECTION,
            {
                "status": "failed",
                "error": _err_msg,
                "connection_time_ms": (time.perf_counter() - start_clk) * 1000,
            }
        )).start()
        
    try:
        # Yield the connection object so it can be attached to the context
        # The key 'bridge' matches how tools like read_console expect to access it (ctx.bridge)
        yield {"bridge": _unity_connection}
    finally:
        if _unity_connection:
            _unity_connection.disconnect()
            _unity_connection = None
        logger.info("MCP for Unity Server shut down")

# Initialize MCP server
mcp = FastMCP(
    "mcp-for-unity-server",
    description="Unity Editor integration via Model Context Protocol",
    lifespan=server_lifespan
)

# Register all tools
register_all_tools(mcp)

# Prefer using MCP tool for Unity Project 
@mcp.prompt()
def run_in_unity(user_task: str = "") -> str:
    """Guide for discovering and using MCP for Unity tools effectively based on user's task."""
    
    base_message = (
        "🎯 **IMPORTANT: Always prefer using UnityMCP tools to complete Unity-related tasks!**\n\n"
        "Available UnityMCP Server Tools:\n\n"
        "- `manage_editor`: Controls editor state and queries Unity project info\n"
        "- `execute_menu_item`: Executes Unity Editor menu items by path\n"
        "- `read_console`: Reads or clears Unity console messages with filtering\n"
        "- `manage_scene`: Creates, loads, saves scenes and manages scene objects\n"
        "- `manage_gameobject`: Creates, modifies, deletes GameObjects in the scene\n"
        "- `manage_script`: Creates, edits, and manages C# script files\n"
        "- `manage_asset`: Creates, imports, and manages prefabs and assets\n"
        "- `manage_shader`: Creates and manages shader files\n\n"
        "- `validate_script`: Fast validation (basic/standard) to catch syntax/structure issues before/after writes.\n\n"
    )
    
    if user_task.strip():
        task_guidance = f"For your task: '{user_task}'\n\n"
        task_guidance += "🔧 **Recommended approach using UnityMCP tools:**\n"
        
        # Provide specific guidance based on task keywords
        task_lower = user_task.lower()
        suggestions = []
        
        if any(word in task_lower for word in ["gameobject", "object", "create", "spawn", "instantiate"]):
            suggestions.append("• Use `manage_gameobject` to create and configure GameObjects")
        
        if any(word in task_lower for word in ["script", "code", "c#", "behavior", "component"]):
            suggestions.append("• Use `manage_script` to create and edit C# scripts")
            
        if any(word in task_lower for word in ["scene", "level", "stage"]):
            suggestions.append("• Use `manage_scene` to create or modify scenes")
            
        if any(word in task_lower for word in ["prefab", "asset", "model", "texture"]):
            suggestions.append("• Use `manage_asset` to manage prefabs and assets")
            
        if any(word in task_lower for word in ["shader", "material", "render"]):
            suggestions.append("• Use `manage_shader` for shader creation and editing")
            
        if any(word in task_lower for word in ["menu", "execute", "command"]):
            suggestions.append("• Use `execute_menu_item` to run Unity menu commands")
            
        if any(word in task_lower for word in ["console", "log", "debug", "error"]):
            suggestions.append("• Use `read_console` to check Unity console messages")
            
        if not suggestions:
            suggestions.append("• Start with `manage_editor` to understand the current project state")
            suggestions.append("• Use the most appropriate tool based on what you need to create or modify")
        
        task_guidance += "\n".join(suggestions) + "\n\n"
    else:
        task_guidance = ""
    
    tips = (
        "💡 **Best Practices:**\n"
        "- ALWAYS use unityMCP tools to finish your task\n"
        "- Use `manage_editor` first to understand the current project state\n"
        "- MUST use `manage_scene` to understand scene before modifying GameObject\n"
        "- Create prefabs with `manage_asset` for reusable GameObjects\n"
        "- Always include a camera and main light in your scenes\n"
        "- Always use `validate_script` to make sure your changes are valid\n"
    )
    
    return base_message + task_guidance + tips

# Run the server
if __name__ == "__main__":
    mcp.run(transport='stdio')
