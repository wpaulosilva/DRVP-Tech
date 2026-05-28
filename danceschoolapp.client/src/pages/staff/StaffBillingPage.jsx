// Tabs: students | coaches
// GET /api/staff/billing/students?month={YYYY-MM}&page=1&pageSize=25
// GET /api/staff/billing/coaches?month={YYYY-MM}&page=1&pageSize=25

import React, { useEffect, useState } from 'react'
import { getBillingStudentsAll, getBillingCoachesAll } from '../../services/billingService'
import { get } from '../../api/client'
import DataTable from '../../components/common/DataTable'
import './StaffBillingPage.css'

function readProp(obj, candidates) {
    if (!obj) return undefined

    const keys = Object.keys(obj)

    for (const name of candidates) {
        const n = String(name)

        if (n.includes('.')) {
            const parts = n.split('.')
            let cur = obj
            let ok = true

            for (const p of parts) {
                if (!cur || typeof cur !== 'object') {
                    ok = false
                    break
                }

                const k = Object.keys(cur).find(k2 => k2.toLowerCase() === p.toLowerCase())

                if (!k) {
                    ok = false
                    break
                }

                cur = cur[k]
            }

            if (ok) return cur
            continue
        }

        const found = keys.find(k => k.toLowerCase() === n.toLowerCase())
        if (found) return obj[found]
    }

    return undefined
}

function resolveName(obj, candidateKeys = []) {
    if (!obj) return ''

    const direct = readProp(obj, candidateKeys)
    if (direct) return String(direct)

    const person = readProp(obj, ['personInfo', 'PersonInfo', 'person', 'Person'])
    if (person) {
        const first = readProp(person, ['firstName', 'FirstName', 'firstname'])
        const last = readProp(person, ['lastName', 'LastName', 'lastname'])

        if (first || last) return `${first ?? ''} ${last ?? ''}`.trim()
    }

    const nested = readProp(obj, ['coach', 'Coach', 'student', 'Student'])
    if (nested) {
        const first = readProp(nested, ['firstName', 'FirstName', 'firstname'])
        const last = readProp(nested, ['lastName', 'LastName', 'lastname'])

        if (first || last) return `${first ?? ''} ${last ?? ''}`.trim()
    }

    return ''
}

function formatMoney(value) {
    const number = Number(value) || 0

    return new Intl.NumberFormat('pt-PT', {
        style: 'currency',
        currency: 'EUR'
    }).format(number)
}

