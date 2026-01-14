# Enterprise Documentation Platform - Frontend

Modern React 19 + Vite 6 frontend for the Enterprise Documentation Automation Platform.

## 🚀 Quick Start

```bash
# Navigate to frontend directory
cd frontend

# Install dependencies
npm install

# Start development server
npm run dev
```

The dev server will start at `http://localhost:5173`

## 📦 Tech Stack

- **React 19** - Latest React with automatic memoization
- **Vite 6** - Fast dev server and build tool
- **TypeScript** - Full type safety
- **Tailwind CSS** - Utility-first styling with custom theme
- **React Flow** - Workflow visualization
- **Framer Motion** - Smooth animations
- **Zustand** - Lightweight state management
- **React Query** - Server state management
- **SignalR** - Real-time updates

## 🎨 Design System

**Colors:**
- Warm stone neutrals (#fafaf9 to #0c0a09)
- Teal brand accent (#14b8a6)
- Status colors: emerald (success), amber (warning), red (danger)

**Typography:**
- Display: Outfit
- Body: Plus Jakarta Sans

## 📁 Project Structure

```
src/
├── main.tsx           # Entry point
├── App.tsx            # Root component with routing
├── types/             # TypeScript interfaces
├── lib/               # Utilities (cn, formatRelativeTime)
├── stores/            # Zustand global state
├── hooks/             # Custom hooks (useSignalR, useApprovalHub)
├── services/          # API layer (axios, document service)
├── components/
│   ├── ui/            # Base components (Button, Card, Badge, etc.)
│   ├── workflow/      # React Flow components
│   ├── dashboard/     # Dashboard widgets
│   └── layout/        # Header, Sidebar, MainLayout
├── pages/             # Route pages
│   ├── Dashboard.tsx
│   ├── Documents.tsx
│   ├── Approvals.tsx
│   └── Settings.tsx
└── styles/
    └── globals.css    # Tailwind + custom styles
```

## 🔧 Configuration

### API Proxy (vite.config.ts)
```typescript
proxy: {
  '/api': {
    target: 'https://localhost:7001',
    changeOrigin: true,
    secure: false,
  },
  '/hubs': {
    target: 'https://localhost:7001',
    ws: true,
    secure: false,
  },
}
```

### Environment Variables
Create `.env` for environment-specific config:
```env
VITE_API_BASE_URL=https://localhost:7001
```

## 📋 Pages

| Route | Page | Description |
|-------|------|-------------|
| `/` | Dashboard | KPIs, workflow canvas, pending approvals, recent documents |
| `/documents` | Documents | Document management with search, filters, grid/list views |
| `/approvals` | Approvals | Pending/completed approvals with priority indicators |
| `/settings` | Settings | Profile, notifications, appearance, security, integrations |

## 🔄 Backend Integration

### SignalR Events
- `StatusUpdated` - Approval status changes
- `NewDocument` - New document created
- `ApprovalRequested` - New approval request

### API Endpoints
- `GET /api/documents` - List documents
- `POST /api/documents` - Create document
- `POST /api/documents/{id}/approval` - Request approval
- `PUT /api/approvals/{id}/approve` - Approve document
- `PUT /api/approvals/{id}/reject` - Reject document

## 🏗️ Build for Production

```bash
npm run build
```

Output will be in `dist/` folder. Deploy to any static hosting.

## 🎯 Features

- ✅ Real-time approval workflow visualization
- ✅ Document status tracking with AI enhancement indicators
- ✅ Priority-based approval queue
- ✅ Dark/light/system theme support
- ✅ Responsive design (mobile/tablet/desktop)
- ✅ Agent status monitoring
- ✅ Activity timeline
- ✅ User settings management
- ✅ SharePoint/Jira/Azure AD integration status

## 📝 Development Notes

### Mock Data
Currently uses mock data for demonstration. Replace with React Query hooks connected to your API.

### Type Safety
All interfaces in `src/types/index.ts` match the backend DTOs:
- `Document` - Document entity
- `ApprovalRequest` - Approval workflow item
- `KpiData` - Dashboard metrics
- `WorkflowNode` - Workflow visualization

---

Built for Tennessee Farmers Insurance - Enterprise Documentation Automation Platform V2
