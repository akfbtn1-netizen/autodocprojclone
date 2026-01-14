# Enterprise UI Implementation TODO

## Project: Documentation Platform Frontend
**Stack:** Next.js 15 + shadcn/ui + Tailwind v4 + TanStack + Vercel AI SDK

---

## Phase 1: Foundation (Day 1-2)

### 1.1 Project Setup
- [ ] Create Next.js 15 project
  ```bash
  npx create-next-app@latest doc-platform-ui --typescript --tailwind --eslint --app
  cd doc-platform-ui
  ```
- [ ] Install core dependencies
  ```bash
  npm install @tanstack/react-query @tanstack/react-table
  npm install react-hook-form @hookform/resolvers zod
  npm install recharts lucide-react
  npm install ai @ai-sdk/react @ai-sdk/openai
  npm install next-themes class-variance-authority clsx tailwind-merge
  ```
- [ ] Initialize shadcn/ui
  ```bash
  npx shadcn@latest init
  npx shadcn@latest add button card input table dialog form select badge skeleton scroll-area avatar dropdown-menu command
  ```

### 1.2 Configure Tailwind v4
- [ ] Update `globals.css` with CSS-first configuration
- [ ] Define brand colors (match existing Word doc theme - blue headers?)
- [ ] Set up dark mode support with `next-themes`

### 1.3 Project Structure
- [ ] Create folder structure:
  ```
  src/
  ├── app/
  │   ├── (dashboard)/
  │   │   ├── layout.tsx
  │   │   ├── page.tsx
  │   │   ├── documents/
  │   │   ├── lineage/
  │   │   ├── schema/
  │   │   └── approvals/
  │   ├── api/
  │   │   └── chat/
  │   ├── layout.tsx
  │   └── providers.tsx
  ├── components/
  │   ├── ui/           # shadcn components
  │   ├── layouts/      # Sidebar, Header
  │   └── features/     # Domain components
  ├── lib/
  │   ├── api/          # API clients + query keys
  │   ├── hooks/        # Custom hooks
  │   ├── schemas/      # Zod schemas
  │   └── utils.ts      # cn() helper
  └── types/
  ```

### 1.4 Core Providers
- [ ] Create `providers.tsx` with QueryClientProvider + ThemeProvider
- [ ] Set up root layout with providers
- [ ] Configure TanStack Query defaults (staleTime, gcTime)

---

## Phase 2: Layout & Navigation (Day 2-3)

### 2.1 Dashboard Shell
- [ ] Create `DashboardLayout` component
  - Collapsible sidebar
  - Header with search + user menu
  - Main content area with ScrollArea
- [ ] Create `Sidebar` with navigation items:
  - Dashboard (home)
  - Documents
  - Data Lineage
  - Schema Browser
  - Approvals
  - Settings

### 2.2 Navigation Features
- [ ] Implement active route highlighting
- [ ] Add Command Palette (⌘K) for quick navigation
- [ ] Create breadcrumb component
- [ ] Add ThemeToggle to header

---

## Phase 3: API Integration Layer (Day 3-4)

### 3.1 Query Key Factory
- [ ] Create `lib/api/keys.ts` with structured query keys:
  - `documents.*`
  - `lineage.*`
  - `procedures.*`
  - `approvals.*`
  - `metadata.*`

### 3.2 API Client Functions
- [ ] Create typed fetch wrappers for .NET API:
  ```typescript
  // lib/api/documents.ts
  export async function fetchDocuments(filters?: DocumentFilters)
  export async function fetchDocument(id: string)
  export async function createDocument(data: CreateDocumentInput)
  export async function updateDocument(id: string, data: UpdateDocumentInput)
  ```
- [ ] Create similar for lineage, approvals, procedures

### 3.3 Custom Hooks
- [ ] `useDocuments(filters)` - list with filtering
- [ ] `useDocument(id)` - single document
- [ ] `useCreateDocument()` - mutation with invalidation
- [ ] `useUpdateDocument()` - optimistic update
- [ ] `useColumnLineage(schema, table, column)`
- [ ] `useProcedureLineage(schema, proc)`
- [ ] `useApprovals(status)`

---

## Phase 4: Documents Feature (Day 4-6)

### 4.1 Document List Page
- [ ] Create `/documents/page.tsx`
- [ ] Implement DataTable with TanStack Table:
  - Columns: Title, Schema, Table, Status, Updated, Actions
  - Sorting on all columns
  - Global search filter
  - Status filter (Draft, Review, Published)
  - Pagination

### 4.2 Document Detail Page
- [ ] Create `/documents/[id]/page.tsx`
- [ ] Show document metadata
- [ ] Display linked lineage information
- [ ] Show approval status/history
- [ ] Download Word document link

### 4.3 Document Forms
- [ ] Create Zod schemas for document validation
- [ ] Create `DocumentForm` component with React Hook Form
- [ ] Implement Create Document dialog
- [ ] Implement Edit Document page
- [ ] Add form for triggering regeneration

---

## Phase 5: Data Lineage Feature (Day 6-8)

### 5.1 Lineage Browser
- [ ] Create `/lineage/page.tsx`
- [ ] Schema/Table/Column selector (cascading dropdowns)
- [ ] Display column lineage results in table:
  - Procedure, Operation Type, Risk Score
  - Link to procedure documentation