function StudentsTable({ month }) {
    const [rows, setRows] = useState([])
    const [search, setSearch] = useState('')
    const [weekdayRate, setWeekdayRate] = useState(36.00)
    const [_weekendRate, setWeekendRate] = useState(43.50)

    useEffect(() => {
        let mounted = true

        void (async () => {
            try {
                const res = await get('/api/staff/appsettings')
                const list = Array.isArray(res) ? res : res.items ?? res.Items ?? []
                const map = {}

                list.forEach(s => {
                    const key = (s.key ?? s.Key ?? s.settingKey ?? s.SettingKey ?? '').toString().toLowerCase()
                    const val = s.value ?? s.Value ?? s.settingValue ?? s.SettingValue ?? ''

                    map[key] = val
                })

                const wk = parseFloat(map.class_price_weekday)
                const we = parseFloat(map.class_price_weekend)

                if (!Number.isNaN(wk)) setWeekdayRate(wk)
                if (!Number.isNaN(we)) setWeekendRate(we)
            } catch {
                // Ignorar: mantém defaults 36.00€/h e 43.50€/h
            }
        })()

        void (async () => {
            try {
                const res = await getBillingStudentsAll(month)
                if (!mounted) return

                const items = Array.isArray(res) ? res : res.items ?? res.Items ?? []

                setRows(items.map(i => {
                    let hoursWeekday = Number(readProp(i, ['hoursWeekday', 'HoursWeekday', 'hours_weekday']) ?? 0)
                    let hoursWeekend = Number(readProp(i, ['hoursWeekend', 'HoursWeekend', 'hours_weekend']) ?? 0)
                    const totalAmount = Number(readProp(i, ['totalAmount', 'TotalAmount', 'total_amount']) ?? 0)
                    const nif = readProp(i, ['nif', 'Nif', 'personInfo.nif']) ?? ''

                    if (totalAmount && !hoursWeekday && !hoursWeekend) {
                        const est = weekdayRate > 0 ? totalAmount / weekdayRate : 0
                        hoursWeekday = Math.round(est * 100) / 100

                        console.warn('Billing students: horas estimadas a partir do total.', {
                            estimated: hoursWeekday,
                            totalAmount,
                            weekdayRate,
                            item: i
                        })
                    }

                    return {
                        id: readProp(i, ['studentId', 'StudentId']) ?? null,
                        Aluno: resolveName(i, ['studentName', 'StudentName', 'name']),
                        Nif: nif,
                        'Horas Realizadas Dias': hoursWeekday,
                        'Horas Realizadas FimDeSemana': hoursWeekend,
                        'Total a Pagar': totalAmount
                    }
                }))
            } catch (err) {
                console.error('Erro ao obter faturação dos alunos', err)
            }
        })()

        return () => {
            mounted = false
        }
    }, [month, weekdayRate])

    const columns = [
        { key: 'Aluno', label: 'Aluno' },
        { key: 'Nif', label: 'NIF' },
        { key: 'Horas Realizadas Dias', label: 'Horas Realizadas (segunda a sábado)' },
        { key: 'Horas Realizadas FimDeSemana', label: 'Horas Realizadas (domingo ou feriados)' },
        { key: 'Total a Pagar', label: 'Total a Pagar' }
    ]

    const totalStudents = rows.length
    const receitaTotal = rows.reduce((sum, row) => sum + (Number(row['Total a Pagar']) || 0), 0)
    const horasTotais = rows.reduce(
        (sum, row) =>
            sum +
            (Number(row['Horas Realizadas Dias']) || 0) +
            (Number(row['Horas Realizadas FimDeSemana']) || 0),
        0
    )
    const pendentes = 0

    const filtered = rows.filter(row => {
        if (!search) return true
        return String(row.Aluno).toLowerCase().includes(search.toLowerCase())
    })

    return (
        <div className="billing-section">
            <div className="cards-row">
                <div className="card">
                    <div className="card-title">Total Alunos</div>
                    <div className="card-value">{totalStudents}</div>
                </div>

                <div className="card">
                    <div className="card-title">Receita Total</div>
                    <div className="card-value card-value-money">{formatMoney(receitaTotal)}</div>
                </div>

                <div className="card">
                    <div className="card-title">Horas Totais</div>
                    <div className="card-value">{horasTotais}h</div>
                </div>

                <div className="card">
                    <div className="card-title">Pendentes</div>
                    <div className="card-value">{pendentes}</div>
                </div>
            </div>

            <div className="controls-row">
                <div className="search-box">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M21 21l-4.35-4.35" stroke="#9CA3AF" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                        <path d="M11 19a8 8 0 1 1 0-16 8 8 0 0 1 0 16z" stroke="#9CA3AF" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>

                    <input
                        placeholder="Nome do aluno..."
                        value={search}
                        onChange={event => setSearch(event.target.value)}
                    />
                </div>

                <div style={{ marginLeft: 'auto' }}>
                    <button
                        className="btn-primary"
                        onClick={async () => {
                            const params = new URLSearchParams({ month })

                            if (search) params.append('search', search)

                            const res = await fetch(`/api/staff/billing/students/export?${params}`, {
                                credentials: 'include'
                            })

                            const blob = await res.blob()
                            const url = URL.createObjectURL(blob)
                            const a = document.createElement('a')

                            a.href = url
                            a.download = `billing_students_${month}.xlsx`
                            a.click()

                            URL.revokeObjectURL(url)
                        }}
                    >
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" style={{ marginRight: 8 }} xmlns="http://www.w3.org/2000/svg">
                            <path d="M21 15V19A2 2 0 0 1 19 21H5A2 2 0 0 1 3 19V15" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                            <path d="M7 10L12 15L17 10" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                            <path d="M12 15V3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                        </svg>
                        Exportar Excel
                    </button>
                </div>
            </div>

            <DataTable columns={columns} rows={filtered} loading={false} />
        </div>
    )
}

