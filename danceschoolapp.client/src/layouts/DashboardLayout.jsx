import { Outlet } from 'react-router-dom'
import Sidebar from './Sidebar'
import Topbar from './Topbar'
import { sidebarMenus } from '../data/SidebarMenus'
import '../styles/Dashboard.css'

function DashboardLayout({ role = 'admin' }) {
    const items = sidebarMenus[role] || []

    return (
        <div className="dashboard-shell">
            <Topbar />

            <div className="dashboard-body">
                <Sidebar items={items} />

                <main className="dashboard-content">
                    <Outlet />
                </main>
            </div>
        </div>
    )
}

export default DashboardLayout