
# ChatFileApp

## Requirements

- **Docker** installed
- **Visual Studio 2022** or later (recommended)
- **.NET 9.0 SDK**

## Getting Started

### 1. Start the Server

Navigate to the project root (where `docker-compose.yml` is located) and run:

```bash
docker-compose up
```

This sets up:
- ASP.NET Core Server (3 instances)
- PostgreSQL database
- Redis for caching
- NGINX load balancer

Wait until the database is ready (about 1-4 minutes). The server will display:

```
Database connection established.
```

### 2. Run the Client Application

- Open `ChatFileApp.sln` in Visual Studio.
- Set the `Client` project as the startup project.
- Press `F5` to build and launch the client.

## Database Initialization

The PostgreSQL database is automatically initialized on first startup using the provided schema (`server.sql`). Initial setup can take up to 1-2 minutes.

### Troubleshooting
- Ensure Docker Desktop is running.
- Ports 5000 (NGINX), 5432 (Postgres), and 6379 (Redis) must be available.
- If the server connection initially fails, wait 2-4 minutes and retry after Docker services fully initialize. Especially on first run.
