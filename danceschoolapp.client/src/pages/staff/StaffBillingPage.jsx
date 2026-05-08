// Tabs: students | coaches
// GET /api/staff/billing/students?month={YYYY-MM}&page=1&pageSize=25
// GET /api/staff/billing/coaches?month={YYYY-MM}&page=1&pageSize=25
import React, { useEffect, useState } from 'react'
import { getBillingStudentsAll, getBillingCoachesAll } from '../../services/billingService'
import { get } from '../../api/client'
import * as XLSX from 'xlsx'
import DataTable from '../../components/common/DataTable'
import './StaffBillingPage.css'

// Helper to read property case-insensitively from server objects
function readProp(obj, candidates) {
  if (!obj) return undefined
  const keys = Object.keys(obj)
  for (const name of candidates) {
    const n = String(name)
    if (n.includes('.')) {
      // traverse nested path, case-insensitive at each level
      const parts = n.split('.')
      let cur = obj
      let ok = true
      for (const p of parts) {
        if (!cur || typeof cur !== 'object') { ok = false; break }
        const k = Object.keys(cur).find(k2 => k2.toLowerCase() === p.toLowerCase())
        if (!k) { ok = false; break }
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

// Resolve a display name from possible DTO shapes
function resolveName(obj, candidateKeys = []) {
  if (!obj) return ''
  // try simple properties
  const direct = readProp(obj, candidateKeys)
  if (direct) return String(direct)

  // try personInfo nested
  const person = readProp(obj, ['personInfo', 'PersonInfo', 'person', 'Person'])
  if (person) {
    const first = readProp(person, ['firstName', 'FirstName', 'firstname'])
    const last = readProp(person, ['lastName', 'LastName', 'lastname'])
    if (first || last) return `${first ?? ''} ${last ?? ''}`.trim()
  }

  // try coach/student nested
  const nested = readProp(obj, ['coach', 'Coach', 'student', 'Student'])
  if (nested) {
    const first = readProp(nested, ['firstName', 'FirstName', 'firstname'])
    const last = readProp(nested, ['lastName', 'LastName', 'lastname'])
    if (first || last) return `${first ?? ''} ${last ?? ''}`.trim()
  }

  return ''
}

function formatMoney(v) { return typeof v === 'number' ? `€${v}` : v }

function downloadExcelFormatted(data, filename = 'export.xlsx', headerKeys = null, headerLabels = null) { 
  // Ensure data is array
  const rows = Array.isArray(data) ? data : []

  // default keys and labels
  const defaultKeys = ['Aluno', 'Nif', 'Horas Realizadas Dias', 'Horas Realizadas FimDeSemana', 'Total a Pagar']
  const keys = Array.isArray(headerKeys) && headerKeys.length ? headerKeys : defaultKeys
  const labels = Array.isArray(headerLabels) && headerLabels.length ? headerLabels : keys

  const aoa = [labels]

  // populate rows in order, coercing numbers
  rows.forEach(r => {
    const line = keys.map(k => {
      const v = r[k]
      if (v === undefined || v === null) return ''
      if (typeof v === 'number') return v
      if (typeof v === 'string' && v.trim() === '') return ''
      // try to coerce numeric strings (but keep non-numeric strings as-is)
      const cleaned = String(v).replace(/[^0-9,.-]+/g, '').replace(',', '.')
      if (cleaned === '') return String(v)
      const num = Number(cleaned)
      return Number.isNaN(num) ? String(v) : num
    })
    aoa.push(line)
  })

  // totals row (sum numeric columns)
  const totals = keys.map((k, idx) => {
    if (idx === 0) return 'Total'
    if (idx === 1) return ''
    // sum numeric column
    return aoa.slice(1).reduce((s, row) => s + (Number(row[idx]) || 0), 0)
  })
  aoa.push(totals)

  const ws = XLSX.utils.aoa_to_sheet(aoa)

  // auto column widths (approx)
  ws['!cols'] = keys.map(h => ({ wch: Math.max(8, Math.min(30, String(h).length + 8)) }))

  // Styling: header purple with white text, data rows white with thin border, totals light purple
  const headerColor = 'FF6D28D9'
  const totalsBg = 'FFF3E8FF'
  try {
    const range = XLSX.utils.decode_range(ws['!ref'])

    // header
    for (let C = 0; C <= range.e.c; ++C) {
      const cellRef = XLSX.utils.encode_cell({ r: 0, c: C })
      const cell = ws[cellRef]
      if (!cell) continue
      cell.s = {
        font: { bold: true, color: { rgb: 'FFFFFFFF' } },
        fill: { patternType: 'solid', fgColor: { rgb: headerColor } },
        alignment: { horizontal: 'center', vertical: 'center' }
      }
    }

    // data rows
    for (let R = 1; R <= range.e.r; ++R) {
      for (let C = 0; C <= range.e.c; ++C) {
        const cellRef = XLSX.utils.encode_cell({ r: R, c: C })
        const cell = ws[cellRef]
        if (!cell) continue
        cell.s = cell.s || {}
        // totals row styling
        if (R === range.e.r) {
          cell.s.fill = { patternType: 'solid', fgColor: { rgb: totalsBg } }
          cell.s.font = { bold: true }
        } else {
          cell.s.fill = { patternType: 'solid', fgColor: { rgb: 'FFFFFFFF' } }
        }
        // thin border
        cell.s.border = {
          top: { style: 'thin', color: { rgb: 'FFEFEFF2' } },
          bottom: { style: 'thin', color: { rgb: 'FFEFEFF2' } },
          left: { style: 'thin', color: { rgb: 'FFEFEFF2' } },
          right: { style: 'thin', color: { rgb: 'FFEFEFF2' } }
        }
      }
    }

    // apply number formats for hours and currency
    for (let R = 1; R <= range.e.r; ++R) {
      const hoursWeekCell = ws[XLSX.utils.encode_cell({ r: R, c: 2 })]
      if (hoursWeekCell && typeof hoursWeekCell.v === 'number') { hoursWeekCell.t = 'n'; hoursWeekCell.z = '0.00' }
      const hoursWeekendCell = ws[XLSX.utils.encode_cell({ r: R, c: 3 })]
      if (hoursWeekendCell && typeof hoursWeekendCell.v === 'number') { hoursWeekendCell.t = 'n'; hoursWeekendCell.z = '0.00' }
      const totalCell = ws[XLSX.utils.encode_cell({ r: R, c: 4 })]
      if (totalCell && typeof totalCell.v === 'number') { totalCell.t = 'n'; totalCell.z = '€#,##0.00' }
    }
  } catch {
    // ignore styling errors
  }

  // number formats for currency/hours heuristics using keys
  keys.forEach((h, c) => {
    const lc = String(h).toLowerCase()
    const isCurrency = lc.includes('total') || lc.includes('€') || lc.includes('paid') || lc.includes('receita')
    const isHours = lc.includes('hora') || lc.includes('horas')
    for (let r = 1; r < aoa.length; r++) {
      const cellRef = XLSX.utils.encode_cell({ r, c })
      const cell = ws[cellRef]
      if (!cell) continue
      if (isCurrency && typeof cell.v === 'number') {
        cell.t = 'n'
        cell.z = '€#,##0.00'
      }
      if (isHours && typeof cell.v === 'number') {
        cell.t = 'n'
        cell.z = '0"h"'
      }
    }
  })

  // apply totals row styling (last row)
  const lastRowIndex = aoa.length - 1
  try {
    keys.forEach((h, c) => {
      const cellRef = XLSX.utils.encode_cell({ r: lastRowIndex, c })
      const cell = ws[cellRef]
      if (!cell) return
      cell.s = cell.s || {}
      cell.s.font = { bold: true }
      cell.s.fill = { patternType: 'solid', fgColor: { rgb: totalsBg } }
      // currency number format for totals
      const lc = String(h).toLowerCase()
      if (lc.includes('total') || lc.includes('€') || lc.includes('paid') || lc.includes('receita')) {
        cell.t = 'n'
        cell.z = '€#,##0.00'
        cell.s.font.color = { rgb: 'FF008000' }
      }
      if (lc.includes('hora')) {
        cell.t = 'n'
        cell.z = '0"h"'
      }
    })
  } catch {
    // ignore styling errors
  }

  const wb = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(wb, ws, 'Faturacao')
  XLSX.writeFile(wb, filename)
}

function StudentsTable({ month }) {
  const [rows, setRows] = useState([])
  const [search, setSearch] = useState('')
  const [weekdayRate, setWeekdayRate] = useState(36.00)
  const [_weekendRate, set_weekendRate] = useState(43.20)

  useEffect(() => {
    let mounted = true
    // fetch rates from appsettings
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
        const wk = parseFloat(map['class_price_weekday'])
        const we = parseFloat(map['class_price_weekend'])
        if (!Number.isNaN(wk)) setWeekdayRate(wk)
        if (!Number.isNaN(we)) set_weekendRate(we)
      } catch {
        // ignore
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
          // warn if hours are zero but total exists (server mapping issue)
          if (totalAmount && !hoursWeekday && !hoursWeekend) {
            // fallback: estimate weekday hours using weekdayRate
            const est = weekdayRate > 0 ? +(totalAmount / weekdayRate) : 0
            hoursWeekday = Math.round(est * 100) / 100
            console.warn('Billing: estimated hoursWeekday from total using weekdayRate', { estimated: hoursWeekday, totalAmount, weekdayRate, item: i })
          }
          return {
            id: readProp(i, ['studentId','StudentId','studentId']) ?? null,
            Aluno: resolveName(i, ['studentName','StudentName','name','studentName']),
            Nif: nif,
            'Horas Realizadas Dias': hoursWeekday,
            'Horas Realizadas FimDeSemana': hoursWeekend,
            'Total a Pagar': totalAmount
          }
        }))
      } catch (err) {
        console.error('Erro fetch students for billing', err)
      }
    })()
    return () => { mounted = false }
  }, [month])

  const columns = [
    { key: 'Aluno', label: 'Aluno' },
    { key: 'Nif', label: 'NIF' },
    { key: 'Horas Realizadas Dias', label: 'Horas Realizadas (dias úteis)' },
    { key: 'Horas Realizadas FimDeSemana', label: 'Horas Realizadas (fins de semana)' },
    { key: 'Total a Pagar', label: 'Total a Pagar' }
  ]

  // compute aggregates
  const totalStudents = rows.length
  const receitaTotal = rows.reduce((s, r) => s + (Number(r['Total a Pagar']) || 0), 0)
  const horasTotais = rows.reduce((s, r) => s + (Number(r['Horas Realizadas Dias']) || 0) + (Number(r['Horas Realizadas FimDeSemana']) || 0), 0)
  const pendentes = 0

  const filtered = rows.filter(r => {
    if (search && !String(r.Aluno).toLowerCase().includes(search.toLowerCase())) return false
    return true
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
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M21 21l-4.35-4.35" stroke="#9CA3AF" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/><path d="M11 19a8 8 0 1 1 0-16 8 8 0 0 1 0 16z" stroke="#9CA3AF" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg>
          <input placeholder="Nome do aluno..." value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>

        {/* status filter removed; kept layout space minimal */}

        <div style={{ marginLeft: 'auto' }}>
          <button className="btn-primary" onClick={() => downloadExcelFormatted(filtered, `students-${month}.xlsx`, ['Aluno','Nif','Horas Realizadas Dias','Horas Realizadas FimDeSemana','Total a Pagar'], ['Aluno','NIF','Horas Realizadas (dias úteis)','Horas Realizadas (fins de semana)','Total a Pagar'])}>
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
  const [_weekendRate, _setWeekendRate] = useState(43.20)
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
        const wk = parseFloat(map['class_price_weekday'])
        const we = parseFloat(map['class_price_weekend'])
        if (!Number.isNaN(wk)) setWeekdayRate(wk)
        if (!Number.isNaN(we)) _setWeekendRate(we)
      } catch {
        // ignore
      }
    })()
    return () => { mounted = false }
  }, [])
  useEffect(() => {
    let mounted = true
    void (async () => {
      try {
        const res = await getBillingCoachesAll(month)
        if (!mounted) return
        const items = Array.isArray(res) ? res : res.items ?? res.Items ?? []
        setRows(items.map(i => {
          let hoursWeekday = Number(readProp(i, ['hoursWeekday','HoursWeekday','hours_weekday']) ?? 0)
          let hoursWeekend = Number(readProp(i, ['hoursWeekend','HoursWeekend','hours_weekend']) ?? 0)
          const totalAmount = Number(readProp(i, ['totalAmount','TotalAmount','total_amount']) ?? 0)
          const nif = readProp(i, ['nif','Nif','coachNavigation.personInfo.nif','coach.personInfo.nif']) ?? ''
          if (totalAmount && !hoursWeekday && !hoursWeekend) {
            const est = weekdayRate > 0 ? +(totalAmount / weekdayRate) : 0
            hoursWeekday = Math.round(est * 100) / 100
            console.warn('Coach billing: estimated hoursWeekday from total using weekdayRate', { estimated: hoursWeekday, totalAmount, weekdayRate, item: i })
          }
          return {
            Professor: resolveName(i, ['coachName','CoachName','name','coachName']),
            Nif: nif,
            'Horas Realizadas Dias': hoursWeekday,
            'Horas Realizadas FimDeSemana': hoursWeekend,
            'Total a Pagar': totalAmount
          }
        }))
      } catch (err) {
        console.error('Erro fetch coaches for billing', err)
      }
    })()
    return () => { mounted = false }
  }, [month])

  const columns = [
    { key: 'Professor', label: 'Professor' },
    { key: 'Nif', label: 'NIF' },
    { key: 'Horas Realizadas Dias', label: 'Horas Realizadas (dias úteis)' },
    { key: 'Horas Realizadas FimDeSemana', label: 'Horas Realizadas (fins de semana)' },
    { key: 'Total a Pagar', label: 'Total a Receber' }
  ]
  const filtered = rows.filter(r => search ? String(r.Professor).toLowerCase().includes(search.toLowerCase()) : true)

  // simple aggregate for coaches
  const totalCoaches = rows.length
  const receitaTotal = rows.reduce((s, r) => s + (Number(r['Total a Pagar']) || 0), 0)

  return (
    <div className="billing-section">
      <div className="cards-row">
        <div className="card">
          <div className="card-title">Total Professores</div>
          <div className="card-value">{totalCoaches}</div>
        </div>
        <div className="card">
          <div className="card-title">Receita Total</div>
          <div className="card-value card-value-money">{formatMoney(receitaTotal)}</div>
        </div>
      </div>

      <div className="controls-row">
        <div className="search-box">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M21 21l-4.35-4.35" stroke="#9CA3AF" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/><path d="M11 19a8 8 0 1 1 0-16 8 8 0 0 1 0 16z" stroke="#9CA3AF" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg>
          <input placeholder="Nome do professor..." value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>

        <div style={{ marginLeft: 'auto' }}>
          <button className="btn-primary" onClick={() => downloadExcelFormatted(filtered, `coaches-${month}.xlsx`, ['Professor','Nif','Horas Realizadas Dias','Horas Realizadas FimDeSemana','Total a Pagar'], ['Professor','NIF','Horas Realizadas (dias úteis)','Horas Realizadas (fins de semana)','Total a Receber'])}>
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
  const [month, setMonth] = useState(() => new Date().toISOString().slice(0,7))

  return (
    <section className="dashboard-page-card">
      <h2>Faturação</h2>
      <p>Gerir tabelas de alunos e professores</p>

      <div style={{ marginTop: 18 }}>
        <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginBottom: 12 }}>
          <button className={`pill ${tab === 'students' ? 'active' : ''}`} onClick={() => setTab('students')}>Tabela de Alunos</button>
          <button className={`pill ${tab === 'coaches' ? 'active' : ''}`} onClick={() => setTab('coaches')}>Tabela de Professores</button>
          <div style={{ marginLeft: 'auto', display: 'flex', gap: 8, alignItems: 'center' }}>
            <input type="month" value={month} onChange={(e) => setMonth(e.target.value)} />
          </div>
        </div>

        <div style={{ marginTop: 12 }}>
          {tab === 'students' ? <StudentsTable month={month} /> : <CoachesTable month={month} />}
        </div>
      </div>
    </section>
  )
}

export default StaffBillingPage
