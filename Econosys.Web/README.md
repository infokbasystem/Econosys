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

## Header search

The header search finds calculations by their exact numeric ID and opens the
matching calculation. Valid IDs shorter than four digits search calculations
only; the existing four-character minimum remains in effect for other results.

Search checks: `node --test src/helpers/globalSearch.test.js`.

## Order searches

Inquiry, quotation, customer-order and supplier-order searches have separate
**Produkt**, **Leverantör** and **Kund** inputs alongside the general **Sök** input.
The three field-specific inputs use case-insensitive partial matches on the
displayed fields and combine with AND alongside the existing general search.
Blank inputs do not restrict results. Changes are debounced
by 350 ms and reset to page one; filtering applies before counting and pagination.
Customer-order and supplier-order searches also have a **Kunds onr** input for
the customer's order number, with the same matching behavior. They retain their
salesperson and creator filters.

Search-filter checks: `node --test src/helpers/orderSearchFilters.test.js`.

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

## Item tabs

Saved item detail pages use Web Locks to allow one editor per item across tabs
on the same origin and browser profile. A duplicate tab shows a warning instead
of the editor. Close or leave the original item, then reload the duplicate tab
to open it. Lists and new drafts are not locked.

Lock acquisition waits for the current tab's previous request to finish releasing,
including React Strict Mode effect cleanup/replay and immediate reopening.

Lock lifecycle checks: `node --test src/components/itemTabLock.test.js`.

## Transport order overview

Click a data-column header in the top planning table to sort ascending; click it
again to sort descending. Chevrons indicate the active sort. Sorting applies to
the filtered rows and is retained when navigating away and back. Report/planning
action columns are not sortable. Missing values stay last in either direction.
Delivery and confirmed-delivery times sort by date; week-mode values use the
Monday of their stored week and year, while still displaying the week label.

Sorting checks: `node --test src/pages/Logistics/transportOrderOverviewSorting.test.js`.

## Call-off freight orders

Adding the first delivery populates **Leveransadress** immediately with the
customer's addresses, including when opening a new call-off from a planned
delivery. The address can be selected before saving the call-off.

After a freight-order email is sent, the call-off is marked as sent to the
shipper and planned. Other unsaved edits are kept, and a save conflict or failure is
reported separately from the email. Emailing a call-off confirmation does not
change the status.

Call-offs and transport orders show **Mail skickat** when a sent-email log exists.
**Visa logg** displays the date, recipients, subject and sender from the legacy
log. Emails sent from these PDF previews are added to the same log.

Freight-order save checks: `node --test src/pages/Logistics/calloffSave.test.js`.

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

## Login and Demo backends

The login screen's **Login / Demo** selector chooses the backend for the entire
session. Both modes require an email and password:

- **Login** uses `VITE_API_URL`, or `https://localhost:5001/api` when unset.
- **Demo** uses `https://econosysapi.abloyd.se/api`. Use demo-account credentials;
  selecting Demo does not submit the form or automatically sign in.

The selected mode is remembered in local storage for reloads and newly opened
tabs, including links opened without an opener. Existing tabs keep their selected
backend so an active session is never silently moved to another API. Logout,
expired sessions (`401`), failed login attempts and temporary network errors do
not change the selected backend or trigger fallback requests. Select **Login**
explicitly to leave Demo. Demo rejects API requests targeting other servers,
including absolute URLs and per-request backend overrides (local blob downloads
are still supported). The application header
shows the active API address and identifies Demo. Email, file uploads/downloads,
and other API requests use the same backend with HttpOnly-cookie authentication.
Demo is not a read-only UI; the demo server controls permissions and data.

The remote API must allow the frontend's exact origin with credentialed CORS
and issue secure cookies suitable for cross-site requests (`SameSite=None;
Secure` where applicable). Browser third-party-cookie restrictions may prevent
cross-site login even with correct CORS. No browser-stored bearer-token fallback
is used.

Backend selection and request-isolation tests:

```bash
node --test src/config/backend.test.js src/config/apiClient.test.js
```

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

- Includes HttpOnly authentication cookies for the selected backend
- Handles expired sessions by redirecting to login without changing the selected backend
- Keeps the selected backend consistent across authentication and application requests

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
