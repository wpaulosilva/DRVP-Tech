import { NavLink } from 'react-router-dom'
import Icon from '../components/ui/Icon'

function Sidebar({ items = [] }) {
    return (
        <aside className="dashboard-sidebar">
            <nav className="dashboard-sidebar-nav" aria-label="Navegacao principal">
                {items.map((item, index) => (
                    <NavLink
                        key={item.to}
                        to={item.to}
                        end={index === 0}
                        className={({ isActive }) =>
                            isActive
                                ? 'dashboard-sidebar-link active'
                                : 'dashboard-sidebar-link'
                        }
                    >
                        {item.icon && (
                            <span className="sidebar-link-icon">
                                <Icon name={item.icon} size={16} />
                            </span>
                        )}
                        {item.label}
                    </NavLink>
                ))}
            </nav>
        </aside>
    )
}

export default Sidebar
