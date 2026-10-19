# Econosys.Web

A modern React application built with Vite and Tailwind CSS for the Econosys platform. This application provides a login interface that integrates with the Econopack.Api backend.

## Features

- ⚡ **Vite** - Next generation frontend tooling
- ⚛️ **React 19** - Modern UI library
- 🎨 **Tailwind CSS** - Utility-first CSS framework
- 🔐 **Authentication** - Login page with Econopack.Api integration
- 🔄 **API Client** - Configured axios client with interceptors
- **Calculation product overview** - Read-only product information, including dimensions, weight, codes and notes, with a link to open the product in a new tab. Format shows available length, width and height separated by ` x `, falling back to the saved format text when no dimensions are present.
- **Collapsible order navigation** - Every order-tree node with children can be expanded or collapsed independently of its navigation link. Opening a record expands its ancestor path and its own children; other branches start collapsed. Customer orders without children remain leaf links.

## Order history

Saved supplier and customer orders share the `OrderHistory` component below the
order form. It displays the legacy date/type/status/destination, transport or
call-off, invoice, quantity, remaining production and stock columns. New drafts
do not request history. Saving an order reloads history; **Uppdatera historik**
refreshes it on demand and retries failed requests.

**Visa** opens read-only delivery or stocktaking details. Transport, call-off and
invoice references navigate to the corresponding records. Stocktaking details
also link to the full inventory count. Production has no detail action.
Customer orders without a stock warehouse omit the stock column, as in legacy.

History formatting/navigation checks can be run with:

```bash
node --test src/helpers/orderHistory.test.js
```

## Prerequisites

- Node.js (latest version)
- npm or yarn

## Installation

```bash
npm install
```

## Environment Variables

Create a `.env` file in the root directory with the following variables:

```env
VITE_API_URL=http://localhost:8080/api
```

See `.env.example` for reference.

## Development

Start the development server:

```bash
npm run dev
```

The application will be available at `http://localhost:5173` (or the next available port).

## Build

Build for production:

```bash
npm run build
```

## Preview

Preview the production build:

```bash
npm run preview
```

## Project Structure

```
src/
├── pages/
│   └── LoginPage.jsx      # Login page component
├── config/
│   └── apiClient.js       # Axios API client configuration
├── App.jsx                # Main app component
├── App.css                # App styles (using Tailwind)
├── index.css              # Global styles (Tailwind directives)
└── main.jsx               # Application entry point
```

## API Integration

The application uses an axios client configured in `src/config/apiClient.js` that:

- Automatically includes the authentication token in requests
- Handles 401 responses by clearing the token and redirecting to login
- Supports bearer token authentication

### Login Endpoint

The LoginPage component expects the following endpoint on your Econopack.Api:

**POST** `/auth/login`

**Request:**
```json
{
  "email": "user@example.com",
  "password": "password"
}
```

**Response:**
```json
{
  "token": "jwt-token-here",
  "user": {
    "id": "user-id",
    "email": "user@example.com",
    "name": "User Name"
  }
}
```

## Scripts

- `npm run dev` - Start development server
- `npm run build` - Build for production
- `npm run preview` - Preview production build
- `npm run lint` - Run ESLint

## Technologies

- **Frontend Framework**: React 19
- **Build Tool**: Vite 7
- **Styling**: Tailwind CSS 4
- **HTTP Client**: Axios
- **Linting**: ESLint

## License

MIT
