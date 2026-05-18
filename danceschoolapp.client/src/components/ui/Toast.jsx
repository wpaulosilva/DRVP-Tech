import Icon from './Icon'
import './Toast.css'

const typeIcon = {
    success: 'check',
    error:   'x',
    warning: 'alert',
    info:    'info',
}

function Toast({ toasts, onDismiss }) {
    if (!toasts.length) return null
    return (
        <div className="toast-stack" role="region" aria-label="Notificacoes">
            {toasts.map(({ id, message, type }) => (
                <div key={id} className={`toast toast--${type}`} role="alert">
                    <span className="toast-icon">
                        <Icon name={typeIcon[type] ?? 'info'} size={16} />
                    </span>
                    <span className="toast-message">{message}</span>
                    <button
                        className="toast-close"
                        onClick={() => onDismiss(id)}
                        aria-label="Fechar"
                    >
                        <Icon name="x" size={14} />
                    </button>
                </div>
            ))}
        </div>
    )
}

export default Toast
