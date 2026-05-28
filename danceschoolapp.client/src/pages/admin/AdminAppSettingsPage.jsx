import { useEffect, useState } from 'react'
import PageCard from '../../components/common/PageCard'
import Icon from '../../components/ui/Icon'
import { getAppSettings, updateAppSetting } from '../../services/appSettingsService'
import '../../styles/AdminPage.css'
import '../../styles/AppSettings.css'

// ─── Meta — how to render each known key ─────────────────────────────────────

const SETTING_META = {
    validation_window_hours: {
        label: 'Janela de validação',
        description: 'Horas que o coach tem para validar uma aula após o fim.',
        type: 'number',
        unit: 'horas',
        min: 1,
        max: 168,
    },
    class_price_weekday: {
        label: 'Preço — dia de semana e sábados',
        description: 'Valor base cobrado por coaching em dia útil e sábados por hora.',
        type: 'decimal',
        unit: '€',
        min: 0,
    },
    class_price_weekend: {
        label: 'Preço — domingo e feriados',
        description: 'Valor base cobrado por coaching ao domingo e feriados por hora.',
        type: 'decimal',
        unit: '€',
        min: 0,
    },
    max_participants: {
        label: 'Máx. participantes',
        description: 'Número máximo de alunos por coaching.',
        type: 'number',
        unit: 'alunos',
        min: 1,
        max: 50,
    },
    join_class_enabled: {
        label: 'Inscrições abertas',
        description: 'Permite ou bloqueia inscrições de alunos em novos coachings.',
        type: 'boolean',
    },
}

const pad2 = (d) => {
    if (!d) return '—'
    const parts = String(d).split('-')
    if (parts.length === 3) return `${parts[2]}/${parts[1]}/${parts[0]}`
    return d
}

// ─── Single setting row ───────────────────────────────────────────────────────

function SettingRow({ setting }) {
    const meta = SETTING_META[setting.key] ?? { label: setting.key, type: 'text' }
    const [editing, setEditing] = useState(false)
    const [draft, setDraft] = useState(setting.value)
    const [saving, setSaving] = useState(false)
    const [error, setError] = useState('')
    const [saved, setSaved] = useState(false)

    const handleSave = async () => {
        setSaving(true)
        setError('')
        setSaved(false)
        try {
            await updateAppSetting(setting.key, draft)
            setSaved(true)
            setEditing(false)
            setTimeout(() => setSaved(false), 2500)
        } catch (err) {
            setError(err.message)
        } finally {
            setSaving(false)
        }
    }

    const handleCancel = () => {
        setDraft(setting.value)
        setEditing(false)
        setError('')
    }

    // Boolean toggle — no edit mode needed
    if (meta.type === 'boolean') {
        const isOn = draft === 'true' || draft === true

        const handleToggle = async () => {
            const next = isOn ? 'false' : 'true'
            setDraft(next)
            setSaving(true)
            setError('')
            setSaved(false)
            try {
                await updateAppSetting(setting.key, next)
                setSaved(true)
                setTimeout(() => setSaved(false), 2500)
            } catch (err) {
                setDraft(isOn ? 'true' : 'false') // revert
                setError(err.message)
            } finally {
                setSaving(false)
            }
        }

        return (
            <div className="as-row">
                <div className="as-row-info">
                    <span className="as-row-label">{meta.label}</span>
                    <span className="as-row-desc">{meta.description}</span>
                    {error && <span className="as-row-error">{error}</span>}
                </div>

                <div className="as-row-control">
                    <button
                        type="button"
                        className={`as-toggle ${isOn ? 'as-toggle--on' : 'as-toggle--off'}`}
                        onClick={handleToggle}
                        disabled={saving}
                        aria-label={isOn ? 'Desativar' : 'Ativar'}
                    >
                        <span className="as-toggle-thumb" />
                    </button>
                    <span className={`as-toggle-label ${isOn ? 'as-toggle-label--on' : ''}`}>
                        {isOn ? 'Ativo' : 'Inativo'}
                    </span>
                    {saved && <span className="as-saved-badge"><Icon name="check" size={13} /> Guardado</span>}
                </div>

                <span className="as-row-date">{pad2(setting.updatedAt)}</span>
            </div>
        )
    }

    // Numeric / decimal / text — inline edit
    const displayValue = meta.unit === '€'
        ? `${parseFloat(draft).toFixed(2)} €`
        : `${draft}${meta.unit ? ` ${meta.unit}` : ''}`

    return (
        <div className="as-row">
            <div className="as-row-info">
                <span className="as-row-label">{meta.label}</span>
                <span className="as-row-desc">{meta.description}</span>
                {error && <span className="as-row-error">{error}</span>}
            </div>

            <div className="as-row-control">
                {editing ? (
                    <div className="as-edit-group">
                        <input
                            type={meta.type === 'decimal' ? 'number' : meta.type === 'number' ? 'number' : 'text'}
                            step={meta.type === 'decimal' ? '0.01' : '1'}
                            min={meta.min}
                            max={meta.max}
                            value={draft}
                            onChange={(e) => setDraft(e.target.value)}
                            className="input as-input"
                            autoFocus
                            onKeyDown={(e) => {
                                if (e.key === 'Enter') handleSave()
                                if (e.key === 'Escape') handleCancel()
                            }}
                        />
                        {meta.unit && <span className="as-unit">{meta.unit}</span>}
                        <button type="button" className="btn btn-primary btn-sm" onClick={handleSave} disabled={saving}>
                            {saving ? '...' : 'Guardar'}
                        </button>
                        <button type="button" className="btn btn-ghost btn-sm" onClick={handleCancel} disabled={saving}>
                            Cancelar
                        </button>
                    </div>
                ) : (
                    <div className="as-view-group">
                        <span className="as-value">{displayValue}</span>
                        {saved && <span className="as-saved-badge"><Icon name="check" size={13} /> Guardado</span>}
                        <button type="button" className="btn btn-secondary btn-sm" onClick={() => setEditing(true)}>
                            Editar
                        </button>
                    </div>
                )}
            </div>

            <span className="as-row-date">{pad2(setting.updatedAt)}</span>
        </div>
    )
}

