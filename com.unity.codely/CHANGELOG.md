# Changelog

All notable changes to Unity TCP Bridge will be documented in this file.

## [1.0.0] - 2024-12-19

### Major Refactoring
- 🔄 **Complete removal of MCP (Model Context Protocol) logic**
  - Removed all MCP-specific components, tools, and protocol handling
  - Eliminated MCP server integration and HTTP server components
  - Removed MCP client models, configuration systems, and UI windows
  
### New TCP-Focused Architecture
- 🚀 **Pure TCP Socket Implementation**
  - New `UnityTcpBridge` class for TCP server management
  - Basic echo server implementation as starting point
  - Async/await patterns for non-blocking operations
  - Multi-client connection support with proper resource management

### Core TCP Features
- **Port Management**
  - Automatic port discovery and allocation
  - Project-specific port persistence
  - Smart port conflict resolution
  - Cross-platform compatibility

- **Connection Handling**
  - TCP listener with automatic client acceptance
  - Configurable socket options (keep-alive, timeouts)
  - Graceful connection cleanup on shutdown
  - Unity lifecycle integration (assembly reload, editor quit)

### Updated Components
- **Renamed Assemblies**: `UnityTcp.*` → `UnityTcp.*`
- **Updated Namespaces**: All classes moved to `UnityTcp.Editor.*` namespace  
- **Simplified Helpers**: Kept only TCP-relevant utilities (PortManager, TcpLog)
- **Package Rebranding**: Updated from "Unity MCP" to "Unity TCP Bridge"

### Removed Components
- All MCP protocol handling and message processing
- MCP tool implementations (ManageScript, ManageAsset, etc.)
- MCP UI windows and editor integrations
- HTTP server and MCP server management
- Telemetry and MCP-specific logging
- Configuration builders and MCP client models

### Technical Details
- **Architecture**: Direct TCP socket server with customizable protocol handling
- **Performance**: Lightweight implementation focused on TCP networking
- **Compatibility**: Unity 2021.3+ with Newtonsoft.Json dependency
- **Protocol**: Basic TCP with welcome handshake (easily customizable)

### Migration Guide
This is a breaking change that removes all MCP functionality:

1. **Previous MCP Users**: This package no longer provides MCP integration
2. **TCP Socket Users**: Replace any `UnityTcpBridge` references with `UnityTcpBridge`
3. **Custom Protocols**: Implement your protocol logic in `HandleClientAsync` method
4. **Port Management**: Use `PortManager` for dynamic port allocation needs

### Development Notes
- Codebase reduced by ~80% by removing MCP complexity
- Focus shifted to providing a clean TCP socket foundation
- Easy to extend for custom networking protocols
- Maintains Unity Editor integration for automatic lifecycle management

## Previous Versions
Previous versions (1.x.x) included MCP (Model Context Protocol) integration which has been completely removed in this version.