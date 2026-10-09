import axios from 'axios'

export function getApiErrorMessage(error: unknown, fallback = 'Κάτι πήγε στραβά.'): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data

    // Τα δικά μας 400/404/409: { message: "..." }
    if (data?.message) return data.message

    // Validation errors (ValidationProblem): { errors: { Name: ["..."], ... } }
    if (data?.errors) {
      return Object.values(data.errors as Record<string, string[]>).flat().join(' ')
    }

    if (error.response?.status === 403) {
      return 'Δεν έχεις δικαίωμα για αυτή την ενέργεια.'
    }
  }
  return fallback
}