### 5.2 Impact Analysis View
- [ ] Column impact analysis display
- [ ] Risk scoring visualization (Low/Medium/High/Critical badges)
- [ ] List of affected procedures
- [ ] Dynamic SQL warnings

### 5.3 Procedure Dependencies
- [ ] Create `/lineage/procedure/[schema]/[name]/page.tsx`
- [ ] Show procedure's read/write operations
- [ ] Visualize dependencies (consider simple tree or list first)

---

## Phase 6: Schema Browser (Day 8-9)

### 6.1 Schema Explorer
- [ ] Create `/schema/page.tsx`
- [ ] Tree view of schemas → tables → columns
- [ ] Table detail panel showing:
  - Column definitions
  - Indexes
  - Referencing procedures
  - Documentation status

### 6.2 Quick Actions
- [ ] "Generate Documentation" button per table
- [ ] Link to existing documents
- [ ] Show documentation coverage metrics

---

## Phase 7: Approvals Feature (Day 9-10)

### 7.1 Approval Queue
- [ ] Create `/approvals/page.tsx`
- [ ] Filter by status: Pending, Approved, Rejected
- [ ] DataTable with: Document, Submitter, Submitted Date, Actions
- [ ] Bulk approval actions

### 7.2 Approval Actions
- [ ] Approve/Reject buttons with confirmation dialog
- [ ] Comment input for rejection reason
- [ ] Approval history timeline on document detail

---

## Phase 8: AI Assistant (Day 10-12)

### 8.1 Chat API Route
- [ ] Create `/api/chat/route.ts`
- [ ] Configure Azure OpenAI provider
- [ ] Define system prompt for documentation context
- [ ] Implement tools:
  - `searchDocuments` - search by keyword
  - `getColumnLineage` - lineage lookup
  - `getProcedureInfo` - procedure details
  - `getTableSchema` - schema information

### 8.2 Chat Interface
- [ ] Create `ChatInterface` component
- [ ] Add to dashboard sidebar or floating panel
- [ ] Handle tool invocations in UI
- [ ] Show sources/citations from tool results

### 8.3 Contextual AI
- [ ] Pass current document/page context to chat
- [ ] "Explain this procedure" button
- [ ] "What tables does this affect?" quick action

---

## Phase 9: Dashboard & Analytics (Day 12-13)

### 9.1 Dashboard Home
- [ ] Stats cards:
  - Total Documents
  - Pending Approvals
  - Documentation Coverage %
  - Recent Activity
- [ ] Quick actions panel
- [ ] Recent documents list

### 9.2 Charts with Recharts
- [ ] Documentation created over time (line chart)
- [ ] Coverage by schema (bar chart)
- [ ] Approval status distribution (pie chart)

---

## Phase 10: Polish & Production (Day 13-15)

### 10.1 Loading States
- [ ] Add Suspense boundaries to all data-fetching pages
- [ ] Create skeleton components for tables, cards, charts
- [ ] Implement optimistic updates for mutations

### 10.2 Error Handling
- [ ] Create error.tsx for route segments
- [ ] Add toast notifications for actions (sonner)
- [ ] Implement retry logic in queries

### 10.3 Accessibility
- [ ] Audit keyboard navigation
- [ ] Add ARIA labels to icon buttons
- [ ] Test with screen reader
- [ ] Verify color contrast

### 10.4 Performance
- [ ] Implement query prefetching on hover
- [ ] Add loading indicators
- [ ] Review bundle size

---

## Configuration Files Needed

### Environment Variables
```env
# .env.local
NEXT_PUBLIC_API_URL=http://localhost:5000/api
AZURE_OPENAI_API_KEY=your-key
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com
AZURE_OPENAI_DEPLOYMENT=gpt-4o
```

### API Configuration
```typescript
// lib/api/config.ts
export const API_BASE = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

export async function apiFetch<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
  });
  if (!res.ok) throw new Error(`API Error: ${res.status}`);
  return res.json();
}
```

---

## Integration Points with .NET Backend

| Frontend Route | Backend Endpoint | Method |
|----------------|------------------|--------|
| `/documents` | `/api/documents` | GET |
| `/documents/[id]` | `/api/documents/{id}` | GET |
| Create document | `/api/documents` | POST |
| Update document | `/api/documents/{id}` | PUT |
| `/lineage?column=X` | `/api/lineage/column/{schema}/{table}/{column}` | GET |
| `/lineage/procedure/X/Y` | `/api/lineage/procedure/{schema}/{name}` | GET |
| `/approvals` | `/api/approvals` | GET |
| Approve/Reject | `/api/approvals/{id}` | PUT |
| Regenerate doc | `/api/documents/{id}/regenerate` | POST |

---

## Success Criteria

- [ ] Documents browsable, searchable, filterable
- [ ] Lineage data visible and navigable
- [ ] Approvals workflow functional
- [ ] AI assistant can answer questions about docs/schemas
- [ ] Dashboard shows key metrics
- [ ] Responsive design (works on tablet+)
- [ ] Dark mode works correctly
- [ ] No console errors in production build

---

## Future Enhancements (Post-MVP)

- [ ] Real-time updates with WebSocket/SSE
- [ ] Document diff viewer
- [ ] Lineage graph visualization (D3 or React Flow)
- [ ] Export to PDF
- [ ] Email notifications integration
- [ ] User preferences persistence
- [ ] Keyboard shortcuts throughout
- [ ] Full-text search with highlighting
