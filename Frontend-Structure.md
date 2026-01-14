# Frontend Project Structure

## Enterprise Documentation Platform V2 - React Frontend

### 📁 Root Directory Structure
```
frontend/
├── 📄 index.html                 # Main HTML entry point
├── 📄 package.json               # Dependencies and scripts
├── 📄 package-lock.json          # Lock file for dependencies
├── 📄 postcss.config.js          # PostCSS configuration
├── 📄 README.md                  # Project documentation
├── 📄 tailwind.config.js         # TailwindCSS configuration
├── 📄 tsconfig.json              # TypeScript configuration
├── 📄 vite.config.ts             # Vite build tool configuration
├── 📁 node_modules/              # Dependencies (auto-generated)
├── 📁 frontend-fix/              # Backup/fix directory
└── 📁 src/                       # Source code (detailed below)
```

---

## 🔧 Configuration Files

| File | Purpose |
|------|---------|
| `vite.config.ts` | Vite dev server and build configuration |
| `tsconfig.json` | TypeScript compiler settings |
| `tailwind.config.js` | TailwindCSS utility classes and theming |
| `postcss.config.js` | CSS processing pipeline |
| `package.json` | NPM scripts, dependencies, project metadata |

---

## 📁 Source Directory (`src/`)

### 🎯 Entry Points
```
src/
├── 📄 main.tsx                   # React app entry point
├── 📄 App.tsx                    # Main app component with routing
└── 📁 styles/                    # Global CSS and styling
```

### 📄 Core Pages (`src/pages/`)
```
pages/
├── 📄 index.ts                   # Page exports barrel file
├── 📄 Dashboard.tsx              # Main dashboard with KPIs and overview
├── 📄 Approvals.tsx              # Approval workflow management
├── 📄 Documents.tsx              # Document management interface
├── 📄 Pipeline.tsx               # Data processing pipeline view
└── 📄 Settings.tsx               # Application settings
```

### 🧩 Components (`src/components/`)

#### **UI Components** (`components/ui/`)
Reusable design system components:
```
ui/
├── 📄 index.ts                   # Component exports
├── 📄 Avatar.tsx                 # User avatar display
├── 📄 Badge.tsx                  # Status badges and labels
├── 📄 Button.tsx                 # Interactive buttons
├── 📄 Card.tsx                   # Content containers
├── 📄 Dropdown.tsx               # Select dropdowns
├── 📄 Input.tsx                  # Form inputs
└── 📄 Modal.tsx                  # Modal dialogs
```

#### **Dashboard Components** (`components/dashboard/`)
Dashboard-specific widgets:
```
dashboard/
├── 📄 index.ts                   # Dashboard exports
├── 📄 ApprovalQueue.tsx          # Pending approvals widget
├── 📄 DocumentList.tsx           # Recent documents list
└── 📄 KpiCard.tsx                # KPI metric cards
```

#### **Workflow Components** (`components/workflow/`)
Visual workflow management:
```
workflow/
├── 📄 index.ts                   # Workflow exports
├── 📄 WorkflowCanvas.tsx         # Interactive workflow diagram
└── 📄 WorkflowNode.tsx           # Individual workflow nodes
```

#### **Feature-Specific Components**
```
components/
├── 📁 agents/                    # AI agent management components
├── 📁 layout/                    # App layout and navigation
├── 📁 lineage/                   # Data lineage visualization
├── 📁 metadata/                  # Document metadata components
├── 📁 pipeline/                  # Data pipeline components
└── 📁 search/                    # Search interface components
```

### 🔧 Services (`src/services/`)
API integration and business logic:
```
services/
├── 📄 index.ts                   # Service exports
├── 📄 api.ts                     # Base API client with auth
├── 📄 agents.ts                  # AI agent operations
├── 📄 approvals.ts               # Approval workflow API
├── 📄 dashboard.ts               # Dashboard data fetching
├── 📄 documents.ts               # Document management API
├── 📄 lineage.ts                 # Data lineage tracking
└── 📄 pipeline.ts                # Pipeline management API
```

### 🪝 Hooks (`src/hooks/`)
Custom React hooks for reusable logic:
```
hooks/
├── 📄 index.ts                   # Hook exports
├── 📄 useFocusTrap.ts            # Accessibility focus management
├── 📄 usePipeline.ts             # Pipeline state management
├── 📄 useQueries.ts              # API query management
└── 📄 useSignalR.ts              # Real-time SignalR connection
```

### 🗄️ State Management (`src/stores/`)
Zustand state stores:
```
stores/
├── 📄 index.ts                   # Store exports and global state
└── Other store files...          # Feature-specific stores
```

### 🏷️ Types (`src/types/`)
TypeScript type definitions:
```
types/
├── 📄 index.ts                   # Type exports and definitions
└── Other type files...           # Feature-specific types
```

### 🛠️ Utilities (`src/lib/`)
Helper functions and utilities:
```
lib/
└── Various utility files...      # Common functions and helpers
```

---

## 🌐 Technology Stack

| Technology | Purpose |
|------------|---------|
| **React 18** | UI framework with hooks and modern patterns |
| **TypeScript** | Type safety and enhanced developer experience |
| **Vite** | Fast development server and build tool |
| **TailwindCSS** | Utility-first CSS framework |
| **Zustand** | Lightweight state management |
| **React Query** | Server state management and caching |
| **SignalR** | Real-time communication with backend |

---

## 🔄 Development Workflow

### **Start Development Server**
```bash
cd frontend
npm run dev
# Runs on http://localhost:5173
```

### **Build for Production**
```bash
npm run build
# Outputs to dist/ directory
```

### **Key NPM Scripts**
- `dev` - Start Vite development server
- `build` - Build for production
- `preview` - Preview production build
- `lint` - Run ESLint
- `type-check` - TypeScript compilation check

---

## 🎨 Architecture Patterns

### **Component Organization**
- **Atomic Design**: UI components follow atomic design principles
- **Feature-Based**: Components grouped by feature domain
- **Barrel Exports**: Each directory has index.ts for clean imports

### **State Management**
- **Local State**: React useState for component-specific state
- **Global State**: Zustand stores for app-wide state
- **Server State**: React Query for API data management
- **Real-time**: SignalR for live updates

### **API Integration**
- **Centralized Client**: Single API client with auth interceptors
- **Service Layer**: Feature-specific service files
- **Type Safety**: Full TypeScript coverage for API responses

---

## 🔗 Backend Integration

**API Base URL**: `http://localhost:5195`
**Authentication**: JWT Bearer tokens
**Real-time**: SignalR hub at `/approvalHub`

---

*Generated on January 7, 2026*