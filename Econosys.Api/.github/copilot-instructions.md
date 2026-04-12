# .NET 9 Core API Project Setup

## Project Overview
A secure .NET 9 Core REST API with JWT authentication, user management, and production-ready configuration.

## Setup Checklist

- [x] Verify project structure created
- [x] Scaffold the Project - Using dotnet CLI
- [x] Customize with Authentication - JWT, Identity, Endpoints
- [x] Install Dependencies
- [x] Compile the Project
- [ ] Create and Run Task
- [ ] Ensure Documentation is Complete

## Development Notes
- JWT-based authentication
- ASP.NET Core Identity integration
- Secure password hashing
- Role-based authorization
- CORS configured
- Input validation and error handling
- Production-ready configuration

## How to Run
```bash
dotnet restore
dotnet build
dotnet run
```

## API Endpoints
- POST /api/auth/register - Register new user
- POST /api/auth/login - Login and get JWT token
- GET /api/auth/me - Get current user (requires auth)
- GET /api/users - Get all users (admin only)
- POST /api/auth/change-password - Change password (requires auth)
- GET /health - Health check endpoint
