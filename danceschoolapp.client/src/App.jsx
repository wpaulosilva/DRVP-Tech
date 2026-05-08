import { Routes, Route } from 'react-router-dom'
import AppLayout from './layouts/AppLayout'
import ProtectedRoute from './layouts/ProtectedRoute'
import DashboardLayout from './layouts/DashboardLayout'

import HomePage from './pages/public/HomePage'
import LoginPage from './pages/public/LoginPage'
import UnauthorizedPage from './pages/public/UnauthorizedPage'
import ForgotPasswordPage from './pages/public/ForgotPasswordPage'
import ResetPasswordPage from './pages/public/ResetPasswordPage'

// Admin
import AdminDashboardPage from './pages/admin/AdminDashboardPage'
import AdminUsersPage from './pages/admin/AdminUsersPage'

// Staff
import StaffDashboardPage from './pages/staff/StaffDashboardPage'
import StaffUsersPage from './pages/staff/StaffUsersPage'
import DirectionUsersPage from './pages/staff/DirectionUsersPage'
import StaffValidateStudentsPage from './pages/staff/StaffValidateStudentsPage'
import StaffValidateClassesPage from './pages/staff/StaffValidateClassesPage'
import StaffModalitiesPage from './pages/staff/StaffModalitiesPage'
import StaffStudiosPage from './pages/staff/StaffStudiosPage'
import StaffEventsPage from './pages/staff/StaffEventsPage'
import StaffInventoryPage from './pages/staff/StaffInventoryPage'
import StaffItemDetailPage from './pages/staff/StaffItemDetailPage'
import StaffAgendaPage from './pages/staff/StaffAgendaPage'
import StaffBillingPage from './pages/staff/StaffBillingPage'
import StaffBlockedPeriodsPage from './pages/staff/StaffBlockedPeriodsPage'

// Coach
import CoachDashboardPage from './pages/coach/CoachDashboardPage'
import CoachAvailabilityPage from './pages/coach/CoachAvailabilityPage'
import CoachValidateClassesPage from './pages/coach/CoachValidateClassesPage'
import CoachAgendaPage from './pages/coach/CoachAgendaPage'
import CoachEventsPage from './pages/coach/CoachEventsPage'

// Parent
import ParentDashboardPage from './pages/parent/ParentDashboardPage'
import ParentClassesPage from './pages/parent/ParentClassesPage'
import ParentStudentsPage from './pages/parent/ParentStudentsPage'
import ParentInventoryPage from './pages/parent/ParentInventoryPage'
import ParentItemDetailPage from './pages/parent/ParentItemDetailPage'
import ParentEventsPage from './pages/parent/ParentEventsPage'

function App() {
    return (
        <Routes>
            {/* PUBLIC */}
            <Route element={<AppLayout />}>
                <Route path="/" element={<HomePage />} />
            </Route>

            <Route element={<AppLayout simple />}>
                <Route path="/login" element={<LoginPage />} />
                <Route path="/reset-password" element={<ResetPasswordPage />} />
                <Route path="/forgot-password" element={<ForgotPasswordPage />} />
                <Route path="/unauthorized" element={<UnauthorizedPage />} />
            </Route>

            {/* ADMIN */}
            <Route
                element={
                    <ProtectedRoute allowedRoles={['admin']}>
                        <DashboardLayout role="admin" />
                    </ProtectedRoute>
                }
            >
                <Route path="/admin" element={<AdminDashboardPage />} />
                <Route path="/admin/utilizadores" element={<AdminUsersPage />} />
            </Route>

            {/* STAFF */}
            <Route
                element={
                    <ProtectedRoute allowedRoles={['staff', 'admin']}>
                        <DashboardLayout role="staff" />
                    </ProtectedRoute>
                }
            >
                <Route path="/staff" element={<StaffDashboardPage />} />
                <Route path="/staff/utilizadores" element={<StaffUsersPage />} />
                <Route path="/staff/direcao" element={<DirectionUsersPage />} />
                <Route path="/staff/validar-estudantes" element={<StaffValidateStudentsPage />} />
                <Route path="/staff/validar-aulas" element={<StaffValidateClassesPage />} />
                <Route path="/staff/modalidades" element={<StaffModalitiesPage />} />
                <Route path="/staff/estudios" element={<StaffStudiosPage />} />
                <Route path="/staff/eventos" element={<StaffEventsPage />} />
                <Route path="/staff/inventario" element={<StaffInventoryPage />} />
                <Route path="/staff/inventario/:itemId" element={<StaffItemDetailPage />} />
                <Route path="/staff/agenda" element={<StaffAgendaPage />} />
                <Route path="/staff/faturacao" element={<StaffBillingPage />} />
                <Route path="/staff/bloqueios" element={<StaffBlockedPeriodsPage />} />
            </Route>

            {/* COACH */}
            <Route
                element={
                    <ProtectedRoute allowedRoles={['coach', 'admin']}>
                        <DashboardLayout role="coach" />
                    </ProtectedRoute>
                }
            >
                <Route path="/coach" element={<CoachDashboardPage />} />
                <Route path="/coach/disponibilidade" element={<CoachAvailabilityPage />} />
                <Route path="/coach/validar-aulas" element={<CoachValidateClassesPage />} />
                <Route path="/coach/agenda" element={<CoachAgendaPage />} />
                <Route path="/coach/eventos" element={<CoachEventsPage />} />
            </Route>

            {/* PARENT */}
            <Route
                element={
                    <ProtectedRoute allowedRoles={['parent', 'admin']}>
                        <DashboardLayout role="parent" />
                    </ProtectedRoute>
                }
            >
                <Route path="/parent" element={<ParentDashboardPage />} />
                <Route path="/parent/aulas" element={<ParentClassesPage />} />
                <Route path="/parent/estudantes" element={<ParentStudentsPage />} />
                <Route path="/parent/inventario" element={<ParentInventoryPage />} />
                <Route path="/parent/inventario/:itemId" element={<ParentItemDetailPage />} />
                <Route path="/parent/eventos" element={<ParentEventsPage />} />
            </Route>
        </Routes>
    )
}

export default App
