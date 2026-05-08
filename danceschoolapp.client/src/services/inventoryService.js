import { get, post, patch, del, upload } from '@/api/client'

// ── Items (staff direct endpoints) ────────────────────────────────────────────

export function getItems({ fromSchool, search, categoryId, page = 1, pageSize = 12 } = {}) {
    const p = new URLSearchParams({ page, pageSize })
    if (fromSchool !== undefined && fromSchool !== null) p.set('fromSchool', fromSchool)
    if (search)     p.set('search', search)
    if (categoryId) p.set('categoryId', categoryId)
    return get(`/api/items?${p}`)
}

export function getItem(id) {
    return get(`/api/items/${id}`)
}

export function createSchoolItem(body) {
    return post('/api/items/school', body)
}

export function createPersonalItem(body) {
    return post('/api/items/personal', body)
}

export function updateItem(id, body) {
    return patch(`/api/items/${id}`, body)
}

export function deleteItem(id) {
    return del(`/api/items/${id}`)
}

// ── Images ─────────────────────────────────────────────────────────────────────

export function uploadItemImage(id, file) {
    const fd = new FormData()
    fd.append('file', file)
    return upload(`/api/items/${id}/images`, fd)
}

export function removeItemImage(id, imageId) {
    return del(`/api/items/${id}/images/${imageId}`)
}

// ── Variants ───────────────────────────────────────────────────────────────────

export function getVariants(id) {
    return get(`/api/items/${id}/variants`)
}

export function createVariant(id, body) {
    return post(`/api/items/${id}/variants`, body)
}

export function updateVariant(id, variantId, body) {
    return patch(`/api/items/${id}/variants/${variantId}`, body)
}

export function deleteVariant(id, variantId) {
    return del(`/api/items/${id}/variants/${variantId}`)
}

// ── Categories ─────────────────────────────────────────────────────────────────

export function getCategories() {
    return get('/api/item-categories')
}

export function createCategory(body) {
    return post('/api/item-categories', body)
}

export function deleteCategory(id) {
    return del(`/api/item-categories/${id}`)
}

// ── Requisitions ───────────────────────────────────────────────────────────────

export function getRequisitions() {
    return get('/api/requisitions')
}

export function createRequisition(body) {
    return post('/api/requisitions', body)
}

export function reviewRequisition(id, body) {
    return patch(`/api/requisitions/${id}/review`, body)
}

export function returnRequisition(id, body) {
    return patch(`/api/requisitions/${id}/return`, body)
}

export function cancelRequisition(id) {
    return del(`/api/requisitions/${id}`)
}

// ── Parent portal inventory endpoints ─────────────────────────────────────────

export function getSchoolInventory({ categoryId, search, page = 1, pageSize = 12 } = {}) {
    const p = new URLSearchParams({ page, pageSize })
    if (search)     p.set('search', search)
    if (categoryId) p.set('categoryId', categoryId)
    return get(`/api/ee/inventory/school?${p}`)
}

export function getCommunityInventory({ categoryId, maxPrice, search, page = 1, pageSize = 12 } = {}) {
    const p = new URLSearchParams({ page, pageSize })
    if (search)   p.set('search', search)
    if (categoryId) p.set('categoryId', categoryId)
    if (maxPrice)   p.set('maxPrice', maxPrice)
    return get(`/api/ee/inventory/community?${p}`)
}
