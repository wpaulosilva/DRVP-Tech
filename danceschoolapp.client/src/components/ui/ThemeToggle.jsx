import { useTheme } from '../../hooks/useTheme'
import Icon from './Icon'

function ThemeToggle() {
    const { theme, toggle } = useTheme()
    return (
        <button
            className="theme-toggle-btn"
            onClick={toggle}
            title={theme === 'dark' ? 'Mudar para tema claro' : 'Mudar para tema escuro'}
            aria-label="Alternar tema"
        >
            <Icon name={theme === 'dark' ? 'sun' : 'moon'} size={18} />
        </button>
    )
}

export default ThemeToggle
