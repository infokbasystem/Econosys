import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useBlocker, useNavigate, useParams } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';
import { ArrowLeft, CirclePlus, PanelLeftClose, PanelLeftOpen, Printer, RefreshCw, Save, Trash2 } from 'lucide-react';

import { usePdf } from '../../contexts/PdfContext';
import ActionButton from '../../components/ActionButton';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledCheckbox from '../../components/LabeledCheckbox';
import SelectNewDeliveryInfo from '../../modals/SelectNewDeliveryInfo';
import DeliveryFromStock from '../../modals/DeliveryFromStock';
import LabeledDatePicker from '../../components/LabeledDatePicker';
import LabeledInput from '../../components/LabeledInput';
import LabeledReactSelect from '../../components/LabeledReactSelect';
import LabeledTextArea from '../../components/LabeledTextArea';
import apiClient from '../../config/apiClient';
import { getSharedRequest } from '../../helpers/sharedRequest';
import { toSwedishDateInputValue } from '../../helpers/dateUtils';

const defaultCallOffForm = {
    id: null,
    shipperId: '',
    reference: '',
    note: '',
    deliveryDate: '',
    deliveryStatus: 0,
    isSentToShipper: false,
    doDebitFreight: false,
    freightCostToDebit: '',
    customerDeliveryAddressId: '',
    createdByUserId: null,
    createdByUserName: '',
    createdDateTime: null,
};

const COMPANY_SETTINGS_ID = 1;
const GOOGLE_MAPS_SCRIPT_ID = 'google-maps-js-api';
let googleMapsLoaderPromise = null;

const mapDtoToForm = (dto) => ({
    id: dto?.id ?? null,
    shipperId: dto?.shipperId == null ? '' : String(dto.shipperId),
    reference: String(dto?.reference ?? ''),
    note: String(dto?.note ?? ''),
    deliveryDate: toSwedishDateInputValue(dto?.deliveryDate),
    deliveryStatus: Number.isInteger(dto?.deliveryStatus) ? dto.deliveryStatus : 0,
    isSentToShipper: Boolean(dto?.isSentToShipper),
    doDebitFreight: Boolean(dto?.doDebitFreight),
    freightCostToDebit: dto?.freightCostToDebit == null ? '' : String(dto.freightCostToDebit),
    customerDeliveryAddressId: dto?.customerDeliveryAddressId == null ? '' : String(dto.customerDeliveryAddressId),
    createdByUserId: dto?.createdByUserId ?? null,
    createdByUserName: String(dto?.createdByUserName ?? ''),
    createdDateTime: dto?.createdDateTime ?? null,
});

const mapDeliveryRowDto = (dto) => ({
    id: Number(dto?.id ?? 0),
    // Distinguishes rows in local state before/after they are linked to the call-off (id stays 0 until saved).
    rowKey: dto?.id ? String(dto.id) : `new-${dto?.deliveryFromStockId ?? ''}`,
    deliveryFromStockId: dto?.deliveryFromStockId ?? null,
    sortOrder: Number(dto?.sortOrder ?? 0),
    note: String(dto?.note ?? ''),
    deliveryAddressFreeText: String(dto?.deliveryAddressFreeText ?? ''),
    nrOfPalletPlaces: dto?.nrOfPalletPlaces == null ? '' : String(dto.nrOfPalletPlaces),
    customerOrderNr: String(dto?.customerOrderNr ?? ''),
    customerName: String(dto?.customerName ?? ''),
    productName: String(dto?.productName ?? ''),
    inventoryName: String(dto?.inventoryName ?? ''),
    nrOfPallets: Number(dto?.nrOfPallets ?? 0),
    palletFormatName: String(dto?.palletFormatName ?? ''),
    kolliFormat: String(dto?.kolliFormat ?? ''),
});

const mapDeliveryLegDto = (dto) => ({
    id: Number(dto?.id ?? 0),
    sortOrder: Number(dto?.sortOrder ?? 0),
    typeOfTransport: String(dto?.typeOfTransport ?? ''),
    distanceKm: dto?.distanceKm == null ? null : Number(dto.distanceKm),
    latitudeStart: dto?.latitudeStart == null ? null : Number(dto.latitudeStart),
    longitudeStart: dto?.longitudeStart == null ? null : Number(dto.longitudeStart),
    latitudeEnd: dto?.latitudeEnd == null ? null : Number(dto.latitudeEnd),
    longitudeEnd: dto?.longitudeEnd == null ? null : Number(dto.longitudeEnd),
    helppropLegFromPositionName: String(dto?.helppropLegFromPositionName ?? ''),
    helppropLegFromPositionPostalAddress: String(dto?.helppropLegFromPositionPostalAddress ?? ''),
    helppropLegToPositionName: String(dto?.helppropLegToPositionName ?? ''),
    helppropLegToPositionPostalAddress: String(dto?.helppropLegToPositionPostalAddress ?? ''),
});

const mapDeliveryLegsResponse = (dto) => ({
    deliveryLegGroupedList: Array.isArray(dto?.deliveryLegGroupedList)
        ? dto.deliveryLegGroupedList.map(mapDeliveryLegDto)
        : [],
    distributionDeliveryLegGroupedList: Array.isArray(dto?.distributionDeliveryLegGroupedList)
        ? dto.distributionDeliveryLegGroupedList.map(mapDeliveryLegDto)
        : [],
    distributionLegList: Array.isArray(dto?.distributionLegList)
        ? dto.distributionLegList.map((group) => (Array.isArray(group) ? group.map(mapDeliveryLegDto) : [])).filter((group) => group.length > 0)
        : [],
});

const mapAggregateResponse = (dto) => ({
    callOffForm: mapDtoToForm(dto?.callOff),
    callOffDeliveryList: Array.isArray(dto?.callOffDeliveryList) ? dto.callOffDeliveryList.map(mapDeliveryRowDto) : [],
    deliveryAddressOptions: Array.isArray(dto?.deliveryAddressOptions)
        ? dto.deliveryAddressOptions.map((item) => ({
            id: Number(item?.id ?? 0),
            name: String(item?.name ?? ''),
            street: String(item?.street ?? ''),
            postalNr: String(item?.postalNr ?? ''),
            city: String(item?.city ?? ''),
        }))
        : [],
    deliveryLegData: mapDeliveryLegsResponse(dto?.deliveryLegs),
    version: String(dto?.version ?? ''),
});

const deliveryStatusOptions = [
    { value: 0, label: 'Ej lastad' },
    { value: 1, label: 'Lastad' },
    { value: 2, label: 'Lossad' },
];

