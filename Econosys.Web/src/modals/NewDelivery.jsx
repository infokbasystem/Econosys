import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { useNavigate } from 'react-router-dom'
import { FilePlus2, MapPinPlus, Plus, Search, Trash2 } from 'lucide-react'

import ActionButton from '../components/ActionButton'
import LabeledCheckbox from '../components/LabeledCheckbox'
import LabeledDatePicker from '../components/LabeledDatePicker'
import LabeledInput from '../components/LabeledInput'
import LabeledSelect from '../components/LabeledSelect'
import SegmentedFilter from '../components/SegmentedFilter'
import apiClient from '../config/apiClient'
import { getSharedRequest } from '../helpers/sharedRequest'

const deliveryTypes = [
  { value: 1, label: 'Leverans till lager' },
  { value: 2, label: 'Leverans från lager' },
  { value: 3, label: 'Direktleverans' },
]

const dateInputToday = () => {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
}

const emptyForm = () => ({
  deliveryDate: dateInputToday(),
  producedEdition: null,
  deliveredEdition: null,
  deliveredPallets: null,
  quantityInputMode: 'items',
  editionPerPallet: null,
  inventoryId: '',
  palletFormatId: '',
  palletLength: null,
  palletWidth: null,
  palletHeight: null,
  palletIsStackable: false,
  palletCalcFactor: null,
  isDelivered: true,
  callOff: '',
})

const mapOrderInfoToForm = (info) => ({
  ...emptyForm(),
  producedEdition: info?.producedEdition ?? null,
  deliveredEdition: null,
  editionPerPallet: info?.editionPerPallet ?? null,
  inventoryId: info?.inventoryId == null ? '' : String(info.inventoryId),
  palletFormatId: info?.customerPalletFormatId == null ? '' : String(info.customerPalletFormatId),
  palletLength: info?.palletLength ?? null,
  palletWidth: info?.palletWidth ?? null,
  palletHeight: info?.palletHeight ?? null,
  palletIsStackable: Boolean(info?.palletIsStackable),
  palletCalcFactor: info?.palletCalcFactor ?? null,
})

let googleMapsLoaderPromise = null

