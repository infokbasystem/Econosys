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

## Customer and supplier order creation

`POST /api/customerorders` and `POST /api/supplierorders` set the required legacy
`CompanyId` on their respective orders from the authenticated user's resolved
legacy account, including repeat supplier orders. The company is assigned
server-side, not supplied by the client. These map existing database columns;
no database migration is needed.

Customer order create and update requests accept `deliveryDate` (nullable) and
`deliveryDateWeekMode` (boolean). Both are returned in the order details. The web
order's **Orderinfo** column uses these fields for **Leveransdatum**: select a day
for an exact date or a week number for week mode; clearing the picker clears the
date and resets week mode.

The same requests accept nullable `logisticsInfoInternal` and `invoicingInfo`
strings, shown as multiline **Logistikinfo** and **Faktureringsinfo** fields below
the delivery date in **Orderinfo**. Clearing either field saves `null`.

**Slutlevererad** in **Orderinfo** edits `completed` and the nullable
`completedDate` through these same requests. As in the legacy order form, the
checkbox and exact date are independently editable; toggling the status does not
automatically assign or clear the date. The completion date does not use week mode.

Customer order responses include the selected unit's nullable `unitMultiplicator`.
**Upplaga och pris** displays it as `x {unitMultiplicator}` instead of the unit name.

**Producerat och inköpspris** is a read-only summary of the linked supplier order's
produced edition, unit multiplicator, purchase price, purchase currency and saved
purchase exchange rate. Customer order responses expose these as
`supplierOrderProducedEdition`, `supplierOrderUnitMultiplicator`,
`supplierOrderPurchasePrice`, `supplierOrderPurchaseCurrencyId`,
`supplierOrderPurchaseCurrencyName` and `supplierOrderPurchaseCurrencyRate`.
Missing supplier values remain blank rather than falling back to sales values.

## Invoice drafts from deliveries and order costs

`POST /api/invoices/draft/from-selection` assigns accounts using the legacy rules:
customer receivables and earnings overrides take precedence; otherwise receivables
use the account configured for invoice payment method `3`, delivery earnings use
price type `1`, and order costs use their cost definition's account. Earnings use
the domestic, EU, or export account according to the customer (EU takes precedence).
VAT and rounding always use the domestic accounts configured for price types `13`
and `12`, independently of customer overrides. These accounts are also copied to
the draft's accounting rows and retained when saved. Missing account configuration
is logged as a warning and leaves the affected account blank for manual completion.

Invoice creation and updates assign `CompanyId` on both invoice rows and accounting
rows from the authenticated user's resolved legacy account. The accounting row
mapping includes the existing required `InvoiceAccountRow.CompanyId` column;
no database migration is needed.

## Order history

The authenticated `GET /api/orderhistory/supplierorders/{id}` and
`GET /api/orderhistory/customerorders/{id}` endpoints return the legacy order
history with production, deliveries, stocktakes, transport/call-off references
and delivery-linked invoice numbers. Missing orders return `404`; existing
orders with no events return an empty `rows` array.

Supplier orders include all their deliveries. Customer orders include their own
outbound/direct deliveries plus the linked supplier order's inbound deliveries
and stocktakes. Customer credit invoices are separate events and do not affect
balances. Header and invoice-row delivery references are both supported, without
confusing identical IDs from different delivery tables.

Invoice loading first selects only rows referencing the visible deliveries,
then fetches lightweight invoice headers. Keep these lookups separate: a
correlated invoice-row search combined with a split collection include repeats
the expensive invoice selection. No history cache is used, so refreshes reflect
current delivery and invoice data.

As in the legacy application, planned deliveries affect running balances:
inbound deliveries increase stock and reduce remaining production, outbound
deliveries reduce stock, direct deliveries reduce remaining production, and
stocktakes replace the stock balance. Dates include the Swedish UTC offset.
Customer history hides the stock column for non-stock warehouses.
See [OrderHistory.http](./OrderHistory.http) for sample requests.

## Calculation Implementation

### New calculations from products

The product page's **Ny kalkyl** action opens
`/order/calculations/new?productId={productId}`. The authenticated
`GET /api/calculations/from-product/{productId}/draft` endpoint returns product
information and legacy calculation defaults without inserting a calculation.
Drafts have ID `0` and no creation or edit timestamps.

Only clicking **Spara** sends
`POST /api/calculations/from-product/{productId}` with the calculation update
payload. The header and new rows are saved together, using the authenticated
legacy user's company. The response is `201 Created`, and the web page replaces
the draft URL with the saved calculation's URL. Discarding a draft creates no
calculation. Document creation remains disabled until the first save.

Draft freight previews use `POST /api/calculations/draft/freight-refresh`;
this does not persist the draft.

### Existing calculation details

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
