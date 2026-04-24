import { get } from '../api/client'

export async function getBillingStudents(month, search = '', page = 1, pageSize = 25) {
  const q = new URLSearchParams({ month, search, page: String(page), pageSize: String(pageSize) })
  return get(`/api/staff/billing/students?${q.toString()}`)
}

export async function getBillingCoaches(month, search = '', page = 1, pageSize = 25) {
  const q = new URLSearchParams({ month, search, page: String(page), pageSize: String(pageSize) })
  return get(`/api/staff/billing/coaches?${q.toString()}`)
}

// helper to request full data for export (no pagination)
export async function getBillingStudentsAll(month, search = '') {
  const q = new URLSearchParams({ month, search, page: '1', pageSize: '1000' })
  return get(`/api/staff/billing/students?${q.toString()}`)
}

export async function getBillingCoachesAll(month, search = '') {
  const q = new URLSearchParams({ month, search, page: '1', pageSize: '1000' })
  return get(`/api/staff/billing/coaches?${q.toString()}`)
}
