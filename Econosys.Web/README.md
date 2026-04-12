# Econosys.Web

A modern React application built with Vite and Tailwind CSS for the Econosys platform. This application provides a login interface that integrates with the Econopack.Api backend.

## Features

- ⚡ **Vite** - Next generation frontend tooling
- ⚛️ **React 19** - Modern UI library
- 🎨 **Tailwind CSS** - Utility-first CSS framework
- 🔐 **Authentication** - Login page with Econopack.Api integration
- 🔄 **API Client** - Configured axios client with interceptors

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