function CoachesTable({ month }) {
    const [rows, setRows] = useState([])
    const [search, setSearch] = useState('')
    const [weekdayRate, setWeekdayRate] = useState(36.00)
    const [_weekendRate, setWeekendRate] = useState(43.50)

    useEffect(() => {
        let mounted = true

        void (async () => {
            try {
                const res = await get('/api/staff/appsettings')
                if (!mounted) return

                const list = Array.isArray(res) ? res : res.items ?? res.Items ?? []
                const map = {}

                list.forEach(s => {
                    const key = (s.key ?? s.Key ?? s.settingKey ?? s.SettingKey ?? '').toString().toLowerCase()
                    const val = s.value ?? s.Value ?? s.settingValue ?? s.SettingValue ?? ''

                    map[key] = val
                })

                const wk = parseFloat(map.class_price_weekday)
                const we = parseFloat(map.class_price_weekend)

                if (!Number.isNaN(wk)) setWeekdayRate(wk)
                if (!Number.isNaN(we)) setWeekendRate(we)
            } catch {
                // Ignorar: mantém defaults 36.00€/h e 43.50€/h
            }
        })()

        return () => {
            mounted = false
        }
    }, [])

    useEffect(() => {
        let mounted = true

        void (async () => {
            try {
                const res = await getBillingCoachesAll(month)
                if (!mounted) return

                const items = Array.isArray(res) ? res : res.items ?? res.Items ?? []

                setRows(items.map(i => {
                    let hoursWeekday = Number(readProp(i, ['hoursWeekday', 'HoursWeekday', 'hours_weekday']) ?? 0)
                    let hoursWeekend = Number(readProp(i, ['hoursWeekend', 'HoursWeekend', 'hours_weekend']) ?? 0)
                    const totalAmount = Number(readProp(i, ['totalAmount', 'TotalAmount', 'total_amount']) ?? 0)
                    const nif = readProp(i, ['nif', 'Nif', 'coachNavigation.personInfo.nif', 'coach.personInfo.nif']) ?? ''

                    if (totalAmount && !hoursWeekday && !hoursWeekend) {
                        const est = weekdayRate > 0 ? totalAmount / weekdayRate : 0
                        hoursWeekday = Math.round(est * 100) / 100

                        console.warn('Billing coaches: horas estimadas a partir do total.', {
                            estimated: hoursWeekday,
                            totalAmount,
                            weekdayRate,
                            item: i
                        })
                    }

                    return {
                        Professor: resolveName(i, ['coachName', 'CoachName', 'name']),
                        Nif: nif,
                        'Horas Realizadas Dias': hoursWeekday,
                        'Horas Realizadas FimDeSemana': hoursWeekend,
                        'Total a Pagar': totalAmount
                    }
                }))
            } catch (err) {
                console.error('Erro ao obter faturação dos professores', err)
            }
        })()

        return () => {
            mounted = false
        }
    }, [month, weekdayRate])

    const columns = [
        { key: 'Professor', label: 'Professor' },
        { key: 'Nif', label: 'NIF' },
        { key: 'Horas Realizadas Dias', label: 'Horas Realizadas (segunda a sábado)' },
        { key: 'Horas Realizadas FimDeSemana', label: 'Horas Realizadas (domingo ou feriados)' },
        { key: 'Total a Pagar', label: 'Total a Receber' }
    ]

    const filtered = rows.filter(row => {
        if (!search) return true
        return String(row.Professor).toLowerCase().includes(search.toLowerCase())
    })

    const totalCoaches = rows.length
    const totalExpense = rows.reduce((sum, row) => sum + (Number(row['Total a Pagar']) || 0), 0)
    const horasTotais = rows.reduce(
        (sum, row) =>
            sum +
            (Number(row['Horas Realizadas Dias']) || 0) +
            (Number(row['Horas Realizadas FimDeSemana']) || 0),
        0
    )

    return (
        <div className="billing-section">
            <div className="cards-row">
                <div className="card">
                    <div className="card-title">Total Professores</div>
                    <div className="card-value">{totalCoaches}</div>
                </div>

                <div className="card">
                    <div className="card-title">Total a Receber</div>
                    <div className="card-value card-value-money">{formatMoney(totalExpense)}</div>
                </div>

                <div className="card">
                    <div className="card-title">Horas Totais</div>
                    <div className="card-value">{horasTotais}h</div>
                </div>
            </div>

            <div className="controls-row">
                <div className="search-box">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M21 21l-4.35-4.35" stroke="#9CA3AF" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                        <path d="M11 19a8 8 0 1 1 0-16 8 8 0 0 1 0 16z" stroke="#9CA3AF" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>

                    <input
                        placeholder="Nome do professor..."
                        value={search}
                        onChange={event => setSearch(event.target.value)}
                    />
                </div>

                <div style={{ marginLeft: 'auto' }}>
                    <button
                        className="btn-primary"
                        onClick={async () => {
                            const params = new URLSearchParams({ month })

                            if (search) params.append('search', search)

                            const res = await fetch(`/api/staff/billing/coaches/export?${params}`, {
                                credentials: 'include'
                            })

                            const blob = await res.blob()
                            const url = URL.createObjectURL(blob)
                            const a = document.createElement('a')

                            a.href = url
                            a.download = `billing_coaches_${month}.xlsx`
                            a.click()

                            URL.revokeObjectURL(url)
                        }}
                    >
                        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" style={{ marginRight: 8 }} xmlns="http://www.w3.org/2000/svg">
                            <path d="M21 15V19A2 2 0 0 1 19 21H5A2 2 0 0 1 3 19V15" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                            <path d="M7 10L12 15L17 10" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                            <path d="M12 15V3" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                        </svg>
                        Exportar Excel
                    </button>
                </div>
            </div>

            <DataTable columns={columns} rows={filtered} loading={false} />
        </div>
    )
}

function StaffBillingPage() {
    const [tab, setTab] = useState('students')
    const [month, setMonth] = useState(() => new Date().toISOString().slice(0, 7))

    return (
        <section className="dashboard-page-card">
            <h2>Faturação</h2>
            <p>Gerir tabelas de alunos e professores</p>

            <div style={{ marginTop: 18 }}>
                <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginBottom: 12 }}>
                    <button
                        className={`pill ${tab === 'students' ? 'active' : ''}`}
                        onClick={() => setTab('students')}
                    >
                        Tabela de Alunos
                    </button>

                    <button
                        className={`pill ${tab === 'coaches' ? 'active' : ''}`}
                        onClick={() => setTab('coaches')}
                    >
                        Tabela de Professores
                    </button>

                    <div style={{ marginLeft: 'auto', display: 'flex', gap: 8, alignItems: 'center' }}>
                        <input
                            type="month"
                            value={month}
                            onChange={event => setMonth(event.target.value)}
                        />
                    </div>
                </div>

                <div style={{ marginTop: 12 }}>
                    {tab === 'students' ? (
                        <StudentsTable month={month} />
                    ) : (
                        <CoachesTable month={month} />
                    )}
                </div>
            </div>
        </section>
    )
}

export default StaffBillingPage