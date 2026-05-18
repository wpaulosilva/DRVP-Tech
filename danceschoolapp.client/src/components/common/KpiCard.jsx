import { Link } from 'react-router-dom'
import Icon from '../ui/Icon'

/**
 * KpiCard — stat card for dashboards.
 * @param {string}  label        - metric name
 * @param {*}       value        - numeric value
 * @param {boolean} loading      - shows placeholder when true
 * @param {string}  tone         - 'default' | 'orange' | 'success' | 'danger'
 * @param {string}  icon         - Icon name (see Icon.jsx)
 * @param {string}  description  - small subtext below value
 * @param {string}  to           - if provided, card is a navigable link
 */
function KpiCard({
    label,
    value,
    loading   = false,
    tone      = 'default',
    icon,
    description,
    to,
}) {
    const toneClass = tone !== 'default' ? ` kpi-card--${tone}` : ''
    const className = `kpi-card${toneClass}`

    const inner = (
        <>
            {icon && (
                <div className="kpi-card-icon">
                    <Icon name={icon} size={18} />
                </div>
            )}
            <span className="kpi-label">{label}</span>
            <span className="kpi-value">
                {loading ? '—' : (value ?? '—')}
            </span>
            {description && !loading && (
                <span className="kpi-description">{description}</span>
            )}
        </>
    )

    if (to) {
        return (
            <Link to={to} className={className} style={{ textDecoration: 'none' }}>
                {inner}
            </Link>
        )
    }

    return <div className={className}>{inner}</div>
}

export default KpiCard