const getRouteRequestFromLegs = (legs) => {
    if (!Array.isArray(legs) || legs.length === 0) {
        return null;
    }

    const sortedLegs = [...legs].sort((a, b) => a.sortOrder - b.sortOrder);
    const originLeg = sortedLegs[0];
    const destinationLeg = sortedLegs[sortedLegs.length - 1];

    const originLat = Number(originLeg?.latitudeStart);
    const originLng = Number(originLeg?.longitudeStart);
    const destinationLat = Number(destinationLeg?.latitudeEnd);
    const destinationLng = Number(destinationLeg?.longitudeEnd);

    if (!Number.isFinite(originLat) || !Number.isFinite(originLng) || !Number.isFinite(destinationLat) || !Number.isFinite(destinationLng)) {
        return null;
    }

    const waypoints = sortedLegs.length >= 2
        ? sortedLegs
            .slice(1)
            .map((leg) => ({
                lat: Number(leg?.latitudeStart),
                lng: Number(leg?.longitudeStart),
            }))
            .filter((waypoint) => Number.isFinite(waypoint.lat) && Number.isFinite(waypoint.lng))
        : [];

    return {
        origin: { lat: originLat, lng: originLng },
        destination: { lat: destinationLat, lng: destinationLng },
        waypoints,
    };
};

