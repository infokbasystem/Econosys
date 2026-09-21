import { useEffect, useState } from 'react'
import apiClient from '../../../config/apiClient'
import LabeledInput from '../../../components/LabeledInput'

const COMPANY_ID = 1

const defaultForm = {
  handlingTimesGoalNrOfDays: '',
  handlingTimesGoalMaxNrOfDays: '',
  handlingTimesPercentHandledUnderGoalNrOfDays: '',
  handlingTimesThresholdNrOfDays: '',
}

const parseNullableInt = (value) => {
  if (value === '' || value === null || value === undefined) return null
  const parsed = Number.parseInt(String(value), 10)
  return Number.isNaN(parsed) ? null : parsed
}

const parseNullableDecimal = (value) => {
  if (value === '' || value === null || value === undefined) return null
  const normalized = String(value).replace(',', '.')
  const parsed = Number.parseFloat(normalized)
  return Number.isNaN(parsed) ? null : parsed
}

const mapDtoToForm = (dto) => ({
  handlingTimesGoalNrOfDays: dto?.handlingTimesGoalNrOfDays?.toString() ?? '',
  handlingTimesGoalMaxNrOfDays: dto?.handlingTimesGoalMaxNrOfDays?.toString() ?? '',
  handlingTimesPercentHandledUnderGoalNrOfDays: dto?.handlingTimesPercentHandledUnderGoalNrOfDays?.toString() ?? '',
  handlingTimesThresholdNrOfDays: dto?.handlingTimesThresholdNrOfDays?.toString() ?? '',
})

const mapFormToPayload = (form) => ({
  propertiesToUpdate: [
    'handlingTimesGoalNrOfDays',
    'handlingTimesGoalMaxNrOfDays',
    'handlingTimesPercentHandledUnderGoalNrOfDays',
    'handlingTimesThresholdNrOfDays',
  ],
  handlingTimesGoalNrOfDays: parseNullableInt(form.handlingTimesGoalNrOfDays),
  handlingTimesGoalMaxNrOfDays: parseNullableInt(form.handlingTimesGoalMaxNrOfDays),
  handlingTimesPercentHandledUnderGoalNrOfDays: parseNullableDecimal(form.handlingTimesPercentHandledUnderGoalNrOfDays),
  handlingTimesThresholdNrOfDays: parseNullableInt(form.handlingTimesThresholdNrOfDays),
})

function HandlingTimesSettings() {
  const [form, setForm] = useState(defaultForm)
  const [hasExisting, setHasExisting] = useState(false)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)
  const [message, setMessage] = useState(null)

  const updateField = (field, value) => {
    setForm((prev) => ({ ...prev, [field]: value }))
  }

  const loadSettings = async () => {
    setIsLoading(true)
    setMessage(null)

    try {
      const response = await apiClient.get(`/companysettings/${COMPANY_ID}`)
      setForm(mapDtoToForm(response.data))
      setHasExisting(true)
    } catch (error) {
      if (error.response?.status === 404) {
        setForm(defaultForm)
        setHasExisting(false)
      } else {
        setMessage({ type: 'error', text: 'Kunde inte hämta inställningarna.' })
      }
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    loadSettings()
  }, [])

  const handleSave = async () => {
    setIsSaving(true)
    setMessage(null)

    try {
      const payload = mapFormToPayload(form)
      const response = hasExisting
        ? await apiClient.put(`/companysettings/${COMPANY_ID}`, payload)
        : await apiClient.post('/companysettings', payload)

      setForm(mapDtoToForm(response.data))
      setHasExisting(true)
      setMessage({ type: 'success', text: 'Hanteringstidsinställningarna sparades.' })
    } catch (error) {
      console.error('Failed to save handling time settings:', error)
      setMessage({ type: 'error', text: 'Kunde inte spara inställningarna.' })
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <div className="relative flex h-full flex-col">
      <h2 className="ml-10 pb-2 pt-2 text-sm text-gray-700">Hanteringstider / Inställningar</h2>

      <div className="flex h-full min-w-0 items-stretch">
        <div className="flex-1 min-w-0 overflow-x-auto px-10 py-2">
          <div className="mb-6 mt-1 flex items-center gap-5">
            <button
              type="button"
              onClick={handleSave}
              disabled={isSaving || isLoading}
              className="shadow-md/30 bg-lime-700 px-5 p-[5px] text-xs text-white hover:bg-lime-900 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isSaving ? 'Sparar...' : 'Spara'}
            </button>
          </div>

          {isLoading ? (
            <p className="mt-4 text-xs text-gray-600">Laddar inställningar...</p>
          ) : (
            <div className="w-[220px] max-w-full">
              <LabeledInput
                label="Mål antal dagar"
                value={form.handlingTimesGoalNrOfDays}
                onChange={(value) => updateField('handlingTimesGoalNrOfDays', value)}
                labelWidth="w-40"
                margintop="0"
                type="number"
                integerOnly
              />
              <LabeledInput
                label="Mål max antal dagar"
                value={form.handlingTimesGoalMaxNrOfDays}
                onChange={(value) => updateField('handlingTimesGoalMaxNrOfDays', value)}
                labelWidth="w-40"
                margintop="0"
                type="number"
                integerOnly
              />
              <LabeledInput
                label="Andel hanterad under mål (%)"
                value={form.handlingTimesPercentHandledUnderGoalNrOfDays}
                onChange={(value) => updateField('handlingTimesPercentHandledUnderGoalNrOfDays', value)}
                labelWidth="w-40"
                margintop="0"
                type="number"
                decimals={2}
              />
              <LabeledInput
                label="Tröskel antal dagar"
                value={form.handlingTimesThresholdNrOfDays}
                onChange={(value) => updateField('handlingTimesThresholdNrOfDays', value)}
                labelWidth="w-40"
                margintop="0"
                type="number"
                integerOnly
              />
            </div>
          )}
        </div>

        <div className="mb-5 flex w-80 shrink-0 flex-col border-l border-gray-300 pl-4 py-2">
          <h2 className="mt-1 text-center text-sm text-gray-700">Meddelanden</h2>
          {!message ? (
            <p className="mt-4 text-center text-xs font-light text-gray-600">Inga meddelanden</p>
          ) : (
            <ul className="mt-2 space-y-2">
              <li className={`rounded border border-gray-200 p-2 text-center text-xs ${message.type === 'error' ? 'bg-red-100 text-red-700' : 'bg-green-100 text-green-700'}`}>
                {message.text}
              </li>
            </ul>
          )}
        </div>
      </div>
    </div>
  )
}

export default HandlingTimesSettings
