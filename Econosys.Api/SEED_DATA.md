# Database Seed Data

The application automatically seeds the database with the following roles and users on startup.

## Roles
- Admin
- CEO
- Finance
- Sales
- Logistics
- User
- Moderator

## Seed Users

| Role | Name | Email | Password |
|------|------|-------|----------|
| Admin | System Administrator | admin@econosys.com | Admin@12345Secure! |
| CEO | Michael Jensen | michael.jensen@econosys.com | CEO@Secure2024! |
| Finance | Sarah Anderson | sarah.anderson@econosys.com | Finance@2024Eco! |
| Sales | James Mitchell | james.mitchell@econosys.com | Sales@Econosys24! |
| Logistics | Emma Williams | emma.williams@econosys.com | Logistics@2024! |

## How Seeding Works

The seeding logic is located in `Program.cs` in the database initialization section. When the application starts:

1. All pending Entity Framework Core migrations are applied
2. All roles are created if they don't already exist
3. Each seed user is created with their respective role if they don't already exist
4. The process logs successful user creations

## Testing the Accounts

You can test any of these accounts by:

1. Starting the application: `dotnet run`
2. Opening the Swagger UI at `https://localhost:5001`
3. Using the `/api/auth/login` endpoint with any email and password from the table above

Example login request:
```json
{
  "email": "michael.jensen@econosys.com",
  "password": "CEO@Secure2024!"
}
```

## Notes

- All users are created with `EmailConfirmed = true` (no email verification required)
- All users are created as `IsActive = true`
- The seeding only creates users/roles if they don't already exist, so it's safe to run multiple times
- Users are automatically assigned their respective roles during creation
