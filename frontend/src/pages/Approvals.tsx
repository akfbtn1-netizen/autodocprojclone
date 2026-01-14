// ═══════════════════════════════════════════════════════════════════════════
// Approvals Page - Prompt 2A
// Main approval workflow page using the enhanced ApprovalDashboard component
// ═══════════════════════════════════════════════════════════════════════════
// TODO [2A]: Wire SignalR real-time events to approval store
// TODO [2A]: Test E2E approval/reject flow with backend
// TODO [2A]: Implement 5-in-10min notification batching UI
// TODO [2A]: Add 24hr escalation reminder display

import { ApprovalDashboard } from '@/components/approval';

export function Approvals() {
  return <ApprovalDashboard />;
}

export default Approvals;
