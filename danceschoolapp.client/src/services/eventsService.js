import { get, post, put, patch, del } from '../api/client'

// Staff — todos os eventos
export const getAllEvents = () => get('/api/events')

// Coach/Parent — só ativos
export const getActiveEvents = () => get('/api/events/active')

export const getEventById = (id) => get(`/api/events/${id}`)
export const createEvent = (body) => post('/api/events', body)
export const updateEvent = (id, body) => put(`/api/events/${id}`, body)
export const activateEvent = (id) => patch(`/api/events/${id}/activate`)
export const deactivateEvent = (id) => patch(`/api/events/${id}/deactivate`)
export const deleteEvent = (id) => del(`/api/events/${id}`)