const loadGoogleMapsApi = (apiKey) => {
    if (!apiKey) {
        return Promise.reject(new Error('Missing Google Maps API key.'));
    }

    if (window.google?.maps) {
        return Promise.resolve(window.google.maps);
    }

    if (googleMapsLoaderPromise) {
        return googleMapsLoaderPromise;
    }

    googleMapsLoaderPromise = new Promise((resolve, reject) => {
        const existingScript = document.getElementById(GOOGLE_MAPS_SCRIPT_ID);

        if (existingScript) {
            existingScript.addEventListener('load', () => resolve(window.google.maps), { once: true });
            existingScript.addEventListener('error', () => reject(new Error('Failed to load Google Maps script.')), { once: true });
            return;
        }

        const script = document.createElement('script');
        script.id = GOOGLE_MAPS_SCRIPT_ID;
        script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}`;
        script.async = true;
        script.defer = true;
        script.onload = () => resolve(window.google.maps);
        script.onerror = () => reject(new Error('Failed to load Google Maps script.'));
        document.body.appendChild(script);
    });

    return googleMapsLoaderPromise;
};

const formatDistance = (distanceKm) => {
    if (distanceKm == null || Number.isNaN(distanceKm)) {
        return '';
    }

    return Number(distanceKm).toLocaleString('sv-SE', {
        minimumFractionDigits: 1,
        maximumFractionDigits: 1,
    });
};

const Calloff = () => {
    const navigate = useNavigate();
    const { id } = useParams();
    const { openPdfPreview, closePdfPreview } = usePdf();

    // Legacy parity: the WPF client opens CallOffViewModel with CallOffId = 0 for a new call-off.
    const isNewCallOff = id === 'new' || id == null;

    const [loading, setLoading] = useState(true);
    const [messages, setMessages] = useState([]);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [callOffForm, setCallOffForm] = useState(defaultCallOffForm);
    const [callOffDeliveryList, setCallOffDeliveryList] = useState([]);
    const [deliveryAddressOptions, setDeliveryAddressOptions] = useState([]);
    const [deliveryLegData, setDeliveryLegData] = useState({ deliveryLegGroupedList: [], distributionDeliveryLegGroupedList: [], distributionLegList: [] });
    const [googleApiKey, setGoogleApiKey] = useState('');
    const [mapError, setMapError] = useState('');
    const [shipperOptions, setShipperOptions] = useState([]);
    const [aggregateVersion, setAggregateVersion] = useState('');
    const [isSaving, setIsSaving] = useState(false);
    const [isStaleConflict, setIsStaleConflict] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);
    const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
    const [showAddDeliveryModal, setShowAddDeliveryModal] = useState(false);
    const [pendingDeliverySelection, setPendingDeliverySelection] = useState(null);
    const [isAddingDelivery, setIsAddingDelivery] = useState(false);
    const [addDeliveryError, setAddDeliveryError] = useState('');
    // View mode: shows the details of an already created delivery from stock.
    const [viewDeliveryDetails, setViewDeliveryDetails] = useState(null);
    const [isLoadingViewDelivery, setIsLoadingViewDelivery] = useState(false);
    const [viewDeliveryError, setViewDeliveryError] = useState('');
    // Legacy parity: info panel starts collapsed, same as TransportOrder.
    const [isInfoPanelExpanded, setIsInfoPanelExpanded] = useState(false);
    const [originalCallOffState, setOriginalCallOffState] = useState(null);
    const skipUnsavedCheckRef = useRef(false);
    const mapContainerRef = useRef(null);
    const isSavingRef = useRef(false);
    const mapRuntimeRef = useRef({ map: null, renderers: [] });
    const infoPanelAutoCloseRef = useRef(null);

    const flashInfoPanel = useCallback((hasIssue) => {
        if (infoPanelAutoCloseRef.current) {
            clearTimeout(infoPanelAutoCloseRef.current);
            infoPanelAutoCloseRef.current = null;
        }
        setIsInfoPanelExpanded((prev) => {
            if (prev) {
                return prev;
            }
            if (!hasIssue) {
                infoPanelAutoCloseRef.current = setTimeout(() => {
                    setIsInfoPanelExpanded(false);
                    infoPanelAutoCloseRef.current = null;
                }, 2000);
            }
            return true;
        });
    }, []);

    useEffect(() => () => {
        if (infoPanelAutoCloseRef.current) {
            clearTimeout(infoPanelAutoCloseRef.current);
        }
    }, []);

    const createSnapshot = useCallback(() => ({
        callOffForm,
        callOffDeliveryList,
    }), [callOffDeliveryList, callOffForm]);

    const hasUnsavedChanges = useCallback(() => {
        if (skipUnsavedCheckRef.current) return false;
        if (!originalCallOffState) return false;
        return JSON.stringify(createSnapshot()) !== JSON.stringify(originalCallOffState);
    }, [createSnapshot, originalCallOffState]);

    const blocker = useBlocker(hasUnsavedChanges);

    const applyAggregateResponse = (dto) => {
        const aggregate = mapAggregateResponse(dto);
        const nextSnapshot = {
            callOffForm: aggregate.callOffForm,
            callOffDeliveryList: aggregate.callOffDeliveryList,
        };

        setCallOffForm(aggregate.callOffForm);
        setCallOffDeliveryList(aggregate.callOffDeliveryList);
        setDeliveryAddressOptions(aggregate.deliveryAddressOptions);
        setDeliveryLegData(aggregate.deliveryLegData);
        setAggregateVersion(aggregate.version);
        setOriginalCallOffState(nextSnapshot);
    };

    useEffect(() => {
        let isActive = true;

        const loadFormData = async () => {
            setLoading(true);
            setMessages([]);
            setIsStaleConflict(false);

            try {
                const [formOptionsResponse, companySettingsResponse] = await Promise.all([
                    getSharedRequest('calloff:form-options', () => apiClient.get('/calloff/form-options')),
                    getSharedRequest(`companysettings:${COMPANY_SETTINGS_ID}`, () => apiClient.get(`/companysettings/${COMPANY_SETTINGS_ID}`)),
                ]);

                if (!isActive) {
                    return;
                }

                const shippers = Array.isArray(formOptionsResponse?.data?.shippers)
                    ? formOptionsResponse.data.shippers
                    : [];

                setShipperOptions(shippers);

                if (isNewCallOff) {
                    setCallOffForm(defaultCallOffForm);
                    setCallOffDeliveryList([]);
                    setDeliveryAddressOptions([]);
                    setDeliveryLegData({ deliveryLegGroupedList: [], distributionDeliveryLegGroupedList: [], distributionLegList: [] });
                    setAggregateVersion('');
                    setOriginalCallOffState(null);
                } else {
                    const aggregateResponse = await getSharedRequest(
                        `calloff:${id}:aggregate`,
                        () => apiClient.get(`/calloff/${id}/aggregate`),
                    );

                    if (!isActive) {
                        return;
                    }

                    applyAggregateResponse(aggregateResponse?.data);
                }

                setGoogleApiKey(String(companySettingsResponse?.data?.googleApiKey ?? ''));
            } catch (error) {
                console.error('Failed to load call-off form:', error);

                if (!isActive) {
                    return;
                }

                setMessages([{ type: 'error', text: 'Kunde inte hämta avropet.' }]);
                setCallOffDeliveryList([]);
                setDeliveryAddressOptions([]);
                setDeliveryLegData({ deliveryLegGroupedList: [], distributionDeliveryLegGroupedList: [], distributionLegList: [] });
                setAggregateVersion('');
                setGoogleApiKey('');
            } finally {
                if (isActive) {
                    setLoading(false);
                }
            }
        };

        loadFormData();

        return () => {
            isActive = false;
        };
    }, [id, isNewCallOff]);

    useEffect(() => {
        const handleBeforeUnload = (event) => {
            if (!hasUnsavedChanges()) return;

            event.preventDefault();
            event.returnValue = '';
            return '';
        };

        window.addEventListener('beforeunload', handleBeforeUnload);
        return () => window.removeEventListener('beforeunload', handleBeforeUnload);
    }, [hasUnsavedChanges]);

    useEffect(() => {
        if (blocker.state === 'blocked') {
            setShowUnsavedWarning(true);
        }
    }, [blocker.state]);

    useEffect(() => {
        skipUnsavedCheckRef.current = false;
    }, [id]);

    useEffect(() => {
        return () => {
            closePdfPreview?.();
        };
    }, [closePdfPreview]);

    const titleText = useMemo(() => (isNewCallOff ? 'Ny avrop' : `Avrop ${id}`), [id, isNewCallOff]);

    const shipperItems = useMemo(() => {
        return [{ id: '', name: '' }, ...shipperOptions];
    }, [shipperOptions]);

    const deliveryAddressItems = useMemo(() => {
        return [{ id: '', name: '' }, ...deliveryAddressOptions.map((item) => ({
            id: item.id,
            name: [item.name, item.street, [item.postalNr, item.city].filter(Boolean).join(' ')].filter(Boolean).join(', '),
        }))];
    }, [deliveryAddressOptions]);

    // Legacy parity: Leveransplatser shows the customer name plus the selected delivery address details.
    const selectedDeliveryAddressSummary = useMemo(() => {
        if (callOffForm.customerDeliveryAddressId === '') {
            return null;
        }

        const selectedAddress = deliveryAddressOptions.find((item) => String(item.id) === String(callOffForm.customerDeliveryAddressId));
        if (!selectedAddress) {
            return null;
        }

        const customerName = callOffDeliveryList.find((delivery) => delivery.customerName)?.customerName ?? '';

        return {
            customerName,
            name: selectedAddress.name,
            street: selectedAddress.street,
            postalCity: [selectedAddress.postalNr, selectedAddress.city].filter(Boolean).join(''),
        };
    }, [callOffDeliveryList, callOffForm.customerDeliveryAddressId, deliveryAddressOptions]);

    // Legacy parity: Palltyper groups the call-off deliveries by pallet format and sums the pallet counts.
    const palletFormatSummary = useMemo(() => {
        const countByFormatName = new Map();

        callOffDeliveryList.forEach((delivery) => {
            const formatName = delivery.palletFormatName || '';
            countByFormatName.set(formatName, (countByFormatName.get(formatName) ?? 0) + (delivery.nrOfPallets ?? 0));
        });

        return Array.from(countByFormatName.entries()).map(([name, count]) => ({ name, count }));
    }, [callOffDeliveryList]);

    useEffect(() => {
        let isActive = true;

        const renderLegacyMapRoutes = async () => {
            if (!mapContainerRef.current) {
                mapRuntimeRef.current = { map: null, renderers: [] };
                return;
            }

            setMapError('');

            // Legacy parity: a call-off is often a single leg (pickup -> destination), so one leg is enough to draw a route.
            const mainRouteRequest = deliveryLegData.deliveryLegGroupedList.length >= 1
                ? getRouteRequestFromLegs(deliveryLegData.deliveryLegGroupedList)
                : null;

            const distributionRouteRequests = Array.isArray(deliveryLegData.distributionLegList)
                ? deliveryLegData.distributionLegList
                    .map((legs) => (Array.isArray(legs) && legs.length >= 1 ? getRouteRequestFromLegs(legs) : null))
                    .filter(Boolean)
                : [];

            if (!googleApiKey) {
                setMapError('Google API key saknas i Företagsinfo.');
                return;
            }

            try {
                const maps = await loadGoogleMapsApi(googleApiKey);

                if (!isActive || !mapContainerRef.current) {
                    return;
                }

                const map = mapRuntimeRef.current.map
                    ?? new maps.Map(mapContainerRef.current, {
                        disableDefaultUI: true,
                        zoom: 5,
                        center: { lat: 59.3293, lng: 18.0686 },
                    });

                mapRuntimeRef.current.map = map;

                mapRuntimeRef.current.renderers.forEach((renderer) => {
                    renderer.setMap(null);
                });
                mapRuntimeRef.current.renderers = [];

                const allPoints = [];
                const routeDefinitions = [];

                if (mainRouteRequest) {
                    routeDefinitions.push({ route: mainRouteRequest, strokeColor: '#dc2626' });
                }

                distributionRouteRequests.forEach((route) => {
                    routeDefinitions.push({ route, strokeColor: '#2563eb' });
                });

                if (routeDefinitions.length === 0) {
                    return;
                }

                const directionsService = new maps.DirectionsService();

                for (const definition of routeDefinitions) {
                    const renderer = new maps.DirectionsRenderer({
                        map,
                        suppressMarkers: true,
                        preserveViewport: true,
                        polylineOptions: {
                            strokeColor: definition.strokeColor,
                        },
                    });

                    mapRuntimeRef.current.renderers.push(renderer);

                    const routeResult = await directionsService.route({
                        origin: definition.route.origin,
                        destination: definition.route.destination,
                        waypoints: definition.route.waypoints.map((waypoint) => ({
                            location: waypoint,
                            stopover: false,
                        })),
                        optimizeWaypoints: true,
                        avoidTolls: false,
                        avoidHighways: false,
                        travelMode: maps.TravelMode.DRIVING,
                    });

                    if (!isActive) {
                        return;
                    }

                    renderer.setDirections(routeResult);

                    allPoints.push(definition.route.origin, definition.route.destination, ...definition.route.waypoints);
                }

                if (allPoints.length > 0) {
                    const bounds = new maps.LatLngBounds();
                    allPoints.forEach((point) => {
                        bounds.extend(point);
                    });
                    map.fitBounds(bounds);
                }
            } catch (error) {
                console.error('Failed to render Google call-off map:', error);
                if (isActive) {
                    setMapError('Kunde inte ladda Google-karta eller rita transportben.');
                }
            }
        };

        renderLegacyMapRoutes();

        return () => {
            isActive = false;
        };
    }, [deliveryLegData, googleApiKey]);

    const updateFormField = (field, value) => {
        setCallOffForm((prev) => ({
            ...prev,
            [field]: value,
        }));
    };

    const updateDeliveryField = (rowKey, field, value) => {
        setCallOffDeliveryList((prev) => prev.map((delivery) => (
            delivery.rowKey === rowKey ? { ...delivery, [field]: value } : delivery
        )));
    };

    const removeCallOffDelivery = async (rowKey) => {
        const delivery = callOffDeliveryList.find((item) => item.rowKey === rowKey);
        if (!delivery) {
            return;
        }

        setCallOffDeliveryList((prev) => prev.filter((item) => item.rowKey !== rowKey));

        const pruneSnapshot = (prev) => (prev
            ? { ...prev, callOffDeliveryList: prev.callOffDeliveryList.filter((item) => item.rowKey !== rowKey) }
            : prev);

        try {
            if (delivery.id === 0) {
                // The delivery hasn't been linked to the call-off yet (Id 0), so clean up the now-orphaned DeliveryFromStock row.
                if (delivery.deliveryFromStockId) {
                    await apiClient.delete(`/calloff/deliveries/${delivery.deliveryFromStockId}`);
                }
                setOriginalCallOffState(pruneSnapshot);
                return;
            }

            // Linked rows are deleted from the database directly; no save is needed.
            const response = await apiClient.delete(`/calloff/${id}/deliveries/${delivery.id}`);
            if (response?.data?.version) {
                setAggregateVersion(String(response.data.version));
            }
            setOriginalCallOffState(pruneSnapshot);
            setMessages((prev) => [...prev, { type: 'info', text: 'Leveransen raderades.' }]);
        } catch (error) {
            console.error('Failed to delete call-off delivery:', error);
            // Restore the row so local state matches the database again.
            setCallOffDeliveryList((prev) => (prev.some((item) => item.rowKey === rowKey) ? prev : [...prev, delivery]));
            setMessages((prev) => [...prev, { type: 'error', text: 'Kunde inte radera leveransen.' }]);
            flashInfoPanel(true);
        }
    };

    const handleBackClick = () => {
        if (window.history.length > 1) {
            navigate(-1);
            return;
        }

        navigate('/logistics/calloffoverview');
    };

    const handleUnsavedWarningConfirm = () => {
        setShowUnsavedWarning(false);
        blocker.proceed();
    };

    const handleUnsavedWarningAbort = () => {
        setShowUnsavedWarning(false);
        if (blocker.state === 'blocked') {
            blocker.reset();
        }
    };

    const handleOpenPdfPanel = () => {
        openPdfPreview?.('');
    };

    const handleDeleteCallOff = async () => {
        setShowDeleteConfirm(false);

        if (isDeleting || isNewCallOff || !id) {
            return;
        }

        setIsDeleting(true);
        setMessages([]);
        try {
            await apiClient.delete(`/calloff/${id}`);
            skipUnsavedCheckRef.current = true;
            navigate('/logistics/calloffoverview');
        } catch (error) {
            console.error('Failed to delete call-off:', error);
            const apiMessage = typeof error.response?.data === 'string' ? error.response.data : '';
            setMessages((prev) => [...prev, { type: 'error', text: apiMessage || 'Kunde inte radera avropet.' }]);
            flashInfoPanel(true);
        } finally {
            setIsDeleting(false);
        }
    };

    const handleAddDelivery = () => {
        setAddDeliveryError('');
        setShowAddDeliveryModal(true);
    };

    const handleCloseAddDeliveryModal = () => {
        setShowAddDeliveryModal(false);
    };

    // Legacy parity: picking a balance group only opens the delivery details dialog; nothing is created yet.
    const handleDeliveryCandidateSelected = (candidate, group) => {
        setAddDeliveryError('');
        setPendingDeliverySelection({ candidate, group });
        setShowAddDeliveryModal(false);
    };

    const handleCloseDeliveryDetailsModal = () => {
        if (isAddingDelivery || isLoadingViewDelivery) {
            return;
        }

        if (viewDeliveryDetails != null) {
            setViewDeliveryDetails(null);
            setViewDeliveryError('');
            return;
        }

        setPendingDeliverySelection(null);
    };

    // View mode: fetch the details of an already created delivery from stock and show them read-only.
    const handleViewDelivery = async (delivery) => {
        if (isLoadingViewDelivery || !delivery?.deliveryFromStockId) {
            return;
        }

        setIsLoadingViewDelivery(true);
        setViewDeliveryError('');
        try {
            const response = await apiClient.get(`/calloff/deliveries/fromstock/${delivery.deliveryFromStockId}`);
            setViewDeliveryDetails(response?.data ?? null);
        } catch (error) {
            console.error('Failed to load delivery details:', error);
            const apiMessage = typeof error.response?.data === 'string' ? error.response.data : '';
            setViewDeliveryError(apiMessage || 'Kunde inte läsa in leveransen.');
            setMessages((prev) => [...prev, { type: 'error', text: apiMessage || 'Kunde inte läsa in leveransen.' }]);
            flashInfoPanel(true);
        } finally {
            setIsLoadingViewDelivery(false);
        }
    };

    const handleSaveDeliveryDetails = async (details) => {
        setIsAddingDelivery(true);
        setAddDeliveryError('');
        try {
            const response = await apiClient.post('/calloff/deliveries', details);
            const newDelivery = mapDeliveryRowDto(response?.data);
            setCallOffDeliveryList((prev) => [...prev, newDelivery]);
            setPendingDeliverySelection(null);
            setMessages((prev) => [...prev, { type: 'info', text: 'Leverans tillagd. Spara avropet för att spara ändringen.' }]);
        } catch (error) {
            console.error('Failed to add delivery to call-off:', error);
            const apiMessage = typeof error.response?.data === 'string' ? error.response.data : '';
            setAddDeliveryError(apiMessage || 'Kunde inte lägga till leveransen.');
        } finally {
            setIsAddingDelivery(false);
        }
    };

    const saveCallOff = async () => {
        if (isSavingRef.current || isStaleConflict || (!aggregateVersion && !isNewCallOff)) {
            return;
        }
        isSavingRef.current = true;

        const payload = {
            shipperId: callOffForm.shipperId === '' ? null : Number(callOffForm.shipperId),
            reference: callOffForm.reference || null,
            note: callOffForm.note || null,
            deliveryDate: callOffForm.deliveryDate || null,
            deliveryStatus: Number(callOffForm.deliveryStatus),
            isSentToShipper: Boolean(callOffForm.isSentToShipper),
            doDebitFreight: Boolean(callOffForm.doDebitFreight),
            freightCostToDebit: callOffForm.freightCostToDebit === '' ? null : Number(callOffForm.freightCostToDebit),
            customerDeliveryAddressId: callOffForm.customerDeliveryAddressId === '' ? null : Number(callOffForm.customerDeliveryAddressId),
            callOffDeliveryList: callOffDeliveryList.map((delivery) => ({
                id: Number(delivery.id),
                note: delivery.note || null,
                nrOfPalletPlaces: delivery.nrOfPalletPlaces === '' ? null : Number(delivery.nrOfPalletPlaces),
                deliveryFromStockId: delivery.id === 0 ? delivery.deliveryFromStockId : null,
            })),
        };

        setIsSaving(true);
        setMessages([]);
        try {
            if (isNewCallOff) {
                const response = await apiClient.post('/calloff', payload);
                skipUnsavedCheckRef.current = true;
                setMessages((prev) => [...prev, { type: 'info', text: 'Avropet sparades.' }]);
                navigate(`/logistics/calloff/${response?.data?.callOff?.id}`, { replace: true });
            } else {
                const response = await apiClient.put(`/calloff/${id}/aggregate`, {
                    ...payload,
                    version: aggregateVersion,
                });
                applyAggregateResponse(response?.data);
                setIsStaleConflict(false);
                setMessages((prev) => [...prev, { type: 'info', text: 'Avropet sparades.' }]);
                flashInfoPanel(false);
            }
        } catch (error) {
            console.error('Failed to save call-off:', error);
            if (error.response?.status === 409) {
                setIsStaleConflict(true);
                setMessages((prev) => [...prev, { type: 'error', text: 'Avropet har ändrats av en annan användare.' }]);
            } else {
                const apiMessage = typeof error.response?.data === 'string' ? error.response.data : '';
                setMessages((prev) => [...prev, { type: 'error', text: apiMessage || 'Kunde inte spara avropet.' }]);
            }
            flashInfoPanel(true);
        } finally {
            setIsSaving(false);
            isSavingRef.current = false;
        }
    };

    const reloadLatestCallOff = async () => {
        try {
            const response = await apiClient.get(`/calloff/${id}/aggregate`);
            applyAggregateResponse(response?.data);
            setIsStaleConflict(false);
            setMessages((prev) => [...prev, { type: 'info', text: 'Den senaste versionen av avropet lästes in.' }]);
        } catch (error) {
            console.error('Failed to reload call-off:', error);
            setMessages((prev) => [...prev, { type: 'error', text: 'Kunde inte läsa in den senaste versionen av avropet.' }]);
        }
    };

    if (loading) {
        return (
            <div className="relative flex flex-col h-full md:px-[clamp(4px,3vw,6vw)] animate-pulse">
                <div className="mt-1 flex min-h-0 flex-1 flex-col">
                    <div className={`grid min-h-0 flex-1 gap-6 ${isInfoPanelExpanded ? 'lg:grid-cols-[300px_minmax(0,1fr)] lg:gap-8' : 'lg:grid-cols-[56px_minmax(0,1fr)] lg:gap-6'} lg:items-start`}>
                        <div className={`mt-6 mb-8 pr-3 border-b border-gray-300 lg:mb-6 lg:self-stretch lg:border-b-0 ${isInfoPanelExpanded ? 'lg:border-r' : 'lg:border-r-0'}`}>
                            {isInfoPanelExpanded ? (
                                <aside className="space-y-4 pr-0 pb-4 ml-2">
                                    <Skeleton height={18} width={120} />
                                    <Skeleton height={12} width={220} />
                                    <Skeleton height={12} width={220} />
                                </aside>
                            ) : null}
                        </div>
                        <div className="flex-grow pb-10 mr-6">
                            <div className="pb-3 flex items-center gap-3">
                                <Skeleton height={32} width={32} />
                                <Skeleton height={18} width={260} />
                            </div>
                            <div className="flex gap-4 mb-8 flex-wrap">
                                <Skeleton height={30} width={90} />
                                <Skeleton height={30} width={90} />
                                <Skeleton height={30} width={130} />
                                <Skeleton height={30} width={120} />
                                <Skeleton height={30} width={130} />
                                <Skeleton height={30} width={90} />
                            </div>
                            <Skeleton height={120} />
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="relative flex flex-col h-full md:px-[clamp(6px,4vw,8vw)]">
            <ConfirmationModal
                isOpen={showUnsavedWarning}
                onClose={handleUnsavedWarningAbort}
                onConfirm={handleUnsavedWarningConfirm}
                title="Osparade ändringar"
                message="Det finns osparade ändringar, vill du ändå fortsätta?"
                confirmText="Fortsätt ändå"
                cancelText="Avbryt"
                isDestructive={false}
            />

            <ConfirmationModal
                isOpen={showDeleteConfirm}
                onClose={() => setShowDeleteConfirm(false)}
                onConfirm={handleDeleteCallOff}
                title="Radera avrop"
                message={`Är du säker på att du vill radera avropet? Åtgärden kan inte ångras.`}
                confirmText={isDeleting ? 'Raderar...' : 'Radera'}
                cancelText="Avbryt"
                isDestructive={true}
            />

            <SelectNewDeliveryInfo
                isOpen={showAddDeliveryModal}
                onClose={handleCloseAddDeliveryModal}
                onSelect={handleDeliveryCandidateSelected}
            />

            <DeliveryFromStock
                isOpen={pendingDeliverySelection != null || viewDeliveryDetails != null}
                onClose={handleCloseDeliveryDetailsModal}
                onSave={handleSaveDeliveryDetails}
                candidate={pendingDeliverySelection?.candidate}
                group={pendingDeliverySelection?.group}
                details={viewDeliveryDetails}
                isSubmitting={isAddingDelivery || isLoadingViewDelivery}
                submitError={addDeliveryError}
            />

            <div className="mt-0 flex min-h-0 flex-1 flex-col pr-10">
                <div className={`grid min-h-0 flex-1 gap-6 transition-[grid-template-columns] duration-300 ease-in-out ${isInfoPanelExpanded ? 'lg:grid-cols-[300px_minmax(0,1fr)] lg:gap-8' : 'lg:grid-cols-[0px_minmax(0,1fr)] lg:gap-6'} lg:items-start`}>
                    <div className={`mt-2 lg:mt-0 mb-8 pr-3 border-b border-gray-300 lg:mb-6 lg:self-stretch lg:border-b-0 overflow-hidden lg:overflow-visible transition-[border-color] duration-300 ease-in-out ${isInfoPanelExpanded ? 'lg:border-r' : 'lg:border-r-0'}`}>
                        <aside
                            className={`lg:sticky lg:top-[calc(52px+72px+1rem)] lg:z-10 lg:self-start transform transition-all duration-300 ease-in-out ${isInfoPanelExpanded ? 'translate-x-0 opacity-100' : '-translate-x-4 opacity-0 pointer-events-none'}`}
                        >
                            <div className="space-y-4 pr-0 pb-4 ml-2">
                                <h2 className="text-4 text-center text-gray-500 text-sm">Info</h2>
                                <div className="space-y-2 text-xs text-gray-500" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                                    <div className="grid grid-cols-24 gap-1">
                                        <div className="col-span-6"><span className="text-gray-500">Skapad:</span></div>
                                        <div className="col-span-9">
                                            {callOffForm.createdDateTime
                                                ? new Date(callOffForm.createdDateTime).toLocaleString('sv-SE', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
                                                : '-'}
                                        </div>
                                        <div className="col-span-9 text-gray-500">
                                            {callOffForm.createdByUserName ? `av ${callOffForm.createdByUserName}` : '-'}
                                        </div>
                                    </div>
                                </div>
                            </div>

                            <div className="border-b border-gray-300" />

                            <div className="space-y-4 pr-0 py-4 ml-2">
                                <h2 className="text-4 text-center text-gray-500 text-sm">Meddelanden</h2>
                                {messages.length === 0 ? (
                                    <p className="text-xs text-center font-light mt-4">Inga meddelanden</p>
                                ) : (
                                    <ul className="mt-2 space-y-2">
                                        {messages.map((message, index) => (
                                            <li
                                                key={`${message.type ?? 'info'}-${index}`}
                                                className={`text-center text-xs p-2 rounded border border-gray-200 ${message.type === 'error' ? 'bg-red-100 text-red-700' :
                                                    message.type === 'warning' ? 'bg-yellow-100 text-yellow-800' :
                                                        'bg-green-100 text-green-700'
                                                    }`}
                                            >
                                                {String(message.text ?? '')}
                                            </li>
                                        ))}
                                    </ul>
                                )}
                            </div>

                            <div className="border-b border-gray-300" />
                        </aside>
                    </div>

                    <div className="flex-grow pb-10 mr-0">
                        <div className="pb-1 flex items-center gap-3">
                            <button
                                type="button"
                                onClick={() => setIsInfoPanelExpanded((prev) => !prev)}
                                className="inline-flex h-8 w-8 items-center justify-center text-gray-600 hover:bg-gray-50"
                                title={isInfoPanelExpanded ? 'Dolj informationspanel' : 'Visa informationspanel'}
                                aria-label={isInfoPanelExpanded ? 'Dolj informationspanel' : 'Visa informationspanel'}
                            >
                                {isInfoPanelExpanded ? <PanelLeftClose size={16} /> : <PanelLeftOpen size={16} />}
                            </button>
                            <h2 className="text-sm text-gray-500 tracking-[0.10em] font-semibold uppercase">
                                {titleText}
                            </h2>
                        </div>

                        <div className="flex justify-between w-full mb-5 pb-2">
                            <div className="flex items-center gap-6 flex-wrap">
                                <ActionButton
                                    label="Tillbaka"
                                    icon={ArrowLeft}
                                    onClick={handleBackClick}
                                    accent="slate"
                                />
                                <ActionButton
                                    label="Spara"
                                    icon={Save}
                                    onClick={() => saveCallOff()}
                                    disabled={isSaving || isStaleConflict || (!aggregateVersion && !isNewCallOff)}
                                    accent="lime"
                                />
                                <div className="ml-20 flex items-center gap-2">
                                    <ActionButton
                                        label="Fraktbeställning/följesedel"
                                        icon={Printer}
                                        onClick={handleOpenPdfPanel}
                                        accent="sky"
                                    />
                                    <ActionButton
                                        label="Avropsbekräftelse"
                                        icon={Printer}
                                        onClick={handleOpenPdfPanel}
                                        accent="sky"
                                    />
                                </div>
                                <div className="ml-20 flex items-center gap-2">
                                    <ActionButton
                                        label="Radera"
                                        icon={Trash2}
                                        accent="rose"
                                        disabled={isNewCallOff || isDeleting}
                                        onClick={() => setShowDeleteConfirm(true)}
                                    />
                                </div>
                            </div>
                        </div>

                        {isStaleConflict ? (
                            <div className="mb-4 flex flex-wrap items-center gap-4 border border-amber-300 bg-amber-50 px-4 py-3 text-xs text-amber-900">
                                <span>Avropet har ändrats av en annan användare. Ditt utkast har behållits men kan inte sparas ovanpå den nya versionen.</span>
                                <ActionButton
                                    label="Läs in senaste"
                                    icon={RefreshCw}
                                    onClick={reloadLatestCallOff}
                                    accent="yellow"
                                />
                            </div>
                        ) : null}

                        <div className="mt-3">
                            <div className="grid grid-cols-1 items-start gap-30 xl:grid-cols-[auto_500px]">
                                <section aria-label="Vänster">

                                    <div className="grid grid-cols-[450px_1fr] gap-20 items-start">
                                        <section aria-label="Formulärfält">
                                            <div className="">
                                                <LabeledReactSelect
                                                    label="Speditör"
                                                    labelWidth="w-24"
                                                    name="shipperId"
                                                    value={callOffForm.shipperId}
                                                    items={shipperItems}
                                                    onChange={(value) => updateFormField('shipperId', value)}
                                                    margintop="0"
                                                />
                                                <LabeledReactSelect
                                                    label="Leveransadress"
                                                    labelWidth="w-24"
                                                    name="customerDeliveryAddressId"
                                                    value={callOffForm.customerDeliveryAddressId}
                                                    items={deliveryAddressItems}
                                                    onChange={(value) => updateFormField('customerDeliveryAddressId', value)}
                                                    margintop="0"
                                                />
                                                <LabeledInput
                                                    label="Referens"
                                                    labelWidth="w-24"
                                                    name="reference"
                                                    value={callOffForm.reference}
                                                    onChange={(value) => updateFormField('reference', value)}
                                                    margintop="0"
                                                />
                                                <LabeledTextArea
                                                    label="Notering"
                                                    labelWidth="w-24"
                                                    height="h-16"
                                                    name="note"
                                                    value={callOffForm.note}
                                                    onChange={(value) => updateFormField('note', value)}
                                                    margintop="0"
                                                />
                                            </div>

                                            <div className="pt-5 grid grid-cols-[auto_1fr] gap-10 items-start">
                                                <div className="">
                                                    <LabeledDatePicker
                                                        label="Leveransdatum"
                                                        labelWidth="w-24"
                                                        name="deliveryDate"
                                                        value={callOffForm.deliveryDate}
                                                        onChange={(value) => updateFormField('deliveryDate', value)}
                                                        valueType="input"
                                                        margintop="0"
                                                    />
                                                </div>
                                            </div>

                                            <div className="pt-3">
                                                <div className="w-40">
                                                    <LabeledCheckbox
                                                        label="Skickad till transportör"
                                                        labelPosition="left"
                                                        name="isSentToShipper"
                                                        checked={callOffForm.isSentToShipper}
                                                        onChange={(value) => updateFormField('isSentToShipper', value)}
                                                        className="flex w-full items-center justify-between gap-3 text-xs text-gray-700"
                                                    />
                                                </div>
                                                <div className='flex flex-row items-center'>
                                                    <div className="w-40">
                                                        <LabeledCheckbox
                                                            label="Debitera frakt"
                                                            labelPosition="left"
                                                            name="doDebitFreight"
                                                            checked={callOffForm.doDebitFreight}
                                                            onChange={(value) => updateFormField('doDebitFreight', value)}
                                                            className="flex w-full items-center justify-between gap-3 text-xs text-gray-700"
                                                        />
                                                    </div>

                                                    {callOffForm.doDebitFreight ? (
                                                        <div className="w-50 ml-10">
                                                            <LabeledInput
                                                                label="Fraktkostnad"
                                                                labelWidth="w-20"
                                                                name="freightCostToDebit"
                                                                value={callOffForm.freightCostToDebit}
                                                                onChange={(value) => updateFormField('freightCostToDebit', value)}
                                                                margintop="0"
                                                            />
                                                        </div>
                                                    ) : null}

                                                </div>
                                            </div>

                                            <fieldset className="mt-3 w-40" aria-label="Leveransstatus">
                                                {deliveryStatusOptions.map((status) => (
                                                    <LabeledCheckbox
                                                        key={status.value}
                                                        inputType="radio"
                                                        label={status.label}
                                                        labelPosition="left"
                                                        name="deliveryStatus"
                                                        checked={Number(callOffForm.deliveryStatus) === status.value}
                                                        onChange={() => updateFormField('deliveryStatus', status.value)}
                                                        className="flex w-full items-center justify-between gap-3 text-xs text-gray-700"
                                                    />
                                                ))}
                                            </fieldset>
                                        </section>

                                        <section aria-label="Summary">
                                            <div className="pb-2 border-b border-gray-300">
                                                <span className="text-xs font-semibold tracking-[0.08em] text-gray-500 uppercase">Leveransplatser</span>
                                            </div>
                                            <div className="mt-3 space-y-1 text-xs text-gray-800">
                                                {selectedDeliveryAddressSummary ? (
                                                    <>
                                                        <div>{selectedDeliveryAddressSummary.customerName}</div>
                                                        <div>{selectedDeliveryAddressSummary.name}</div>
                                                        <div>{selectedDeliveryAddressSummary.street}</div>
                                                        <div>{selectedDeliveryAddressSummary.postalCity}</div>
                                                    </>
                                                ) : (
                                                    <div className="text-gray-400">Ingen leveransadress vald.</div>
                                                )}
                                            </div>

                                            <div className="mt-6 pb-2 border-b border-gray-300">
                                                <span className="text-xs font-semibold tracking-[0.08em] text-gray-500 uppercase">Palltyper</span>
                                            </div>
                                            <div className="mt-3 space-y-2 text-xs text-gray-800">
                                                {palletFormatSummary.length === 0 ? (
                                                    <div className="text-gray-400">Inga pallar.</div>
                                                ) : palletFormatSummary.map((item) => (
                                                    <div key={`pallet-format-${item.name || 'okand'}`} className="flex items-center justify-between gap-4">
                                                        <span>{item.name || '-'}</span>
                                                        <span>{item.count}</span>
                                                    </div>
                                                ))}
                                            </div>
                                        </section>

                                    </div>

                                    <section aria-label="Leveranser" className="pt-10">
                                        <div className="w-full pb-0">
                                            <div className="flex items-center gap-24">
                                                <span className="text-center text-xs font-semibold tracking-[0.08em] text-gray-500 uppercase">Avropsleveranser</span>
                                                <ActionButton
                                                    label="Lägg till leverans från lager"
                                                    icon={CirclePlus}
                                                    accent="indigo"
                                                    onClick={handleAddDelivery}
                                                />
                                            </div>
                                        </div>

                                        <div className="mt-3 overflow-hidden">
                                            <table className="w-full text-xs text-gray-700">
                                                <thead className="text-tiny tracking-[0.08em] text-gray-500 border-b border-gray-300">
                                                    <tr>
                                                        <th className="px-2 py-1 text-left font-medium">Ordernr</th>
                                                        <th className="px-2 py-1 text-left font-medium">Kund</th>
                                                        <th className="px-2 py-1 text-left font-medium">Notering</th>
                                                        <th className="px-2 py-1 text-left font-medium">Palltyp</th>
                                                        <th className="px-2 py-1 text-left font-medium">Lager</th>
                                                        <th className="px-2 py-1 text-right font-medium">Ppl.lev.</th>
                                                        <th className="px-2 py-1 text-right font-medium">Ppl.egen</th>
                                                        <th className="px-2 py-1 text-right font-medium">Visa</th>
                                                        <th className="px-2 py-1 text-right font-medium">Ta bort</th>
                                                    </tr>
                                                </thead>
                                                <tbody>
                                                    {callOffDeliveryList.length === 0 ? (
                                                        <tr>
                                                            <td colSpan={9} className="px-2 py-3 text-center text-gray-400">Inga leveranser tillagda.</td>
                                                        </tr>
                                                    ) : callOffDeliveryList.map((delivery) => (
                                                        <tr key={`calloff-delivery-row-${delivery.rowKey}`} className="border-b border-gray-200">
                                                            <td className="px-2 pt-1.5 pb-1">{delivery.customerOrderNr}</td>
                                                            <td className="px-2 pt-1.5 pb-1">
                                                                <div>{delivery.customerName}</div>
                                                                <div className="text-gray-500 mt-1">{delivery.productName}</div>
                                                            </td>
                                                            <td className="p-0 h-full">
                                                                <textarea
                                                                    value={delivery.note ?? ''}
                                                                    onChange={(event) => updateDeliveryField(delivery.rowKey, 'note', event.target.value)}
                                                                    className="block h-full w-full resize-none border-0 bg-white px-2 py-1 text-xs text-gray-700 focus:outline-none focus:ring-1 focus:ring-inset focus:ring-blue-500"
                                                                    placeholder="Notering"
                                                                />
                                                            </td>
                                                            <td className="px-2 pt-1.5 pb-1">
                                                                <div>{delivery.palletFormatName}</div>
                                                                <div className="text-gray-500 mt-1">{delivery.kolliFormat}</div>
                                                            </td>
                                                            <td className="px-2 pt-1.5 pb-1">{delivery.inventoryName}</td>
                                                            <td className="px-2 pt-1.5 pb-1 text-right">{delivery.nrOfPallets}</td>
                                                            <td className="px-2 py-1 text-right">
                                                                <input
                                                                    type="text"
                                                                    value={delivery.nrOfPalletPlaces ?? ''}
                                                                    onChange={(event) => updateDeliveryField(delivery.rowKey, 'nrOfPalletPlaces', event.target.value)}
                                                                    className="w-16 border border-gray-300 bg-white px-2 py-0.5 text-right text-xs text-gray-700 focus:outline-none focus:ring-1 focus:ring-blue-500"
                                                                />
                                                            </td>
                                                            <td className="px-2 pt-1.5 pb-1 text-right">
                                                                {/* View mode: shows the details of an already created delivery from stock, read-only. */}
                                                                <button
                                                                    type="button"
                                                                    className={delivery.deliveryFromStockId ? 'text-xs text-blue-600 hover:underline' : 'text-xs text-gray-400 cursor-not-allowed'}
                                                                    disabled={!delivery.deliveryFromStockId || isLoadingViewDelivery}
                                                                    onClick={() => handleViewDelivery(delivery)}
                                                                >
                                                                    Visa
                                                                </button>
                                                            </td>
                                                            <td className="px-2 pt-1.5 pb-1 text-right">
                                                                <button
                                                                    type="button"
                                                                    className="text-xs text-red-600 hover:underline"
                                                                    onClick={() => removeCallOffDelivery(delivery.rowKey)}
                                                                >
                                                                    Ta bort
                                                                </button>
                                                            </td>
                                                        </tr>
                                                    ))}
                                                </tbody>
                                            </table>
                                        </div>
                                    </section>
                                </section>


                                <section aria-label="Höger" className="mt-5 pl-10 lg:sticky lg:top-[calc(52px+72px+1rem)] lg:z-10 lg:self-start">
                                    <div className="mb-3 h-[550px] w-full overflow-hidden border border-gray-300 bg-slate-50 relative">
                                        <div ref={mapContainerRef} className="h-full w-full" />
                                        {mapError ? (
                                            <div className="absolute inset-0 flex items-center justify-center bg-slate-50/90 px-4 text-center text-xs text-red-600">
                                                {mapError}
                                            </div>
                                        ) : null}
                                    </div>

                                    <div className="mt-5 overflow-hidden">
                                        <table className="w-full text-xs text-gray-700">
                                            <thead className="text-tiny tracking-[0.08em] text-gray-500">
                                                <tr>
                                                    <th className="px-2 py-1 text-left font-medium">Typ</th>
                                                    <th className="px-2 py-1 text-left font-medium">Ort från</th>
                                                    <th className="px-2 py-1 text-left font-medium">Ort till</th>
                                                    <th className="px-2 py-1 text-right font-medium">Sträcka km</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                {deliveryLegData.deliveryLegGroupedList.length === 0 ? (
                                                    <tr>
                                                        <td colSpan={4} className="px-2 py-3 text-center text-gray-400">Inga transportben.</td>
                                                    </tr>
                                                ) : deliveryLegData.deliveryLegGroupedList.map((leg, index) => (
                                                    <tr key={`main-leg-${leg.id}-${index}`} className="border-t border-gray-200">
                                                        <td className="px-2 pt-1.5 pb-1">{leg.typeOfTransport}</td>
                                                        <td className="px-2 pt-1.5 pb-1">{leg.helppropLegFromPositionPostalAddress || leg.helppropLegFromPositionName}</td>
                                                        <td className="px-2 pt-1.5 pb-1">{leg.helppropLegToPositionPostalAddress || leg.helppropLegToPositionName}</td>
                                                        <td className="px-2 pt-1.5 pb-1 text-right">{formatDistance(leg.distanceKm)}</td>
                                                    </tr>
                                                ))}
                                            </tbody>
                                        </table>
                                    </div>

                                    {deliveryLegData.distributionDeliveryLegGroupedList.length > 0 ? (
                                        <div className="mt-1 overflow-hidden border border-gray-300 border-t-0">
                                            <table className="w-full text-[11px] text-gray-700">
                                                <tbody>
                                                    {deliveryLegData.distributionDeliveryLegGroupedList.map((leg, index) => (
                                                        <tr key={`distribution-leg-${leg.id}-${index}`} className="border-t border-gray-200 first:border-t-0">
                                                            <td className="px-2 py-1 w-[70px]">{leg.typeOfTransport}</td>
                                                            <td className="px-2 py-1 w-[115px]">{leg.helppropFromPositionPostalAddress || leg.helppropFromPositionName}</td>
                                                            <td className="px-2 py-1 w-[115px]">{leg.helppropToPositionPostalAddress || leg.helppropToPositionName}</td>
                                                            <td className="px-2 py-1 text-right">{formatDistance(leg.distanceKm)}</td>
                                                        </tr>
                                                    ))}
                                                </tbody>
                                            </table>
                                        </div>
                                    ) : null}
                                </section>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default Calloff;
