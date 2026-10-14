# Econosys API

A secure .NET 9 Core REST API with JWT authentication, user management, and production-ready configuration.

## Features

- ✅ **JWT Authentication**: Secure token-based authentication
- ✅ **ASP.NET Core Identity**: User management with role-based authorization
- ✅ **Password Security**: Strong password hashing with bcrypt
- ✅ **Role-Based Authorization**: Admin, User, and Moderator roles
- ✅ **CORS Support**: Configurable cross-origin resource sharing
- ✅ **Input Validation**: Data validation using DataAnnotations
- ✅ **Swagger/OpenAPI**: Interactive API documentation
- ✅ **Entity Framework Core 9**: ORM with SQL Server support
- ✅ **Error Handling**: Comprehensive error handling and logging
- ✅ **Database Migrations**: Support for EF Core migrations

## Prerequisites

- .NET 9 SDK
- SQL Server (LocalDB or full installation)
- Visual Studio Code or Visual Studio 2022

## Getting Started

### 1. Restore Dependencies

```bash
dotnet restore
```

### 2. Update Database

```bash
dotnet ef database update
```

This creates the database and applies all migrations. Default admin user is created:
- **Email**: admin@econosys.com
- **Password**: Admin@12345 (⚠️ Change this on first login!)

### 3. Run the Application

```bash
dotnet run
```

The API will start at `https://localhost:5001` (or the configured port).

### 4. Access Swagger Documentation

Open your browser and navigate to: `https://localhost:5001`

## Calculation Implementation

The initial calculation milestone adds `GET /api/calculations/{id}/aggregate`
and the web detail route `/order/calculations/:id`. Both calculation-number
and product-name search links open the detail page. The page currently displays
saved values; editing, approvals, transactional saving, explicit price refresh,
price snapshots, document creation and calculation PDF generation remain planned.

The detail endpoint requires authentication and performs no database writes or
price-list refresh. Saved sales prices and markups remain unchanged. Archived
rows use their recorded exchange rates. Audit dates carry the Swedish offset,
including daylight-saving time. Computed display totals are returned by the API.

`CalculationPricingService` contains independently tested legacy freight,
price-tier, surcharge, sales-price and packaging primitives. It does not mutate
entities or apply calculation previews to sales rows.

Run the regression suite from the workspace root:

```bash
dotnet test Econosys.Api.Tests/Econosys.Api.Tests.csproj
```

Normal API startup retains its existing database initialization behavior. For
read-only verification against an already provisioned database, disable both
startup migrations and identity seeding:

```bash
ASPNETCORE_ENVIRONMENT=Development DatabaseInitialization__Enabled=false \
  dotnet run --project Econosys.Api/Econosys.Api.csproj --no-launch-profile \
  --urls https://localhost:5003
```

No schema migration is included in this initial milestone.

## API Endpoints

### Authentication

#### Register a New User
```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "firstName": "John",
  "lastName": "Doe"
}
```

**Response:**
```json
{
  "success": true,
  "message": "User registered successfully",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "user-id",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "createdAt": "2026-02-19T10:30:00Z",
    "roles": ["User"]
  }
}
```

#### Login
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePass123!"
}
```

#### Get Current User
```http
GET /api/auth/me
Authorization: Bearer <jwt-token>
```

#### Change Password
```http
POST /api/auth/change-password
Authorization: Bearer <jwt-token>
Content-Type: application/json

{
  "currentPassword": "OldPassword123!",
  "newPassword": "NewPassword456!"
}
```

#### Health Check
```http
GET /health
```

## Password Requirements

- Minimum 8 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one digit
- At least one special character (@$!%*?&)

## Configuration

### appsettings.json

Key settings to modify for production:

```json
{
  "Jwt": {
    "SecretKey": "your-very-long-and-secure-secret-key-min-32-chars",
    "Issuer": "EconosysAPI",
    "Audience": "EconosysClient",
    "ExpirationMinutes": 15
  },
  "ConnectionStrings": {
    "DefaultConnection": "your-connection-string-here"
  },
  "Cors": {
    "AllowedOrigins": "https://yourdomain.com;https://www.yourdomain.com"
  }
}
```

## Database Setup

The application uses SQL Server with Entity Framework Core. Connection string can be configured in `appsettings.json`.

### Create Migrations

```bash
dotnet ef migrations add MigrationName
```

### Update Database

```bash
dotnet ef database update
```

## Project Structure

```
EconosysAPI/
├── Controllers/          # API endpoints
├── Data/                 # Database context and models
├── DTOs/                 # Data transfer objects
├── Services/             # Business logic and authentication
├── Program.cs            # Application configuration
├── appsettings.json      # Configuration
└── EconosysAPI.csproj    # Project file
```

## Security Considerations

⚠️ **Production Checklist:**

- [ ] Change JWT SecretKey to a long, random value (32+ characters)
- [ ] Change default admin password immediately
- [ ] Configure CORS for specific origins only (not *)
- [ ] Use HTTPS in production
- [ ] Set strong password requirements
- [ ] Enable HTTPS redirection
- [ ] Use environment variables for sensitive configuration
- [ ] Implement rate limiting
- [ ] Add request logging and monitoring
- [ ] Regularly update NuGet packages
- [ ] Use strong database passwords
- [ ] Enable SQL Server encryption

## Troubleshooting

### Database Connection Issues

If you get connection errors:
1. Ensure SQL Server LocalDB is installed
2. Check connection string in appsettings.json
3. Try running: `sqllocaldb start mssqllocaldb`

### JWT Token Errors

- **"Invalid token"**: Token may be expired or malformed
- **"Token expired"**: Request a new token via login
- **"Unauthorized"**: Include the token in Authorization header: `Bearer <token>`

### Port Already in Use

If port 5001 is in use, you can change it in `Properties/launchSettings.json`

## Development Tips

- Use Swagger UI for API testing: `https://localhost:5001`
- Enable EF Core logging in appsettings.Development.json
- Use `dotnet watch run` for hot reload during development
- Migrations are applied automatically on startup

## Contributing

1. Create a feature branch
2. Make your changes
3. Test thoroughly
4. Submit a pull request

## License

This project is licensed under the MIT License.

## Support

For issues or questions, please open an issue on the project repository.
