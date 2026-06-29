# Aspire Troubleshooting Guide

## Problem: PostgreSQL Container Stuck at "Starting"

### Symptoms
- Aspire dashboard shows the PostgreSQL container status as "Starting" indefinitely
- Container appears as "Created" in Docker Desktop but never starts
- Error message: `Could not create the network as all available subnet ranges from the default pool are allocated`

### Root Cause
Docker has a limited pool of subnet ranges (~30 networks by default). When Aspire doesn't shut down cleanly, it leaves behind orphaned session networks. Eventually, all subnet ranges are exhausted and new containers can't start.

### Solution

#### 1. Clean up orphaned Aspire networks
```powershell
# View all networks (look for many aspire-session-network-* entries)
docker network ls

# Remove all unused networks (quick fix)
docker network prune -f

# Or remove only Aspire networks specifically
docker network ls --filter "name=aspire-session" -q | ForEach-Object { docker network rm $_ }
docker network ls --filter "name=aspire-persistent" -q | ForEach-Object { docker network rm $_ }
```

#### 2. If networks won't delete (has active endpoints)
```powershell
# Stop and remove all containers first
docker ps -a -q | ForEach-Object { docker rm -f $_ }

# Then remove the networks
docker network prune -f
```

#### 3. Kill orphaned DCP processes
```powershell
# Check for orphaned DCP processes
tasklist | findstr dcp

# Kill them
Stop-Process -Name dcp -Force -ErrorAction SilentlyContinue
```

#### 4. Full cleanup script
```powershell
# Complete Aspire cleanup
Stop-Process -Name dcp -Force -ErrorAction SilentlyContinue
docker ps -a -q | ForEach-Object { docker rm -f $_ }
docker network prune -f
docker volume prune -f  # Optional: removes unused volumes too
```

### Prevention

1. **Always stop Aspire cleanly** - Use Ctrl+C or Stop Debugging in Visual Studio
2. **Don't force-kill Visual Studio** while Aspire is running
3. **Periodic cleanup** - Run `docker network prune -f` occasionally

### Other Common Issues

#### Port Already in Use
If you see port binding errors:
```powershell
# Check what's using a port (e.g., 5432)
netstat -ano | findstr ":5432"

# Find the process
tasklist /FI "PID eq <PID>"

# Kill it if it's an orphaned dcp.exe
Stop-Process -Id <PID> -Force
```

#### Avoid Fixed Host Ports
Don't use `.WithHostPort()` in AppHost.cs unless absolutely necessary - it can cause port conflicts:
```csharp
// Avoid this (can cause conflicts):
var postgres = builder.AddPostgres("pgsql")
	.WithDataVolume(databaseName)
	.WithHostPort(5432);  // ❌

// Better (let Aspire assign ports dynamically):
var postgres = builder.AddPostgres("pgsql")
	.WithDataVolume(databaseName);  // ✅
```

### Enable Debug Logging
To diagnose Aspire/DCP issues, enable debug logging in `appsettings.json`:
```json
{
  "Logging": {
	"LogLevel": {
	  "Default": "Information",
	  "Aspire.Hosting.Dcp": "Debug",
	  "Aspire.Hosting": "Debug"
	}
  }
}
```