// ─── Page ─────────────────────────────────────────────────────────────────────

function AdminAppSettingsPage() {
    const [settings, setSettings] = useState([])
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState('')

    useEffect(() => {
        getAppSettings()
            .then((data) => {
                // Normalise key casing from backend (Key vs key)
                const list = (Array.isArray(data) ? data : []).map((s) => ({
                    settingId: s.SettingId ?? s.settingId,
                    key: s.Key ?? s.key,
                    value: s.Value ?? s.value,
                    updatedAt: s.UpdatedAt ?? s.updatedAt,
                }))
                // Sort by known meta order, unknowns at the end
                const order = Object.keys(SETTING_META)
                list.sort((a, b) => {
                    const ai = order.indexOf(a.key)
                    const bi = order.indexOf(b.key)
                    if (ai === -1 && bi === -1) return 0
                    if (ai === -1) return 1
                    if (bi === -1) return -1
                    return ai - bi
                })
                setSettings(list)
            })
            .catch((err) => setError(err.message))
            .finally(() => setLoading(false))
    }, [])

    return (
        <PageCard>
            <div className="admin-page-header">
                <div>
                    <h2>Configurações do Sistema</h2>
                    <p>Parâmetros globais que afetam o comportamento da aplicação.</p>
                </div>
            </div>

            {error && <p className="admin-error">{error}</p>}

            {loading ? (
                <p style={{ color: 'var(--text-3)', fontSize: '0.9rem' }}>A carregar configurações...</p>
            ) : (
                <div className="as-list">
                    <div className="as-list-header">
                        <span>Configuração</span>
                        <span>Valor</span>
                        <span>Atualizado em</span>
                    </div>

                        {settings.map((s) => (
                            <SettingRow key={s.settingId ?? s.key} setting={s} />
                        ))}
                </div>
            )}
        </PageCard>
    )
}

export default AdminAppSettingsPage