const loadGoogleMapsApi = (apiKey) => {
  if (window.google?.maps) return Promise.resolve(window.google.maps)
  if (googleMapsLoaderPromise) return googleMapsLoaderPromise

  googleMapsLoaderPromise = new Promise((resolve, reject) => {
    const existingScript = document.getElementById('google-maps-js-api')
    if (existingScript) {
      existingScript.addEventListener('load', () => resolve(window.google.maps), { once: true })
      existingScript.addEventListener('error', () => reject(new Error('Google Maps kunde inte laddas.')), { once: true })
      return
    }

    const script = document.createElement('script')
    script.id = 'google-maps-js-api'
    script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}`
    script.async = true
    script.defer = true
    script.onload = () => resolve(window.google.maps)
    script.onerror = () => reject(new Error('Google Maps kunde inte laddas.'))
    document.body.appendChild(script)
  }).catch((error) => {
    googleMapsLoaderPromise = null
    throw error
  })

  return googleMapsLoaderPromise
}

const formatAmount = (value) => {
  if (value == null || !Number.isFinite(Number(value))) return ''
  return new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 2 }).format(Number(value))
}

const getApiError = (error, fallback) => {
  const responseData = error?.response?.data
  if (typeof responseData === 'string') return responseData
  return responseData?.message ?? responseData?.title ?? fallback
}

const calculateDeliveryParts = (form, isPallet, customSlattDeliveries) => {
  const editionPerPallet = Number(form.editionPerPallet ?? 0)
  const deliveredPallets = Number(form.deliveredPallets ?? 0)
  const enteredItems = Number(form.deliveredEdition ?? 0)
  const deliveredEdition = isPallet && editionPerPallet > 0 && form.quantityInputMode === 'pallets'
    ? deliveredPallets * editionPerPallet
    : enteredItems
  if (!isPallet) {
    return {
      totalItems: deliveredEdition,
      mainItems: deliveredEdition,
      mainPallets: 0,
      slattDeliveries: [],
    }
  }

  if (editionPerPallet <= 0) {
    return {
      totalItems: deliveredEdition,
      mainItems: deliveredEdition,
      mainPallets: deliveredPallets,
      slattDeliveries: [],
    }
  }

  const fullPallets = form.quantityInputMode === 'pallets'
    ? deliveredPallets
    : Math.floor(deliveredEdition / editionPerPallet)
  let mainItems = fullPallets * editionPerPallet
  let automaticRemainder = deliveredEdition - mainItems

  const customSlattRows = (Array.isArray(customSlattDeliveries) ? customSlattDeliveries : [])
    .map((delivery, index) => ({
      ...delivery,
      customIndex: index + 1,
      nrOfItems: Math.max(0, Number(delivery.nrOfItems) || 0),
    }))
    .filter((delivery) => delivery.nrOfItems > 0)

  let customItemsToTakeFromMain = 0
  let remainderAvailable = automaticRemainder
  const slattDeliveries = customSlattRows.map((delivery) => {
    const takeFromRemainder = Math.min(remainderAvailable, delivery.nrOfItems)
    remainderAvailable -= takeFromRemainder
    customItemsToTakeFromMain += delivery.nrOfItems - takeFromRemainder
    return {
      nrOfItems: delivery.nrOfItems,
      nrOfPallets: 1,
      editionPerPallet: delivery.nrOfItems,
      isCustom: true,
      id: delivery.id,
    }
  })

  mainItems = Math.max(0, mainItems - customItemsToTakeFromMain)
  const mainPallets = Math.floor(mainItems / editionPerPallet)
  const newAutomaticRemainder = remainderAvailable + (mainItems - mainPallets * editionPerPallet)
  const normalizedMainItems = mainPallets * editionPerPallet

  if (newAutomaticRemainder > 0) {
    slattDeliveries.unshift({
      nrOfItems: newAutomaticRemainder,
      nrOfPallets: 1,
      editionPerPallet: newAutomaticRemainder,
      isCustom: false,
    })
  }

  return {
    totalItems: deliveredEdition,
    mainItems: normalizedMainItems,
    mainPallets,
    slattDeliveries,
  }
}

const calculateDistance = async (maps, from, to) => {
  if (!maps || !from || !to) return null

  try {
    const service = new maps.DirectionsService()
    const result = await service.route({
      origin: { lat: Number(from.latitudeStart), lng: Number(from.longitudeStart) },
      destination: { lat: Number(to.latitudeEnd), lng: Number(to.longitudeEnd) },
      travelMode: maps.TravelMode.DRIVING,
    })
    const meters = result.routes?.[0]?.legs?.reduce((sum, leg) => sum + (leg.distance?.value ?? 0), 0)
    return Number.isFinite(meters) && meters > 0 ? meters / 1000 : null
  } catch {
    return null
  }
}

const NewDelivery = () => {
  const navigate = useNavigate()
  const [deliveryType, setDeliveryType] = useState(1)
  const [orderSearch, setOrderSearch] = useState('')
  const [orderInfo, setOrderInfo] = useState(null)
  const [form, setForm] = useState(emptyForm)
  const [customSlattDeliveries, setCustomSlattDeliveries] = useState([])
  const [inventoryOptions, setInventoryOptions] = useState([])
  const [palletFormatOptions, setPalletFormatOptions] = useState([])
  const [deliveryLegs, setDeliveryLegs] = useState([])
  const [positionSearch, setPositionSearch] = useState('')
  const [positionResults, setPositionResults] = useState([])
  const [selectedLegSortOrder, setSelectedLegSortOrder] = useState('1')
  const [selectedFile, setSelectedFile] = useState(null)
  const [savedDelivery, setSavedDelivery] = useState(null)
  const [uploadedFile, setUploadedFile] = useState(null)
  const [searchError, setSearchError] = useState('')
  const [formError, setFormError] = useState('')
  const [isSearching, setIsSearching] = useState(false)
  const [isLoadingOrder, setIsLoadingOrder] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isLoadingLegs, setIsLoadingLegs] = useState(false)
  const [isLoadingPositions, setIsLoadingPositions] = useState(false)
  const [googleApiKey, setGoogleApiKey] = useState('')
  const [mapError, setMapError] = useState('')
  const [isMapReady, setIsMapReady] = useState(false)
  const mapContainerRef = useRef(null)
  const mapRuntimeRef = useRef({ map: null, renderers: [] })
  const fileInputRef = useRef(null)

  const isPallet = Boolean(orderInfo?.isPallet)
  const deliveryParts = calculateDeliveryParts(form, isPallet, customSlattDeliveries)

  const closeModal = () => navigate('/logistics/deliveries', { replace: true })

  useEffect(() => {
    const handleKeyDown = (event) => {
      if (event.key === 'Escape' && !isSubmitting) navigate('/logistics/deliveries', { replace: true })
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [isSubmitting, navigate])

  useEffect(() => {
    let isActive = true
    const loadOptions = async () => {
      const [inventories, formats, settings] = await Promise.allSettled([
        getSharedRequest('inventories:new-delivery-options', () => apiClient.post('/inventories/search', {
          pagination: { pageNumber: 1, pageSize: 1000 },
          orderBy: [{ field: 'name', direction: 'asc' }],
        })),
        getSharedRequest('palletformats:new-delivery-options', () => apiClient.get('/palletformats/search')),
        getSharedRequest('companysettings:1', () => apiClient.get('/companysettings/1')),
      ])

      if (!isActive) return
      if (inventories.status === 'fulfilled') {
        const items = inventories.value?.data?.items ?? []
        setInventoryOptions(items.filter((item) => item.isInventory).map((item) => ({
          id: String(item.id),
          name: item.name ?? '',
        })))
      }
      if (formats.status === 'fulfilled') {
        setPalletFormatOptions((formats.value?.data ?? []).map((item) => ({
          id: String(item.id),
          name: item.name ?? '',
        })))
      }
      if (settings.status === 'fulfilled') {
        setGoogleApiKey(String(settings.value?.data?.googleApiKey ?? ''))
      }
    }

    loadOptions()
    return () => { isActive = false }
  }, [])

  useEffect(() => {
    const term = orderSearch.trim()
    if (term.length < 3) {
      setSearchError('')
      setIsSearching(false)
      return undefined
    }

    let isActive = true
    const timer = window.setTimeout(async () => {
      setIsSearching(true)
      setSearchError('')
      try {
        const requestBody = {
          filter: {
            conditions: [{
              field: 'CustomerOrderNr',
              operator: 'eq',
              value: term,
            }],
          },
          pagination: { pageNumber: 1, pageSize: 2 },
        }
        const response = await getSharedRequest(
          `customerorders:new-delivery-exact:${term}`,
          () => apiClient.post('/customerorders/search', requestBody),
        )
        if (!isActive) return

        const exactMatches = response?.data?.items ?? []
        if (exactMatches.length === 1) {
          setIsLoadingOrder(true)
          const orderResponse = await apiClient.get(`/deliveries/new/order/${exactMatches[0].id}`)
          if (!isActive) return
          const orderInfoResult = orderResponse?.data
          setOrderInfo(orderInfoResult)
          setForm(mapOrderInfoToForm(orderInfoResult))
          setDeliveryType(
            orderInfoResult?.inventoryId != null && orderInfoResult.inventoryIsInventory === false
              ? 3
              : 1,
          )
          setSearchError('')
          return
        }

        setOrderInfo(null)
        setSearchError(exactMatches.length > 1
          ? 'Flera order hittades med samma ordernummer.'
          : 'Ingen order hittades med det ordernumret.')
      } catch (error) {
        if (isActive) {
          setOrderInfo(null)
          setSearchError(getApiError(error, 'Kunde inte söka efter order.'))
        }
      } finally {
        if (isActive) {
          setIsSearching(false)
          setIsLoadingOrder(false)
        }
      }
    }, 300)

    return () => {
      isActive = false
      window.clearTimeout(timer)
    }
  }, [orderSearch])

  useEffect(() => {
    if (!orderInfo) {
      setDeliveryLegs([])
      return undefined
    }

    let isActive = true
    setIsLoadingLegs(true)
    setMapError('')
    const params = {
      customerOrderId: orderInfo.customerOrderId,
      deliveryType,
      inventoryId: form.inventoryId ? Number(form.inventoryId) : null,
    }
    const requestKey = `deliveries:new-legs:${JSON.stringify(params)}`

    getSharedRequest(requestKey, () => apiClient.get('/deliveries/new/legs', { params }))
      .then((response) => {
        if (!isActive) return
        const legs = Array.isArray(response?.data) ? response.data : []
        setDeliveryLegs(legs.map((leg, index) => ({ ...leg, sortOrder: index + 1 })))
        setSelectedLegSortOrder(legs.length ? '1' : '')
      })
      .catch((error) => {
        console.error('Failed to load new delivery legs:', error)
        if (isActive) setDeliveryLegs([])
      })
      .finally(() => {
        if (isActive) setIsLoadingLegs(false)
      })

    return () => { isActive = false }
  }, [orderInfo, deliveryType, form.inventoryId])

  useEffect(() => {
    const term = positionSearch.trim()
    if (term.length < 2) {
      setPositionResults([])
      setIsLoadingPositions(false)
      return undefined
    }

    let isActive = true
    const timer = window.setTimeout(async () => {
      setIsLoadingPositions(true)
      try {
        const response = await apiClient.get('/deliveries/positions', { params: { search: term } })
        if (isActive) setPositionResults(response?.data ?? [])
      } catch (error) {
        console.error('Failed to search delivery positions:', error)
        if (isActive) setPositionResults([])
      } finally {
        if (isActive) setIsLoadingPositions(false)
      }
    }, 250)

    return () => {
      isActive = false
      window.clearTimeout(timer)
    }
  }, [positionSearch])

  useEffect(() => {
    const runtime = mapRuntimeRef.current
    if (!orderInfo) return undefined
    setIsMapReady(false)
    if (!googleApiKey) {
      setMapError('Google API-nyckel saknas i Företagsinfo.')
      return undefined
    }

    let isActive = true
    const renderRoutes = async () => {
      try {
        const maps = await loadGoogleMapsApi(googleApiKey)
        if (!isActive || !mapContainerRef.current) return

        const map = runtime.map ?? new maps.Map(mapContainerRef.current, {
          disableDefaultUI: false,
          zoom: 5,
          center: { lat: 59.3293, lng: 18.0686 },
        })
        runtime.map = map
        runtime.renderers.forEach((renderer) => renderer.setMap(null))
        runtime.renderers = []

        const bounds = new maps.LatLngBounds()
        let hasBounds = false
        const directionsService = new maps.DirectionsService()

        for (const leg of deliveryLegs) {
          const origin = {
            lat: Number(leg.latitudeStart),
            lng: Number(leg.longitudeStart),
          }
          const destination = {
            lat: Number(leg.latitudeEnd),
            lng: Number(leg.longitudeEnd),
          }
          if (![origin.lat, origin.lng, destination.lat, destination.lng].every(Number.isFinite)) continue

          const result = await directionsService.route({
            origin,
            destination,
            travelMode: maps.TravelMode.DRIVING,
          })
          if (!isActive) return

          const meters = result.routes?.[0]?.legs?.reduce((sum, routeLeg) => sum + (routeLeg.distance?.value ?? 0), 0)
          const distanceKm = Number.isFinite(meters) && meters > 0 ? meters / 1000 : null
          if (distanceKm != null && Math.abs(Number(leg.distanceKm ?? 0) - distanceKm) > 0.01) {
            setDeliveryLegs((current) => {
              const currentLeg = current.find((item) => item.sortOrder === leg.sortOrder)
              if (!currentLeg || Math.abs(Number(currentLeg.distanceKm ?? 0) - distanceKm) <= 0.01) return current
              return current.map((item) => item.sortOrder === leg.sortOrder ? { ...item, distanceKm } : item)
            })
          }

          const renderer = new maps.DirectionsRenderer({
            map,
            suppressMarkers: true,
            preserveViewport: true,
            polylineOptions: { strokeColor: '#b45309' },
          })
          renderer.setDirections(result)
          runtime.renderers.push(renderer)
          bounds.extend(origin)
          bounds.extend(destination)
          hasBounds = true
        }

        if (hasBounds) map.fitBounds(bounds)
        setIsMapReady(true)
        setMapError('')
      } catch (error) {
        console.error('Failed to render new-delivery map:', error)
        if (isActive) {
          setIsMapReady(false)
          setMapError('Kunde inte visa transportben på kartan.')
        }
      }
    }

    renderRoutes()
    return () => {
      isActive = false
      runtime.renderers.forEach((renderer) => renderer.setMap(null))
      runtime.renderers = []
    }
  }, [orderInfo, deliveryLegs, googleApiKey])

  const updateField = (field, value) => {
    setForm((current) => ({ ...current, [field]: value }))
    setFormError('')
  }

  const updatePalletCount = (value) => {
    setForm((current) => {
      const editionPerPallet = Number(current.editionPerPallet ?? 0)
      return {
        ...current,
        quantityInputMode: 'pallets',
        deliveredPallets: value,
        deliveredEdition: value == null || editionPerPallet <= 0 ? null : Number(value) * editionPerPallet,
      }
    })
    setFormError('')
  }

  const updateItemCount = (value) => {
    setForm((current) => {
      const editionPerPallet = Number(current.editionPerPallet ?? 0)
      return {
        ...current,
        quantityInputMode: 'items',
        deliveredEdition: value,
        deliveredPallets: value == null || editionPerPallet <= 0
          ? null
          : Math.ceil(Number(value) / editionPerPallet),
      }
    })
    setFormError('')
  }

  const updateEditionPerPallet = (value) => {
    setForm((current) => {
      const editionPerPallet = Number(value ?? 0)
      const deliveredEdition = current.quantityInputMode === 'pallets'
        ? current.deliveredPallets == null || editionPerPallet <= 0
          ? null
          : Number(current.deliveredPallets) * editionPerPallet
        : current.deliveredEdition
      const deliveredPallets = current.quantityInputMode === 'items'
        ? current.deliveredEdition == null || editionPerPallet <= 0
          ? null
          : Math.ceil(Number(current.deliveredEdition) / editionPerPallet)
        : current.deliveredPallets
      return { ...current, editionPerPallet: value, deliveredEdition, deliveredPallets }
    })
    setFormError('')
  }

  const addCustomSlatt = () => {
    setCustomSlattDeliveries((current) => [
      ...current,
      { id: crypto.randomUUID(), nrOfItems: null },
    ])
  }

  const updateCustomSlatt = (id, nrOfItems) => {
    setCustomSlattDeliveries((current) => current.map((delivery) => (
      delivery.id === id ? { ...delivery, nrOfItems } : delivery
    )))
    setFormError('')
  }

  const removeCustomSlatt = (id) => {
    setCustomSlattDeliveries((current) => current.filter((delivery) => delivery.id !== id))
    setFormError('')
  }

  const handleInsertWaypoint = async (position) => {
    const legIndex = deliveryLegs.findIndex((leg) => String(leg.sortOrder) === String(selectedLegSortOrder))
    if (legIndex < 0) return

    const currentLeg = deliveryLegs[legIndex]
    const firstLeg = {
      ...currentLeg,
      toPositionId: position.id,
      legToPositionId: position.id,
      toPositionName: position.name,
      toPositionPostalAddress: position.postalAddress,
      latitudeEnd: position.latitude,
      longitudeEnd: position.longitude,
    }
    const secondLeg = {
      ...currentLeg,
      fromPositionId: position.id,
      legFromPositionId: position.id,
      fromPositionName: position.name,
      fromPositionPostalAddress: position.postalAddress,
      latitudeStart: position.latitude,
      longitudeStart: position.longitude,
      sortOrder: currentLeg.sortOrder + 1,
    }
    if (position.latitude == null || position.longitude == null) {
      setFormError('Positionen saknar koordinater och kan inte läggas till på kartan.')
      return
    }
    firstLeg.distanceKm = await calculateDistance(window.google?.maps, currentLeg, firstLeg)
    secondLeg.distanceKm = await calculateDistance(window.google?.maps, secondLeg, currentLeg)

    setDeliveryLegs((current) => [
      ...current.slice(0, legIndex),
      firstLeg,
      secondLeg,
      ...current.slice(legIndex + 1).map((leg) => ({ ...leg, sortOrder: leg.sortOrder + 1 })),
    ])
    setSelectedLegSortOrder(String(firstLeg.sortOrder))
    setPositionSearch('')
    setPositionResults([])
    setFormError('')
  }

  const validateForm = () => {
    if (!orderInfo?.customerOrderId || !orderInfo.supplierOrderId) {
      return 'Välj en order med tillhörande leverantörsorder.'
    }
    if (!form.deliveryDate) return 'Måste ange datum.'
    if (deliveryParts.totalItems == null || deliveryParts.totalItems < 0) {
      return 'Ange en levererad upplaga som inte är negativ.'
    }
    if (isPallet && (form.deliveredPallets == null || Number(form.deliveredPallets) < 0)) {
      return 'Ange antal pall som inte är negativt.'
    }
    if (isPallet && Number(form.editionPerPallet ?? 0) <= 0) {
      return 'Upplaga per pall måste anges för pallorder.'
    }
    if (deliveryParts.mainItems < 0 || deliveryParts.slattDeliveries.some((delivery) => delivery.nrOfItems < 0)) {
      return 'Antalet på leveransen eller slatten får inte vara negativt.'
    }
    if (customSlattDeliveries.some((delivery) => delivery.nrOfItems == null || Number(delivery.nrOfItems) <= 0)) {
      return 'Ange ett positivt antal för varje extra slattleverans.'
    }
    if (customSlattDeliveries.reduce((sum, delivery) => sum + Number(delivery.nrOfItems ?? 0), 0) > deliveryParts.totalItems) {
      return 'Extra slattleveranser kan inte överstiga levererat antal.'
    }
    if ([1, 2].includes(deliveryType) && !form.inventoryId) {
      return 'Välj lager.'
    }

    const divisor = orderInfo.packagingType === 'BUNT'
      ? Number(orderInfo.nrOfPerBundle ?? 0)
      : orderInfo.packagingType === 'YTTERFORPACKNING'
        ? Number(orderInfo.nrOfPerOuterPackaging ?? 0)
        : 0
    if (divisor > 0 && (deliveryParts.mainItems % divisor !== 0
      || deliveryParts.slattDeliveries.some((delivery) => delivery.nrOfItems % divisor !== 0))) {
      return 'Antalet ej delbart med bunt/ytterförpackning.'
    }

    return ''
  }

  const uploadSelectedFile = async (saved) => {
    if (!selectedFile || !saved || !orderInfo?.supplierOrderId) return
    const formData = new FormData()
    formData.append('file', selectedFile)
    formData.append('supplierOrderId', String(orderInfo.supplierOrderId))
    try {
      const response = await apiClient.post(
        `/deliveries/${saved.type}/${saved.id}/delivery-note`,
        formData,
      )
      setUploadedFile(response?.data ?? { name: selectedFile.name })
      navigate('/logistics/deliveries', { replace: true })
    } catch (error) {
      setFormError(`Leveransen sparades, men följesedeln kunde inte laddas upp: ${getApiError(error, 'Kontrollera filen och försök igen.')}`)
    }
  }

  const handleSave = async () => {
    if (isSubmitting) return
    if (savedDelivery) {
      if (selectedFile && !uploadedFile) {
        setIsSubmitting(true)
        await uploadSelectedFile(savedDelivery)
        setIsSubmitting(false)
      }
      return
    }

    const validationError = validateForm()
    if (validationError) {
      setFormError(validationError)
      return
    }

    setIsSubmitting(true)
    setFormError('')
    try {
      const payload = {
        deliveryType,
        customerOrderId: orderInfo.customerOrderId,
        producedEdition: form.producedEdition == null ? null : Number(form.producedEdition),
        deliveryDate: form.deliveryDate,
        nrOfItems: deliveryParts.mainItems,
        nrOfPallets: isPallet ? deliveryParts.mainPallets : null,
        callOff: form.callOff.trim() || null,
        inventoryId: form.inventoryId ? Number(form.inventoryId) : null,
        palletFormatId: form.palletFormatId ? Number(form.palletFormatId) : null,
        palletIsStackable: form.palletIsStackable,
        palletLength: form.palletLength == null ? null : Number(form.palletLength),
        palletWidth: form.palletWidth == null ? null : Number(form.palletWidth),
        palletHeight: form.palletHeight == null ? null : Number(form.palletHeight),
        editionPerPallet: form.editionPerPallet == null ? null : Number(form.editionPerPallet),
        palletCalcFactor: form.palletCalcFactor == null ? null : Number(form.palletCalcFactor),
        isDelivered: form.isDelivered,
        slattDeliveries: deliveryParts.slattDeliveries.map((delivery) => ({
          nrOfItems: delivery.nrOfItems,
          nrOfPallets: delivery.nrOfPallets,
          editionPerPallet: delivery.editionPerPallet,
          isAutomaticRemainder: !delivery.isCustom,
        })),
        deliveryLegs: deliveryLegs.map((leg, index) => ({
          fromPositionId: leg.fromPositionId ?? null,
          toPositionId: leg.toPositionId ?? null,
          legFromPositionId: leg.legFromPositionId ?? null,
          legToPositionId: leg.legToPositionId ?? null,
          positionDistanceId: leg.positionDistanceId ?? null,
          distanceKm: leg.distanceKm ?? null,
          typeOfTransport: leg.typeOfTransport || 'Väg',
          sortOrder: index + 1,
          latitudeStart: leg.latitudeStart ?? null,
          longitudeStart: leg.longitudeStart ?? null,
          latitudeEnd: leg.latitudeEnd ?? null,
          longitudeEnd: leg.longitudeEnd ?? null,
        })),
      }

      const response = await apiClient.post('/deliveries/new', payload)
      const saved = response?.data
      setSavedDelivery(saved)
      if (selectedFile) {
        await uploadSelectedFile(saved)
      } else {
        navigate('/logistics/deliveries', { replace: true })
      }
    } catch (error) {
      setFormError(getApiError(error, 'Kunde inte spara leveransen.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  const inventoryItems = [
    { id: '', name: 'Välj lager' },
    ...inventoryOptions,
  ]
  const palletFormatItems = [
    { id: '', name: 'Välj pallformat' },
    ...palletFormatOptions,
  ]

  return createPortal(
    <div
      className="fixed inset-0 z-50"
    // onMouseDown={(event) => {
    //   if (event.target === event.currentTarget && !isSubmitting) closeModal()
    // }}
    >
      <div className="absolute inset-0 bg-black/50 z-40" />
      <div className="relative z-50 flex min-h-screen items-start justify-center pt-16">
        <div
          className="relative bg-white rounded-sm shadow-xl w-full max-w-4xl mx-4 py-6 px-10"
          style={{ background: 'rgb(255, 255, 234)' }}
          onClick={(event) => event.stopPropagation()}
        // aria-labelledby="new-delivery-title"
        // aria-modal="true"
        // className="relative w-full max-w-6xl border border-amber-200 bg-[#ffffea] p-5 shadow-2xl"
        // onMouseDown={(event) => event.stopPropagation()}
        // role="dialog"
        >
          <div className="relative flex items-center justify-center mb-4">
            <h2 className="text-sm font-semibold text-center">Ny leverans</h2>
            <button
              type="button"
              onClick={closeModal}
              disabled={isSubmitting}
              className="absolute right-0 text-gray-400 hover:text-gray-600 text-l leading-none mb-1"
            >
              x
            </button>
          </div>

          <label className="block text-xs text-gray-600 text-center">Ordernummer</label>
          <div className="relative mt-2 w-40 mx-auto">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-gray-400" />
            <input
              type="text"
              autoFocus
              disabled={Boolean(savedDelivery) || isSubmitting}
              value={orderSearch}
              placeholder="Sök ordernummer"
              autoComplete="off"
              className="h-7 w-full rounded-full border border-lime-600 bg-white pl-8 pr-4 text-xs text-gray-700 outline-none transition placeholder:text-gray-500 focus:border-lime-700"
              id="new-delivery-order-search"
              onChange={(event) => {
                setOrderSearch(event.target.value)
                setOrderInfo(null)
                setIsLoadingOrder(false)
                setSearchError('')
                setSavedDelivery(null)
                setUploadedFile(null)
                setCustomSlattDeliveries([])
                setFormError('')
              }}
            />
            {isSearching ? <span className="absolute right-2 top-1/2 -translate-y-1/2 text-tiny text-gray-500">Söker...</span> : null}
          </div>

          {!isSearching && orderSearch.trim().length >= 3 && !orderInfo && searchError ? (
            <p className="mt-2 text-center text-xs text-gray-600">{searchError}</p>
          ) : null}
          {isLoadingOrder ? <p className="mt-2 text-center text-xs text-gray-500">Läser in order...</p> : null}
          {orderInfo ? (
            <div className="mx-auto mt-3 grid w-fit max-w-full gap-x-6 gap-y-1 text-xs text-gray-700 sm:grid-cols-2">
              <span><strong>Order:</strong> {orderInfo.customerOrderNr}</span>
              <span><strong>Lev. order:</strong> {orderInfo.supplierOrderNr}</span>
              <span><strong>Kund:</strong> {orderInfo.customerName || ''}</span>
              <span><strong>Produkt:</strong> {orderInfo.productName}</span>
              <span><strong>Beställt antal:</strong> {formatAmount(orderInfo.orderedEdition)}</span>
              <span><strong>Lev.datum OE:</strong> {orderInfo.customerOrderDeliveryDate ? String(orderInfo.customerOrderDeliveryDate).slice(0, 10) : ''}</span>
            </div>
          ) : null}

          <fieldset
            className={`mt-1 mb-6 min-w-0 border-0 p-0 ${!orderInfo ? 'opacity-50' : ''}`}
            disabled={Boolean(savedDelivery) || isSubmitting || !orderInfo}
          >
            <div className="flex justify-center mt-5 mb-1">
              <SegmentedFilter
                value={deliveryType}
                onChange={setDeliveryType}
                options={deliveryTypes}
                theme="sky"
              />
            </div>
          </fieldset>

          {orderInfo ? (
            <div className="mt-4 grid gap-15 lg:grid-cols-[350px_1fr]">
              <div className="min-w-0 space-y-4">
                <fieldset
                  className="min-w-0 space-y-4"
                  disabled={Boolean(savedDelivery) || isSubmitting}
                >
                  <section className="">
                    {[1, 2].includes(deliveryType) ? (
                      <LabeledSelect
                        items={inventoryItems}
                        label="Lager"
                        labelWidth="w-28"
                        margintop="0"
                        onChange={(value) => updateField('inventoryId', value)}
                        value={form.inventoryId}
                      />
                    ) : null}
                    {deliveryType === 2 ? (
                      <LabeledInput
                        label="Avropsreferens"
                        labelWidth="w-28"
                        margintop="0"
                        maxLength={50}
                        onChange={(value) => updateField('callOff', value)}
                        value={form.callOff}
                      />
                    ) : null}

                    <LabeledDatePicker
                      label="Leveransdatum"
                      labelWidth="w-28"
                      inputWidth="w-36"
                      margintop="4"
                      onChange={(value) => updateField('deliveryDate', value)}
                      value={form.deliveryDate}
                      valueType="input"
                    />
                    <div className='mt-1'>
                      <LabeledCheckbox
                        checked={form.isDelivered}
                        className="sm:col-span-2"
                        label="Levererad"
                        labelPosition="left"
                        labelWidth="w-26"
                        onChange={(value) => updateField('isDelivered', value)}
                      />
                    </div>
                    <LabeledInput
                      integerOnly
                      label="Prod. upplaga"
                      labelWidth="w-28"
                      inputWidth="w-36"
                      margintop="2"
                      onChange={(value) => updateField('producedEdition', value)}
                      type="number"
                      value={form.producedEdition}
                    />
                    <LabeledInput
                      integerOnly
                      label="Lev. upplaga"
                      labelWidth="w-28"
                      inputWidth="w-36"
                      margintop="0"
                      onChange={updateItemCount}
                      type="number"
                      value={form.deliveredEdition}
                    />

                    {isPallet ? (
                      <>
                        <LabeledInput
                          integerOnly
                          label="Antal pall"
                          labelWidth="w-28"
                          inputWidth="w-36"
                          margintop="2"
                          onChange={updatePalletCount}
                          type="number"
                          value={form.deliveredPallets}
                        />
                        <LabeledInput
                          integerOnly
                          label="Upplaga per pall"
                          labelWidth="w-28"
                          inputWidth="w-36"
                          margintop="0"
                          onChange={updateEditionPerPallet}
                          type="number"
                          value={form.editionPerPallet}
                        />
                      </>
                    ) : null}

                    {isPallet && [1, 2].includes(deliveryType) ? (
                      <LabeledSelect
                        items={palletFormatItems}
                        label="Pallformat"
                        labelWidth="w-28"
                        margintop="0"
                        onChange={(value) => updateField('palletFormatId', value)}
                        value={form.palletFormatId}
                      />
                    ) : null}

                    {isPallet ? (
                      <>
                        <div className="flex items-center">
                          <label className="w-28 flex-none text-xs text-gray-700">Pallformat LxBxH</label>
                          <div className="grid flex-1 grid-cols-3 gap-1">
                            <LabeledInput
                              label=""
                              labelWidth="w-0"
                              integerOnly
                              onChange={(value) => updateField('palletLength', value)}
                              value={form.palletLength}
                            />
                            <LabeledInput
                              label=""
                              labelWidth="w-0"
                              integerOnly
                              onChange={(value) => updateField('palletWidth', value)}
                              type="number"
                              value={form.palletWidth}
                            />
                            <LabeledInput
                              label=""
                              labelWidth="w-0"
                              integerOnly
                              onChange={(value) => updateField('palletHeight', value)}
                              type="number"
                              value={form.palletHeight}
                            />
                          </div>
                        </div>
                        <div>
                          <div className="flex items-center">
                            <label className="w-28 flex-none text-xs text-gray-700">Pallfaktor</label>
                            <div className="grid flex-1 grid-cols-3 gap-1">
                              <LabeledInput
                                label=""
                                labelWidth="w-0"
                                integerOnly
                                onChange={(value) => updateField('palletCalcFactor', value)}
                                value={form.palletCalcFactor}
                              />
                            </div>
                          </div>
                          <div className='mt-1'>
                            <LabeledCheckbox
                              checked={form.palletIsStackable}
                              className="sm:col-span-2"
                              label="Staplingsbar"
                              labelPosition="left"
                              labelWidth="w-26"
                              onChange={(value) => updateField('palletIsStackable', value)}
                            />
                          </div>
                        </div>

                      </>
                    ) : null}

                  </section>

                  <section className="mt-3 border-t border-gray-200 pt-3 sm:col-span-2">
                    <div className="flex items-center justify-between gap-3">
                      <span className="text-xs font-semibold text-gray-700">Extra slattleveranser</span>
                      <button
                        className="inline-flex items-center gap-1 text-xs font-medium text-lime-700 hover:text-lime-900 hover:underline"
                        onClick={addCustomSlatt}
                        type="button"
                      >
                        <Plus aria-hidden="true" size={13} />
                        Lägg till slatt
                      </button>
                    </div>
                    {customSlattDeliveries.map((delivery, index) => (
                      <div className="flex items-center gap-2" key={delivery.id}>
                        <LabeledInput
                          integerOnly
                          label={`Slatt ${index + 1}, antal`}
                          labelWidth="w-32"
                          inputWidth="w-36"
                          margintop="0"
                          min={1}
                          onChange={(value) => updateCustomSlatt(delivery.id, value)}
                          type="number"
                          value={delivery.nrOfItems}
                        />
                        <button
                          aria-label={`Ta bort slatt ${index + 1}`}
                          className="inline-flex h-7 w-7 shrink-0 items-center justify-center text-red-700 hover:bg-red-50"
                          onClick={() => removeCustomSlatt(delivery.id)}
                          type="button"
                        >
                          <Trash2 aria-hidden="true" size={14} />
                        </button>
                      </div>
                    ))}
                  </section>

                  <section className="border-y border-amber-200 py-3">
                    <div className="flex flex-wrap items-baseline gap-x-6 gap-y-2 text-xs text-gray-700">
                      <span><strong>Leverans:</strong> {formatAmount(deliveryParts.mainItems)} st / {formatAmount(deliveryParts.mainPallets)} pall</span>
                      {deliveryParts.slattDeliveries.map((delivery, index) => (
                        <span className="font-semibold text-amber-800" key={delivery.id ?? `auto-slatt-${index}`}>
                          <strong>{delivery.isCustom ? `Extra slatt ${delivery.customIndex}` : 'Slatt'}:</strong> {formatAmount(delivery.nrOfItems)} st / {formatAmount(delivery.nrOfPallets)} pall
                        </span>
                      ))}
                      <span><strong>Rest:</strong> {formatAmount(Number(orderInfo.producedEdition ?? 0) - deliveryParts.totalItems)} st</span>
                    </div>
                  </section>

                </fieldset>
              </div>
              <div className="min-w-0 space-y-3">
                <div className="min-w-0 space-y-3">
                  <div className="flex items-center justify-between">
                    {isLoadingLegs ? <span className="text-tiny text-gray-500">Läser in...</span> : null}
                  </div>
                  <div className="relative h-120 overflow-hidden border border-gray-300 bg-slate-50">
                    <div className="absolute inset-0" ref={mapContainerRef} />
                    {mapError ? (
                      <div className="absolute inset-0 flex items-center justify-center bg-slate-50/90 px-4 text-center text-xs text-red-700">
                        {mapError}
                      </div>
                    ) : null}
                    {orderInfo && deliveryLegs.length === 0 && !mapError ? (
                      <div className="absolute inset-0 flex items-center justify-center bg-slate-50/90 px-4 text-center text-xs text-gray-500">
                        Inga transportben hittades för ordern.
                      </div>
                    ) : null}
                    {orderInfo && !isMapReady && !mapError && deliveryLegs.length > 0 ? (
                      <div className="absolute inset-0 flex items-center justify-center bg-slate-50/70 text-xs text-gray-500">
                        Laddar karta...
                      </div>
                    ) : null}
                  </div>

                  <div className="max-h-40 overflow-auto border border-gray-300 bg-white">
                    <table className="w-full text-left text-xs text-gray-700">
                      <thead className="sticky top-0 bg-gray-100 text-tiny uppercase text-gray-500">
                        <tr>
                          <th className="px-2 py-1">Typ</th>
                          <th className="px-2 py-1">Ort från</th>
                          <th className="px-2 py-1">Ort till</th>
                          <th className="px-2 py-1 text-right">Km</th>
                        </tr>
                      </thead>
                      <tbody>
                        {deliveryLegs.map((leg) => (
                          <tr className="border-t border-gray-100" key={`${leg.sortOrder}-${leg.fromPositionId}-${leg.toPositionId}`}>
                            <td className="px-2 py-1">{leg.typeOfTransport || 'Väg'}</td>
                            <td className="max-w-32 truncate px-2 py-1" title={leg.fromPositionPostalAddress || leg.fromPositionName}>
                              {leg.fromPositionPostalAddress || leg.fromPositionName || `Position ${leg.fromPositionId ?? ''}`}
                            </td>
                            <td className="max-w-32 truncate px-2 py-1" title={leg.toPositionPostalAddress || leg.toPositionName}>
                              {leg.toPositionPostalAddress || leg.toPositionName || `Position ${leg.toPositionId ?? ''}`}
                            </td>
                            <td className="px-2 py-1 text-right">{formatAmount(leg.distanceKm)}</td>
                          </tr>
                        ))}
                        {deliveryLegs.length === 0 ? (
                          <tr><td className="px-2 py-3 text-center text-gray-400" colSpan={4}>Inga transportben.</td></tr>
                        ) : null}
                      </tbody>
                    </table>
                  </div>

                  {/* <div className="grid gap-2 sm:grid-cols-[1fr_120px_auto]">
                    <div className="relative">
                      <input
                        className="h-7 w-full border border-gray-300 bg-white px-2 text-xs outline-none focus:border-lime-700 disabled:bg-gray-100"
                        disabled={!orderInfo || !deliveryLegs.length || Boolean(savedDelivery) || isSubmitting}
                        onChange={(event) => setPositionSearch(event.target.value)}
                        placeholder="Sök transportposition"
                        value={positionSearch}
                      />
                      {positionResults.length > 0 ? (
                        <div className="absolute left-0 right-0 top-full z-20 max-h-36 overflow-auto border border-gray-300 bg-white shadow-lg">
                          {positionResults.map((position) => (
                            <button
                              className="flex w-full items-start gap-2 border-b border-gray-100 px-2 py-2 text-left text-xs text-gray-700 hover:bg-lime-50"
                              key={position.id}
                              onClick={() => handleInsertWaypoint(position)}
                              type="button"
                            >
                              <MapPinPlus aria-hidden="true" className="mt-0.5 shrink-0 text-amber-700" size={13} />
                              <span className="min-w-0">
                                <span className="block truncate">{position.name}</span>
                                <span className="block truncate text-gray-500">{position.postalAddress}</span>
                              </span>
                            </button>
                          ))}
                        </div>
                      ) : null}
                      {isLoadingPositions ? <span className="absolute right-2 top-1/2 -translate-y-1/2 text-tiny text-gray-500">Söker...</span> : null}
                    </div>
                    <select
                      aria-label="Lägg till position efter transportben"
                      className="h-7 border border-gray-300 bg-white px-1 text-xs text-gray-700"
                      disabled={!deliveryLegs.length || Boolean(savedDelivery) || isSubmitting}
                      onChange={(event) => setSelectedLegSortOrder(event.target.value)}
                      value={selectedLegSortOrder}
                    >
                      {deliveryLegs.map((leg) => (
                        <option key={leg.sortOrder} value={leg.sortOrder}>Efter ben {leg.sortOrder}</option>
                      ))}
                    </select>
                    <span className="inline-flex items-center justify-center gap-1 border border-gray-200 px-2 text-tiny text-gray-500">
                      Lägg till via sökresultat
                    </span>
                  </div> */}
                </div>
              </div>
            </div>
          ) :
            <div className="flex min-h-40 items-center justify-center border border-dashed border-amber-300 px-5 text-center text-xs text-gray-500">
              Sök ordernummer och välj order för att fylla i leveransen.
            </div>
          }

          {/* <div className="mt-4 grid gap-5 lg:grid-cols-[minmax(0,1fr)_minmax(340px,0.95fr)]">
            <div className="min-w-0 space-y-4">
              {orderInfo ? (
                <fieldset
                  className="min-w-0 space-y-4"
                  disabled={Boolean(savedDelivery) || isSubmitting}
                >
                  <section className="grid gap-x-5 gap-y-2 sm:grid-cols-2">
                    <LabeledDatePicker
                      label="Leveransdatum"
                      labelWidth="w-28"
                      margintop="0"
                      onChange={(value) => updateField('deliveryDate', value)}
                      value={form.deliveryDate}
                      valueType="input"
                    />
                    {[1, 2].includes(deliveryType) ? (
                      <LabeledSelect
                        items={inventoryItems}
                        label="Lager"
                        labelWidth="w-28"
                        margintop="0"
                        onChange={(value) => updateField('inventoryId', value)}
                        value={form.inventoryId}
                      />
                    ) : null}
                    {deliveryType === 2 ? (
                      <LabeledInput
                        label="Avropsreferens"
                        labelWidth="w-28"
                        margintop="0"
                        maxLength={50}
                        onChange={(value) => updateField('callOff', value)}
                        value={form.callOff}
                      />
                    ) : null}
                    <LabeledInput
                      integerOnly
                      label="Prod. upplaga"
                      labelWidth="w-28"
                      margintop="0"
                      onChange={(value) => updateField('producedEdition', value)}
                      type="number"
                      value={form.producedEdition}
                    />
                    <LabeledInput
                      integerOnly
                      label="Lev. upplaga"
                      labelWidth="w-28"
                      margintop="0"
                      onChange={(value) => updateField('deliveredEdition', value)}
                      type="number"
                      value={form.deliveredEdition}
                    />

                    {isPallet ? (
                      <>
                        <LabeledInput
                          integerOnly
                          label="Antal pall"
                          labelWidth="w-28"
                          margintop="0"
                          onChange={(value) => updateField('deliveredPallets', value)}
                          type="number"
                          value={form.deliveredPallets}
                        />
                        <LabeledInput
                          integerOnly
                          label="Upplaga per pall"
                          labelWidth="w-28"
                          margintop="0"
                          onChange={(value) => updateField('editionPerPallet', value)}
                          type="number"
                          value={form.editionPerPallet}
                        />
                      </>
                    ) : null}

                    {isPallet && [1, 2].includes(deliveryType) ? (
                      <LabeledSelect
                        items={palletFormatItems}
                        label="Pallformat"
                        labelWidth="w-28"
                        margintop="0"
                        onChange={(value) => updateField('palletFormatId', value)}
                        value={form.palletFormatId}
                      />
                    ) : null}

                    {isPallet ? (
                      <>
                        <LabeledInput
                          integerOnly
                          label="Pallängd"
                          labelWidth="w-28"
                          margintop="0"
                          onChange={(value) => updateField('palletLength', value)}
                          type="number"
                          value={form.palletLength}
                        />
                        <LabeledInput
                          integerOnly
                          label="Pallbredd"
                          labelWidth="w-28"
                          margintop="0"
                          onChange={(value) => updateField('palletWidth', value)}
                          type="number"
                          value={form.palletWidth}
                        />
                        <LabeledInput
                          integerOnly
                          label="Pallhöjd"
                          labelWidth="w-28"
                          margintop="0"
                          onChange={(value) => updateField('palletHeight', value)}
                          type="number"
                          value={form.palletHeight}
                        />
                        <LabeledInput
                          label="Pallfaktor"
                          labelWidth="w-28"
                          margintop="0"
                          onChange={(value) => updateField('palletCalcFactor', value)}
                          type="number"
                          value={form.palletCalcFactor}
                        />
                        <LabeledCheckbox
                          checked={form.palletIsStackable}
                          className="sm:col-span-2"
                          label="Staplingsbar"
                          labelPosition="left"
                          labelWidth="w-28"
                          onChange={(value) => updateField('palletIsStackable', value)}
                        />
                      </>
                    ) : null}

                    <LabeledCheckbox
                      checked={form.isDelivered}
                      className="sm:col-span-2"
                      label="Levererad"
                      labelPosition="left"
                      labelWidth="w-28"
                      onChange={(value) => updateField('isDelivered', value)}
                    />
                  </section>

                  <section className="border-y border-amber-200 py-3">
                    <div className="flex flex-wrap items-baseline gap-x-6 gap-y-2 text-xs text-gray-700">
                      <span><strong>Leverans:</strong> {formatAmount(deliveryParts.mainItems)} st / {formatAmount(deliveryParts.mainPallets)} pall</span>
                      {deliveryParts.hasSlatt ? (
                        <span className="font-semibold text-amber-800"><strong>Slatt:</strong> {formatAmount(deliveryParts.slattItems)} st / {formatAmount(deliveryParts.slattPallets)} pall</span>
                      ) : null}
                      <span><strong>Rest:</strong> {formatAmount(Number(orderInfo.producedEdition ?? 0) - Number(form.deliveredEdition ?? 0))} st</span>
                    </div>
                  </section>

                  <section className="flex flex-wrap items-center gap-3">
                    <button
                      className="inline-flex items-center gap-2 border border-gray-300 bg-white px-3 py-2 text-xs text-gray-700 hover:bg-gray-50 disabled:opacity-60"
                      disabled={Boolean(savedDelivery) || isSubmitting || !orderInfo.supplierOrderId}
                      onClick={() => fileInputRef.current?.click()}
                      type="button"
                    >
                      <FilePlus2 aria-hidden="true" size={14} />
                      Välj följesedel
                    </button>
                    <input
                      accept=".pdf,.jpg,.jpeg,.png,.tif,.tiff"
                      className="hidden"
                      onChange={(event) => setSelectedFile(event.target.files?.[0] ?? null)}
                      ref={fileInputRef}
                      type="file"
                    />
                    {selectedFile ? <span className="max-w-md truncate text-xs text-gray-600">{selectedFile.name}</span> : null}
                  </section>
                </fieldset>
              ) : (
                <div className="flex min-h-40 items-center justify-center border border-dashed border-amber-300 px-5 text-center text-xs text-gray-500">
                  Sök ordernummer och välj order för att fylla i leveransen.
                </div>
              )}
            </div>

            <div className="min-w-0 space-y-3">
              <div className="flex items-center justify-between">
                <h2 className="text-xs font-semibold text-gray-700">Transportben</h2>
                {isLoadingLegs ? <span className="text-tiny text-gray-500">Läser in...</span> : null}
              </div>
              <div className="relative h-56 overflow-hidden border border-gray-300 bg-slate-50">
                <div className="absolute inset-0" ref={mapContainerRef} />
                {mapError ? (
                  <div className="absolute inset-0 flex items-center justify-center bg-slate-50/90 px-4 text-center text-xs text-red-700">
                    {mapError}
                  </div>
                ) : null}
                {orderInfo && deliveryLegs.length === 0 && !mapError ? (
                  <div className="absolute inset-0 flex items-center justify-center bg-slate-50/90 px-4 text-center text-xs text-gray-500">
                    Inga transportben hittades för ordern.
                  </div>
                ) : null}
                {orderInfo && !isMapReady && !mapError && deliveryLegs.length > 0 ? (
                  <div className="absolute inset-0 flex items-center justify-center bg-slate-50/70 text-xs text-gray-500">
                    Laddar karta...
                  </div>
                ) : null}
              </div>

              <div className="max-h-40 overflow-auto border border-gray-300 bg-white">
                <table className="w-full text-left text-xs text-gray-700">
                  <thead className="sticky top-0 bg-gray-100 text-tiny uppercase text-gray-500">
                    <tr>
                      <th className="px-2 py-1">Typ</th>
                      <th className="px-2 py-1">Ort från</th>
                      <th className="px-2 py-1">Ort till</th>
                      <th className="px-2 py-1 text-right">Km</th>
                    </tr>
                  </thead>
                  <tbody>
                    {deliveryLegs.map((leg) => (
                      <tr className="border-t border-gray-100" key={`${leg.sortOrder}-${leg.fromPositionId}-${leg.toPositionId}`}>
                        <td className="px-2 py-1">{leg.typeOfTransport || 'Väg'}</td>
                        <td className="max-w-32 truncate px-2 py-1" title={leg.fromPositionPostalAddress || leg.fromPositionName}>
                          {leg.fromPositionPostalAddress || leg.fromPositionName || `Position ${leg.fromPositionId ?? ''}`}
                        </td>
                        <td className="max-w-32 truncate px-2 py-1" title={leg.toPositionPostalAddress || leg.toPositionName}>
                          {leg.toPositionPostalAddress || leg.toPositionName || `Position ${leg.toPositionId ?? ''}`}
                        </td>
                        <td className="px-2 py-1 text-right">{formatAmount(leg.distanceKm)}</td>
                      </tr>
                    ))}
                    {deliveryLegs.length === 0 ? (
                      <tr><td className="px-2 py-3 text-center text-gray-400" colSpan={4}>Inga transportben.</td></tr>
                    ) : null}
                  </tbody>
                </table>
              </div>

              <div className="grid gap-2 sm:grid-cols-[1fr_120px_auto]">
                <div className="relative">
                  <input
                    className="h-7 w-full border border-gray-300 bg-white px-2 text-xs outline-none focus:border-lime-700 disabled:bg-gray-100"
                    disabled={!orderInfo || !deliveryLegs.length || Boolean(savedDelivery) || isSubmitting}
                    onChange={(event) => setPositionSearch(event.target.value)}
                    placeholder="Sök transportposition"
                    value={positionSearch}
                  />
                  {positionResults.length > 0 ? (
                    <div className="absolute left-0 right-0 top-full z-20 max-h-36 overflow-auto border border-gray-300 bg-white shadow-lg">
                      {positionResults.map((position) => (
                        <button
                          className="flex w-full items-start gap-2 border-b border-gray-100 px-2 py-2 text-left text-xs text-gray-700 hover:bg-lime-50"
                          key={position.id}
                          onClick={() => handleInsertWaypoint(position)}
                          type="button"
                        >
                          <MapPinPlus aria-hidden="true" className="mt-0.5 shrink-0 text-amber-700" size={13} />
                          <span className="min-w-0">
                            <span className="block truncate">{position.name}</span>
                            <span className="block truncate text-gray-500">{position.postalAddress}</span>
                          </span>
                        </button>
                      ))}
                    </div>
                  ) : null}
                  {isLoadingPositions ? <span className="absolute right-2 top-1/2 -translate-y-1/2 text-tiny text-gray-500">Söker...</span> : null}
                </div>
                <select
                  aria-label="Lägg till position efter transportben"
                  className="h-7 border border-gray-300 bg-white px-1 text-xs text-gray-700"
                  disabled={!deliveryLegs.length || Boolean(savedDelivery) || isSubmitting}
                  onChange={(event) => setSelectedLegSortOrder(event.target.value)}
                  value={selectedLegSortOrder}
                >
                  {deliveryLegs.map((leg) => (
                    <option key={leg.sortOrder} value={leg.sortOrder}>Efter ben {leg.sortOrder}</option>
                  ))}
                </select>
                <span className="inline-flex items-center justify-center gap-1 border border-gray-200 px-2 text-tiny text-gray-500">
                  Lägg till via sökresultat
                </span>
              </div>
            </div>
          </div> */}

          {formError ? <p className="mt-4 border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">{formError}</p> : null}
          {savedDelivery ? (
            <p className="mt-3 text-xs font-semibold text-green-800">
              Leveransen sparades ({savedDelivery.type} {savedDelivery.id}{savedDelivery.slattId ? `, slatt ${savedDelivery.slattId}` : ''}).
            </p>
          ) : null}

          <footer className="flex gap-4 mt-8 mb-3 pt-4 justify-between">
            <div className="flex gap-4">
              <section className="flex flex-wrap items-center gap-3">
                <ActionButton
                  label="Välj följesedel"
                  icon={FilePlus2}
                  disabled={Boolean(savedDelivery) || isSubmitting || !orderInfo?.supplierOrderId}
                  onClick={() => fileInputRef.current?.click()}
                  accent="lime"
                />
                <input
                  accept=".pdf,.jpg,.jpeg,.png,.tif,.tiff"
                  className="hidden"
                  onChange={(event) => setSelectedFile(event.target.files?.[0] ?? null)}
                  ref={fileInputRef}
                  type="file"
                />
                {selectedFile ? <span className="max-w-md truncate text-xs text-gray-600">{selectedFile.name}</span> : null}
              </section>

            </div>

            <div className="flex gap-4">
              <button
                type="button"
                onClick={closeModal}
                disabled={isSubmitting}
                className="shadow-md/30 text-xs text-white bg-orange-400 hover:bg-orange-600 px-10 p-[5px] disabled:opacity-50"
              >
                Avbryt
              </button>
              {orderInfo ? (
                <button
                  type="button"
                  onClick={handleSave}
                  disabled={isSubmitting || (Boolean(savedDelivery) && (!selectedFile || Boolean(uploadedFile)))}
                  className="shadow-md/30 text-xs text-white bg-lime-700 hover:bg-lime-900 px-10 p-[5px] disabled:opacity-50"
                >
                  {isSubmitting ? 'Sparar...' : 'Spara'}
                </button>
              ) : null}
            </div>
          </footer>
        </div>
      </div>
    </div>,
    document.body,
  )
}

export default NewDelivery
