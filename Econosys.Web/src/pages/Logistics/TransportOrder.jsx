import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { NavLink, useBlocker, useNavigate, useParams } from 'react-router-dom';
import Skeleton from 'react-loading-skeleton';
import 'react-loading-skeleton/dist/skeleton.css';
import { ArrowLeft, CirclePlus, PanelLeftClose, PanelLeftOpen, Printer, RefreshCw, Save, Trash2 } from 'lucide-react';

import { usePdf } from '../../contexts/PdfContext';
import ActionButton from '../../components/ActionButton';
import ConfirmationModal from '../../components/ConfirmationModal';
import LabeledCheckbox from '../../components/LabeledCheckbox';
import LabeledDatePicker from '../../components/LabeledDatePicker';
import LabeledInput from '../../components/LabeledInput';
import LabeledReactSelect from '../../components/LabeledReactSelect';
import LabeledTextArea from '../../components/LabeledTextArea';
import SegmentedFilter from '../../components/SegmentedFilter';
import apiClient from '../../config/apiClient';
import { getSharedRequest } from '../../helpers/sharedRequest';
import { toSwedishDateInputValue } from '../../helpers/dateUtils';
import TruckLoadingArea, {
    PALLET_LENGTH_CM,
    PALLET_WIDTH_CM,
    TRUCK_AREA_LENGTH_CM,
    TRUCK_AREA_WIDTH_CM,
} from './Components/TruckLoadingArea';
import TransportOrderCostCalcArea from './Components/TransportOrderCostCalcArea';

const defaultTransportOrderForm = {
    id: null,
    transportOrderNr: '',
    shipperId: '',
    senderReference: '',
    note: '',
    dateCreated: '',
    dateLoading: '',
    dateDelivery: '',
    deliveryStatus: 0,
    createdByUserId: null,
    createdByUserName: '',
    createdTimeStamp: null,
    editedByUserId: null,
    editedByUserName: '',
    editedTimeStamp: null,
};

const defaultDeliveryLegState = {
    deliveryLegGroupedList: [],
    distributionDeliveryLegGroupedList: [],
    distributionLegList: [],
};

const defaultDeliveryOverviewState = {
    supplierFactoryList: [],
    transportOrderDeliveryList: [],
};

const COMPANY_SETTINGS_ID = 1;
const GOOGLE_MAPS_SCRIPT_ID = 'google-maps-js-api';
let googleMapsLoaderPromise = null;

const clamp = (value, min, max) => Math.min(Math.max(value, min), max);

const mapDtoToForm = (dto) => ({
    id: dto?.id ?? null,
    transportOrderNr: dto?.transportOrderNr == null ? '' : String(dto.transportOrderNr),
    shipperId: dto?.shipperId == null ? '' : String(dto.shipperId),
    senderReference: String(dto?.senderReference ?? ''),
    note: String(dto?.note ?? ''),
    dateCreated: toSwedishDateInputValue(dto?.dateCreated),
    dateLoading: toSwedishDateInputValue(dto?.dateLoading),
    dateDelivery: toSwedishDateInputValue(dto?.dateDelivery),
    deliveryStatus: Number.isInteger(dto?.deliveryStatus) ? dto.deliveryStatus : 0,
    createdByUserId: dto?.createdByUserId ?? null,
    createdByUserName: String(dto?.createdByUserName ?? ''),
    createdTimeStamp: dto?.createdTimeStamp ?? null,
    editedByUserId: dto?.editedByUserId ?? null,
    editedByUserName: String(dto?.editedByUserName ?? ''),
    editedTimeStamp: dto?.editedTimeStamp ?? null,
});

const rightColumnViewOptions = [
    { value: 'ViewCostCalc', label: 'Kostnadskalkyl' },
    { value: 'ViewDeliveryLegs', label: 'Transportben' },
];

// Legacy parity: TransportOrderViewModel reverts DeliveryStatus locally when these server-side checks fail.
const DELIVERY_STATUS_REVERT_MESSAGES = [
    'Leveransstatus kan inte hoppa över flera steg i taget.',
    'Det finns ej attesterade transportfakturor, åtgärda detta först.',
    "På efterkalkylen ska det finnas en version av status 'Planerad' och en version av status 'Återrapporterad'. Vv åtgärda och försök igen.",
];

const deliveryStatusOptions = [
    { value: 0, label: 'Planering' },
    { value: 1, label: 'Planerad / skickad till trp' },
    { value: 2, label: 'Är lossad' },
    { value: 3, label: 'Återrapporterad' },
];

const mapDeliveryLegDto = (dto) => ({
    id: Number(dto?.id ?? 0),
    sortOrder: Number(dto?.sortOrder ?? 0),
    typeOfTransport: String(dto?.typeOfTransport ?? ''),
    distanceKm: dto?.distanceKm == null ? null : Number(dto.distanceKm),
    latitudeStart: dto?.latitudeStart == null ? null : Number(dto.latitudeStart),
    longitudeStart: dto?.longitudeStart == null ? null : Number(dto.longitudeStart),
    latitudeEnd: dto?.latitudeEnd == null ? null : Number(dto.latitudeEnd),
    longitudeEnd: dto?.longitudeEnd == null ? null : Number(dto.longitudeEnd),
    helppropFromPositionName: String(dto?.helppropFromPositionName ?? ''),
    helppropFromPositionPostalAddress: String(dto?.helppropFromPositionPostalAddress ?? ''),
    helppropToPositionName: String(dto?.helppropToPositionName ?? ''),
    helppropToPositionPostalAddress: String(dto?.helppropToPositionPostalAddress ?? ''),
    helppropLegFromPositionPostalAddress: String(dto?.helppropLegFromPositionPostalAddress ?? ''),
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

const mapDeliveryOverviewResponse = (dto) => ({
    supplierFactoryList: Array.isArray(dto?.supplierFactoryList)
        ? dto.supplierFactoryList.map((item) => ({
            supplierName: String(item?.supplierName ?? ''),
            factoryName: String(item?.factoryName ?? ''),
        }))
        : [],
    transportOrderDeliveryList: Array.isArray(dto?.transportOrderDeliveryList)
        ? dto.transportOrderDeliveryList.map((item) => ({
            id: Number(item?.id ?? 0),
            parentDeliveryId: Number(item?.parentDeliveryId ?? 0) > 0 ? Number(item.parentDeliveryId) : null,
            transportOrderDelivery: {
                ...(item?.transportOrderDelivery ?? {}),
                id: Number(item?.transportOrderDelivery?.id ?? item?.id ?? 0),
                sortOrder: Number(item?.transportOrderDelivery?.sortOrder ?? item?.sortOrder ?? 0),
            },
            supplierOrderId: Number(item?.supplierOrderId ?? 0),
            supplierOrderNr: String(item?.supplierOrderNr ?? ''),
            sortOrder: Number(item?.sortOrder ?? 0),
            customerNameAddress: String(item?.customerNameAddress ?? ''),
            deliveryAddressFreeText: String(item?.deliveryAddressFreeText ?? ''),
            note: String(item?.note ?? ''),
            customerYourOrderNr: String(item?.customerYourOrderNr ?? ''),
            supplierOrderConfirmedDeliveryDate: String(item?.supplierOrderConfirmedDeliveryDate ?? ''),
            forcedOmlastCaption: String(item?.forcedOmlastCaption ?? 'Omlast / direkt'),
            forcedOmlastStatus: Number(item?.forcedOmlastStatus ?? 0),
            isSlattPallet: Boolean(item?.isSlattPallet),
            palletInfo: String(item?.palletInfo ?? ''),
            palletsLeftToPlace: String(item?.palletsLeftToPlace ?? '0 kvar'),
            background: String(item?.background ?? '#2F4E7388'),
            borderBrush: String(item?.borderBrush ?? '#2F4E73'),
            infoPopupVisible: Boolean(item?.infoPopupVisible),
            logisticsInfoInternal: String(item?.logisticsInfoInternal ?? ''),
        }))
        : [],
});

const mapTruckPlanResponse = (dto, deliveryById) => {
    const palletList = Array.isArray(dto?.palletList) ? dto.palletList : [];

    return palletList.map((item) => {
        const deliveryId = Number(item?.transportOrderDeliveryId ?? 0);
        const delivery = deliveryById.get(deliveryId);
        const widthCm = Math.max(1, Number(item?.width ?? PALLET_WIDTH_CM));
        const heightCm = Math.max(1, Number(item?.height ?? PALLET_LENGTH_CM));

        return {
            id: Number(item?.id ?? 0),
            clientId: `db-${Number(item?.id ?? 0)}-${deliveryId}`,
            deliveryId,
            palletInfo: String(delivery?.palletInfo ?? 'Pall'),
            background: String(delivery?.background ?? '#2F4E7388'),
            borderBrush: String(delivery?.borderBrush ?? '#2F4E73'),
            xCm: clamp(Number(item?.posCmX ?? 0), 0, TRUCK_AREA_WIDTH_CM - widthCm),
            yCm: clamp(Number(item?.posCmY ?? 0), 0, TRUCK_AREA_LENGTH_CM - heightCm),
            widthCm,
            heightCm,
            rotation: Number(item?.rotation ?? 0),
        };
    });
};

const mapAggregateResponse = (dto) => {
    const overviewData = mapDeliveryOverviewResponse(dto?.deliveryOverview);
    const deliveryById = new Map(
        overviewData.transportOrderDeliveryList.map((delivery) => [delivery.id, delivery]),
    );

    return {
        transportOrderForm: mapDtoToForm(dto?.transportOrder),
        deliveryLegData: mapDeliveryLegsResponse(dto?.deliveryLegs),
        deliveryOverviewData: overviewData,
        truckPlanPallets: mapTruckPlanResponse(dto?.truckPlan, deliveryById),
        version: String(dto?.version ?? ''),
    };
};

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

const isFiniteCoordinate = (value) => typeof value === 'number' && Number.isFinite(value);

const routePointsFromLegs = (legs) => {
    if (!Array.isArray(legs) || legs.length === 0) {
        return [];
    }

    const startPoints = legs
        .map((leg) => ({ lat: leg.latitudeStart, lng: leg.longitudeStart }))
        .filter((point) => isFiniteCoordinate(point.lat) && isFiniteCoordinate(point.lng));

    const lastLeg = legs[legs.length - 1];
    const endPoint = {
        lat: lastLeg?.latitudeEnd,
        lng: lastLeg?.longitudeEnd,
    };

    if (isFiniteCoordinate(endPoint.lat) && isFiniteCoordinate(endPoint.lng)) {
        startPoints.push(endPoint);
    }

    return startPoints.filter((point, index, list) => {
        if (index === 0) {
            return true;
        }

        const previous = list[index - 1];
        return previous.lat !== point.lat || previous.lng !== point.lng;
    });
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

const TransportOrder = () => {
    const navigate = useNavigate();
    const { id } = useParams();
    const { openPdfPreview, closePdfPreview } = usePdf();

    const [loading, setLoading] = useState(true);
    const [messages, setMessages] = useState([]);
    const [showUnsavedWarning, setShowUnsavedWarning] = useState(false);
    const [transportOrderForm, setTransportOrderForm] = useState(defaultTransportOrderForm);
    const [deliveryLegData, setDeliveryLegData] = useState(defaultDeliveryLegState);
    const [deliveryOverviewData, setDeliveryOverviewData] = useState(defaultDeliveryOverviewState);
    const [googleApiKey, setGoogleApiKey] = useState('');
    const [mapError, setMapError] = useState('');
    const [transporterOptions, setTransporterOptions] = useState([]);
    const [aggregateVersion, setAggregateVersion] = useState('');
    const [isSaving, setIsSaving] = useState(false);
    const [isStaleConflict, setIsStaleConflict] = useState(false);
    const [isInfoPanelExpanded, setIsInfoPanelExpanded] = useState(false);
    const [rightColumnView, setRightColumnView] = useState('ViewCostCalc');
    const [truckPlanPallets, setTruckPlanPallets] = useState([]);
    const [draggingSourceDeliveryId, setDraggingSourceDeliveryId] = useState(null);
    const [editingDeliveryNoteId, setEditingDeliveryNoteId] = useState(null);
    const [originalTransportOrderState, setOriginalTransportOrderState] = useState(null);
    const skipUnsavedCheckRef = useRef(false);
    const mapContainerRef = useRef(null);
    const truckLoadingAreaRef = useRef(null);
    const costCalcAreaRef = useRef(null);
    // Guards against duplicate/near-simultaneous save invocations; the isSaving *state* isn't reliable here since
    // two rapid calls can both read it as false before the first render commits.
    const isSavingRef = useRef(false);
    const mapRuntimeRef = useRef({ map: null, renderers: [] });
    const infoPanelAutoCloseRef = useRef(null);

    // Flash the info panel open briefly to show a save result; keep it open if there are errors/warnings.
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

    // Surfaces messages from the cost calc child component in this page's own messages area.
    const handleCostCalcMessage = useCallback((type, text) => {
        setMessages((prev) => [...prev, { type, text }]);
        flashInfoPanel(type === 'error' || type === 'warning');
    }, [flashInfoPanel]);

    const createSnapshot = useCallback(() => ({
        transportOrderForm,
        deliveryOverviewData,
        truckPlanPallets,
    }), [deliveryOverviewData, transportOrderForm, truckPlanPallets]);

    const hasUnsavedChanges = useCallback(() => {
        if (skipUnsavedCheckRef.current) return false;
        if (!originalTransportOrderState) return false;
        return JSON.stringify(createSnapshot()) !== JSON.stringify(originalTransportOrderState);
    }, [createSnapshot, originalTransportOrderState]);

    const blocker = useBlocker(hasUnsavedChanges);

    const applyAggregateResponse = (dto) => {
        const aggregate = mapAggregateResponse(dto);
        const nextSnapshot = {
            transportOrderForm: aggregate.transportOrderForm,
            deliveryOverviewData: aggregate.deliveryOverviewData,
            truckPlanPallets: aggregate.truckPlanPallets,
        };

        setTransportOrderForm(aggregate.transportOrderForm);
        setDeliveryLegData(aggregate.deliveryLegData);
        setDeliveryOverviewData(aggregate.deliveryOverviewData);
        setTruckPlanPallets(aggregate.truckPlanPallets);
        setAggregateVersion(aggregate.version);
        setOriginalTransportOrderState(nextSnapshot);
    };

    useEffect(() => {
        let isActive = true;

        const loadFormData = async () => {
            setLoading(true);
            setMessages([]);
            setIsStaleConflict(false);

            try {
                const [formOptionsResponse, aggregateResponse, companySettingsResponse] = await Promise.all([
                    getSharedRequest('transportorder:form-options', () => apiClient.get('/transportorder/form-options')),
                    getSharedRequest(`transportorder:${id}:aggregate`, () => apiClient.get(`/transportorder/${id}/aggregate`)),
                    getSharedRequest(`companysettings:${COMPANY_SETTINGS_ID}`, () => apiClient.get(`/companysettings/${COMPANY_SETTINGS_ID}`)),
                ]);

                if (!isActive) {
                    return;
                }

                const transporters = Array.isArray(formOptionsResponse?.data?.transporters)
                    ? formOptionsResponse.data.transporters
                    : [];

                setTransporterOptions(transporters);
                applyAggregateResponse(aggregateResponse?.data);
                setGoogleApiKey(String(companySettingsResponse?.data?.googleApiKey ?? ''));
            } catch (error) {
                console.error('Failed to load transport order form:', error);

                if (!isActive) {
                    return;
                }

                setMessages([{ type: 'error', text: 'Kunde inte hämta transportorder.' }]);
                setDeliveryLegData(defaultDeliveryLegState);
                setDeliveryOverviewData(defaultDeliveryOverviewState);
                setTruckPlanPallets([]);
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
    }, [id]);

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

    useEffect(() => {
        setTruckPlanPallets([]);
    }, [id]);

    const titleText = useMemo(() => {
        if (!transportOrderForm?.transportOrderNr) {
            return 'Transportorder';
        }

        return `Transportorder ${transportOrderForm.transportOrderNr}`;
    }, [transportOrderForm]);

    const transporterItems = useMemo(() => {
        return [{ id: '', name: '' }, ...transporterOptions];
    }, [transporterOptions]);

    useEffect(() => {
        let isActive = true;

        const renderLegacyMapRoutes = async () => {
            if (!mapContainerRef.current) {
                // Container is unmounted while another view is active; drop the stale map instance.
                mapRuntimeRef.current = { map: null, renderers: [] };
                return;
            }

            setMapError('');

            const mainRouteRequest = deliveryLegData.deliveryLegGroupedList.length >= 2
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
                console.error('Failed to render Google transport map:', error);
                if (isActive) {
                    setMapError('Kunde inte ladda Google-karta eller rita transportben.');
                }
            }
        };

        renderLegacyMapRoutes();

        return () => {
            isActive = false;
        };
    }, [deliveryLegData, googleApiKey, rightColumnView]);

    const updateFormField = (field, value) => {
        setTransportOrderForm((prev) => ({
            ...prev,
            [field]: value,
        }));
    };

    const getDeliveryGroupKey = (delivery) => {
        const parentId = Number(delivery?.parentDeliveryId ?? 0);
        return parentId > 0 ? parentId : Number(delivery?.id ?? 0);
    };

    const updateDeliveryField = (deliveryId, field, value) => {
        const normalizedDeliveryId = Number(deliveryId);
        const groupKey = getDeliveryGroupKey(
            deliveryOverviewData.transportOrderDeliveryList.find((delivery) => Number(delivery.id) === normalizedDeliveryId)
        );

        setDeliveryOverviewData((prev) => ({
            ...prev,
            transportOrderDeliveryList: prev.transportOrderDeliveryList.map((delivery) => {
                const isGroupedDelivery = getDeliveryGroupKey(delivery) === groupKey;
                return isGroupedDelivery ? { ...delivery, [field]: value, transportOrderDelivery: { ...delivery.transportOrderDelivery, [field]: value } } : delivery;
            }),
        }));
    };

    const removeTransportOrderDelivery = (deliveryId) => {
        const normalizedDeliveryId = Number(deliveryId);
        setDeliveryOverviewData((prev) => ({
            ...prev,
            transportOrderDeliveryList: prev.transportOrderDeliveryList.filter((delivery) => delivery.id !== normalizedDeliveryId),
        }));
        setTruckPlanPallets((prev) => prev.filter((pallet) => pallet.deliveryId !== normalizedDeliveryId));
    };

    const handleBackClick = () => {
        if (window.history.length > 1) {
            navigate(-1);
            return;
        }

        navigate('/logistics/transportorderoverivew');
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

    // deliveryStatusOverride lets the delivery-status radio trigger an immediate save without waiting for the async form-field update to land in state.
    const saveTransportOrder = async (deliveryStatusOverride) => {
        if (isSavingRef.current || isStaleConflict || !aggregateVersion) {
            return;
        }
        isSavingRef.current = true;

        const effectiveDeliveryStatus = deliveryStatusOverride ?? Number(transportOrderForm.deliveryStatus);

        const deliveryGroups = new Map();
        deliveryOverviewData.transportOrderDeliveryList.forEach((delivery) => {
            const groupKey = getDeliveryGroupKey(delivery);
            if (!deliveryGroups.has(groupKey)) {
                deliveryGroups.set(groupKey, delivery);
            }
        });

        const payload = {
            version: aggregateVersion,
            senderReference: transportOrderForm.senderReference || null,
            note: transportOrderForm.note || null,
            dateCreated: transportOrderForm.dateCreated || null,
            dateLoading: transportOrderForm.dateLoading || null,
            dateDelivery: transportOrderForm.dateDelivery || null,
            deliveryStatus: effectiveDeliveryStatus,
            shipperId: transportOrderForm.shipperId === '' ? null : Number(transportOrderForm.shipperId),
            transportOrderDeliveryList: deliveryOverviewData.transportOrderDeliveryList.map((delivery) => {
                const groupPrimary = deliveryGroups.get(getDeliveryGroupKey(delivery)) ?? delivery;
                return {
                    ...delivery.transportOrderDelivery,
                    id: Number(delivery.id),
                    sortOrder: Number(groupPrimary.sortOrder ?? delivery.sortOrder ?? 0),
                    note: String(groupPrimary.note ?? delivery.note ?? ''),
                    customerYourOrderNr: String(groupPrimary.customerYourOrderNr ?? delivery.customerYourOrderNr ?? ''),
                };
            }),
            palletList: truckPlanPallets
                .filter((item) => item.deliveryId > 0)
                .map((item) => ({
                    id: Number(item.id ?? 0),
                    transportOrderDeliveryId: Number(item.deliveryId ?? 0),
                    posCmX: Math.round(item.xCm),
                    posCmY: Math.round(item.yCm),
                    rotation: Number(item.rotation ?? 0),
                    width: Math.round(item.widthCm),
                    height: Math.round(item.heightCm),
                })),
        };

        setIsSaving(true);
        setMessages([]);
        // Server recalculates/rebases the cost calc on a status change, so the pre-change draft must not be pushed back over it.
        const deliveryStatusChanged = effectiveDeliveryStatus !== Number(originalTransportOrderState?.transportOrderForm.deliveryStatus);
        try {
            const response = await apiClient.put(`/transportorder/${id}/aggregate`, payload);
            applyAggregateResponse(response?.data);
            setIsStaleConflict(false);

            if (deliveryStatusChanged) {
                await costCalcAreaRef.current?.reload();
                setMessages((prev) => [...prev, { type: 'info', text: 'Transportordern sparades.' }]);
                flashInfoPanel(false);
            } else {
                const costCalcSaved = await costCalcAreaRef.current?.save();
                if (costCalcSaved === false) {
                    flashInfoPanel(true);
                } else {
                    setMessages((prev) => [...prev, { type: 'info', text: 'Transportordern sparades.' }]);
                    flashInfoPanel(false);
                }
            }
        } catch (error) {
            console.error('Failed to save transport order:', error);
            if (error.response?.status === 409) {
                setIsStaleConflict(true);
                setMessages((prev) => [...prev, { type: 'error', text: 'Transportordern har ändrats av en annan användare.' }]);
            } else {
                const apiMessage = typeof error.response?.data === 'string' ? error.response.data : '';
                if (DELIVERY_STATUS_REVERT_MESSAGES.includes(apiMessage) && originalTransportOrderState) {
                    setTransportOrderForm((prev) => ({ ...prev, deliveryStatus: originalTransportOrderState.transportOrderForm.deliveryStatus }));
                }
                setMessages((prev) => [...prev, { type: 'error', text: apiMessage || 'Kunde inte spara transportordern.' }]);
            }
            flashInfoPanel(true);
        } finally {
            setIsSaving(false);
            isSavingRef.current = false;
        }
    };

    // Legacy parity: DeliveryStatus change triggers CreateFromData + SaveTransportOrder immediately, not on a separate Spara click.
    const handleDeliveryStatusChange = (value) => {
        if (Number(value) === Number(transportOrderForm.deliveryStatus)) {
            return;
        }
        updateFormField('deliveryStatus', value);
        saveTransportOrder(value);
    };

    const reloadLatestTransportOrder = async () => {
        try {
            const response = await apiClient.get(`/transportorder/${id}/aggregate`);
            applyAggregateResponse(response?.data);
            await costCalcAreaRef.current?.reload();
            setIsStaleConflict(false);
            setMessages((prev) => [...prev, { type: 'info', text: 'Den senaste versionen av transportordern lästes in.' }]);
        } catch (error) {
            console.error('Failed to reload transport order:', error);
            setMessages((prev) => [...prev, { type: 'error', text: 'Kunde inte läsa in den senaste versionen av transportordern.' }]);
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
                                            {transportOrderForm.createdTimeStamp
                                                ? new Date(transportOrderForm.createdTimeStamp).toLocaleString('sv-SE', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
                                                : '-'}
                                        </div>
                                        <div className="col-span-9 text-gray-500">
                                            {transportOrderForm.createdByUserName ? `av ${transportOrderForm.createdByUserName}` : '-'}
                                        </div>
                                    </div>
                                    <div className="grid grid-cols-24 gap-1">
                                        <div className="col-span-6"><span className="text-gray-500">Redigerad:</span></div>
                                        <div className="col-span-9">
                                            {transportOrderForm.editedTimeStamp
                                                ? new Date(transportOrderForm.editedTimeStamp).toLocaleString('sv-SE', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
                                                : '-'}
                                        </div>
                                        <div className="col-span-9 text-gray-500">
                                            {transportOrderForm.editedByUserName ? `av ${transportOrderForm.editedByUserName}` : '-'}
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
                            {/* border-b border-gray-300  */}
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
                                    onClick={() => saveTransportOrder()}
                                    disabled={isSaving || isStaleConflict || !aggregateVersion}
                                    accent="lime"
                                />
                                <div className="ml-20 flex items-center gap-2">
                                    <ActionButton
                                        label="Transportör"
                                        icon={Printer}
                                        onClick={handleOpenPdfPanel}
                                        accent="sky"
                                    />
                                    <ActionButton
                                        label="Levernatör"
                                        icon={Printer}
                                        onClick={handleOpenPdfPanel}
                                        accent="sky"
                                    />
                                    <ActionButton
                                        label="Lager"
                                        icon={Printer}
                                        onClick={handleOpenPdfPanel}
                                        accent="sky"
                                    />
                                </div>
                                <div className="ml-20 flex items-center gap-2">
                                    <ActionButton
                                        label="Delete"
                                        icon={Trash2}
                                        accent="rose"
                                    />

                                </div>
                            </div>
                            <div className="flex items-center justify-center">
                                <SegmentedFilter
                                    value={rightColumnView}
                                    onChange={setRightColumnView}
                                    options={rightColumnViewOptions}
                                    theme="sky"
                                />
                            </div>

                        </div>

                        {isStaleConflict ? (
                            <div className="mb-4 flex flex-wrap items-center gap-4 border border-amber-300 bg-amber-50 px-4 py-3 text-xs text-amber-900">
                                <span>Transportordern har ändrats av en annan användare. Ditt utkast har behållits men kan inte sparas ovanpå den nya versionen.</span>
                                <ActionButton
                                    label="Läs in senaste"
                                    icon={RefreshCw}
                                    onClick={reloadLatestTransportOrder}
                                    accent="yellow"
                                />
                            </div>
                        ) : null}

                        <div className="mt-5">
                            <div className="grid grid-cols-1 items-start gap-15 xl:grid-cols-[auto_360px_auto_1fr]">
                                {/* xl:min-h-[880px]  */}
                                <section aria-label="Övre vänster" className="w-90">
                                    <div className="">
                                        <LabeledReactSelect
                                            label="Speditör"
                                            labelWidth="w-20"
                                            name="shipperId"
                                            value={transportOrderForm.shipperId}
                                            items={transporterItems}
                                            onChange={(value) => updateFormField('shipperId', value)}
                                            margintop="0"
                                        />
                                        <LabeledInput
                                            label="Referens"
                                            labelWidth="w-20"
                                            name="senderReference"
                                            value={transportOrderForm.senderReference}
                                            onChange={(value) => updateFormField('senderReference', value)}
                                            margintop="0"
                                        />
                                        <LabeledTextArea
                                            label="Notering"
                                            labelWidth="w-20"
                                            height="h-16"
                                            name="note"
                                            value={transportOrderForm.note}
                                            onChange={(value) => updateFormField('note', value)}
                                            margintop="0"
                                        />
                                    </div>

                                    <div className="pt-5 grid grid-cols-[auto_1fr] gap-10 items-start">
                                        <div className="">
                                            <LabeledDatePicker
                                                label="Skapad"
                                                labelWidth="w-24"
                                                name="dateCreated"
                                                value={transportOrderForm.dateCreated}
                                                onChange={(value) => updateFormField('dateCreated', value)}
                                                valueType="input"
                                                margintop="0"
                                            />
                                            <LabeledDatePicker
                                                label="Lastningsdatum"
                                                labelWidth="w-24"
                                                name="dateLoading"
                                                value={transportOrderForm.dateLoading}
                                                onChange={(value) => updateFormField('dateLoading', value)}
                                                valueType="input"
                                                margintop="0"
                                            />
                                            <LabeledDatePicker
                                                label="Leveransdatum"
                                                labelWidth="w-24"
                                                name="dateDelivery"
                                                value={transportOrderForm.dateDelivery}
                                                onChange={(value) => updateFormField('dateDelivery', value)}
                                                valueType="input"
                                                margintop="0"
                                            />
                                        </div>

                                        {/* <fieldset className="" aria-label="Leveransstatus">
                                            {deliveryStatusOptions.map((status) => (
                                                <LabeledCheckbox
                                                    key={status.value}
                                                    inputType="radio"
                                                    label={status.label}
                                                    labelPosition="left"
                                                    name="deliveryStatus"
                                                    checked={Number(transportOrderForm.deliveryStatus) === status.value}
                                                    onChange={() => updateFormField('deliveryStatus', status.value)}
                                                    className="flex w-full items-center justify-between gap-3 text-xs text-gray-700"
                                                />
                                            ))}
                                        </fieldset> */}
                                    </div>

                                    <fieldset className="mt-5 w-50" aria-label="Leveransstatus">
                                        {deliveryStatusOptions.map((status) => (
                                            <LabeledCheckbox
                                                key={status.value}
                                                inputType="radio"
                                                label={status.label}
                                                labelPosition="left"
                                                name="deliveryStatus"
                                                checked={Number(transportOrderForm.deliveryStatus) === status.value}
                                                // Legacy parity: block skipping more than one status forward at a time.
                                                disabled={isSaving || status.value > Number(transportOrderForm.deliveryStatus) + 1}
                                                onChange={() => handleDeliveryStatusChange(status.value)}
                                                className="flex w-full items-center justify-between gap-3 text-xs text-gray-700"
                                            />
                                        ))}
                                    </fieldset>

                                </section>

                                <section aria-label="Övre mitten" className="pl-10">
                                    <div className="w-full border-b border-gray-300 pb-2">
                                        <div className="flex items-center justify-center gap-4">
                                            <span className="text-center text-xs font-semibold tracking-[0.08em] text-gray-500 uppercase">Leverantör och fabrik</span>
                                        </div>
                                        <div className="mt-5 space-y-1">
                                            {deliveryOverviewData.supplierFactoryList.length === 0 ? (
                                                <div className="text-center text-xs text-gray-400">Inga leverantörer.</div>
                                            ) : deliveryOverviewData.supplierFactoryList.map((row, index) => (
                                                <div key={`supplier-factory-${index}`} className="grid grid-cols-2 gap-4 text-center text-xs text-gray-800">
                                                    <div>{row.supplierName}</div>
                                                    <div>{row.factoryName}</div>
                                                </div>
                                            ))}
                                        </div>
                                    </div>

                                    {/* <div className="border-b border-gray-300 py-4">
                                        <div className="flex items-center justify-center gap-4">
                                            <ActionButton
                                                label="Leverans till lager"
                                                icon={CirclePlus}
                                                accent="indigo"
                                                onClick={() => setMessages((prev) => [...prev, { type: 'info', text: 'Lägg till leverans till lager implementeras i nästa steg.' }])}
                                            />
                                            <ActionButton
                                                label="Leverans till kund"
                                                icon={CirclePlus}
                                                accent="indigo"
                                                onClick={() => setMessages((prev) => [...prev, { type: 'info', text: 'Lägg till leverans till kund implementeras i nästa steg.' }])}
                                            />
                                        </div>
                                    </div> */}

                                    <div>
                                        {deliveryOverviewData.transportOrderDeliveryList
                                            .filter((delivery) => Number(delivery.parentDeliveryId ?? 0) <= 0)
                                            .map((delivery) => (
                                                <div key={`delivery-card-${delivery.id}`} className="border-b border-gray-300 py-2">
                                                    <div className="grid grid-cols-[1fr_auto_auto] gap-04 items-start">
                                                        <div>
                                                            {delivery.deliveryAddressFreeText ? (
                                                                <textarea
                                                                    className="w-full resize-none border border-gray-300 px-2 py-1 text-xs text-gray-700"
                                                                    value={delivery.deliveryAddressFreeText}
                                                                    readOnly
                                                                    rows={4}
                                                                />
                                                            ) : (
                                                                <div className="transport-order-delivery-address-display whitespace-pre-line text-xs text-gray-800">
                                                                    {delivery.customerNameAddress}
                                                                </div>
                                                            )}

                                                            {(delivery.note || delivery.customerYourOrderNr || editingDeliveryNoteId === delivery.id) && (
                                                                <div className="mt-1 space-y-[1px]">
                                                                    <input
                                                                        type="text"
                                                                        value={delivery.note ?? ''}
                                                                        onChange={(event) => updateDeliveryField(delivery.id, 'note', event.target.value)}
                                                                        className="w-full border border-gray-300 bg-white px-2 pt-0.5 pb-0.25 text-xs text-gray-700 focus:outline-none focus:ring-1 focus:ring-blue-500"
                                                                        placeholder="Notering"
                                                                    />
                                                                    <div className="flex items-center gap-2 text-xs text-gray-700">
                                                                        <span className="w-20 shrink-0">Kunds ordernr.</span>
                                                                        <input
                                                                            type="text"
                                                                            value={delivery.customerYourOrderNr ?? ''}
                                                                            onChange={(event) => updateDeliveryField(delivery.id, 'customerYourOrderNr', event.target.value)}
                                                                            className="w-full border border-gray-300 bg-white px-2 pt-0.5 pb-0.25 text-xs text-gray-700 focus:outline-none focus:ring-1 focus:ring-blue-500"
                                                                        />
                                                                    </div>
                                                                </div>
                                                            )}

                                                            <div className="mt-2 flex items-center gap-2">
                                                                {Number(delivery?.supplierOrderId ?? 0) > 0 ? (
                                                                    <NavLink
                                                                        to={`/order/supplierorders/${delivery.supplierOrderId}`}
                                                                        target="_blank"
                                                                        rel="noopener noreferrer"
                                                                        className="text-xs font-semibold text-blue-800 hover:text-blue-900 hover:underline"
                                                                    >
                                                                        {delivery.supplierOrderNr || '-'}
                                                                    </NavLink>
                                                                ) : (
                                                                    <span className="text-xs font-semibold text-gray-500">{delivery.supplierOrderNr || '-'}</span>
                                                                )}
                                                                <input
                                                                    className="w-10 py-0.5 border border-gray-300 text-center text-xs bg-white text-gray-800"
                                                                    value={Number(delivery.sortOrder ?? 0)}
                                                                    onChange={(event) => updateDeliveryField(delivery.id, 'sortOrder', Number(event.target.value || 0))}
                                                                />
                                                            </div>

                                                            <div className="mt-2 flex items-center gap-2 text-xs">
                                                                <span className="w-20 text-gray-700">Bekr. lev.datum</span>
                                                                <span className="text-red-600">{delivery.supplierOrderConfirmedDeliveryDate}</span>
                                                            </div>
                                                        </div>

                                                        <div className="pt-0 mr-5">
                                                            <button
                                                                type="button"
                                                                className="h-5 w-5 text-gray-600 hover:text-gray-800"
                                                                title="Redigera notering"
                                                                onClick={() => setEditingDeliveryNoteId((prev) => prev === delivery.id ? null : delivery.id)}
                                                            >
                                                                ✎
                                                            </button>
                                                            {delivery.infoPopupVisible ? (
                                                                <div className="mt-1 text-center text-tiny text-blue-700" title={delivery.logisticsInfoInternal}>
                                                                    info
                                                                </div>
                                                            ) : null}
                                                        </div>

                                                        <div className="flex flex-col items-end gap-1">
                                                            <button
                                                                type="button"
                                                                className="text-xs text-red-600 hover:underline"
                                                                onClick={() => removeTransportOrderDelivery(delivery.id)}
                                                            >
                                                                Ta bort
                                                            </button>
                                                            <button type="button" className="text-xs text-gray-800 hover:underline">Visa</button>
                                                            <button type="button" className="text-xs text-gray-800 hover:underline">{delivery.forcedOmlastCaption}</button>
                                                            <div className="mt-2 flex items-center gap-2">
                                                                <span className="text-xs text-gray-700">{delivery.palletsLeftToPlace}</span>
                                                                <div
                                                                    className={`min-w-[40px] border px-2 pt-[6px] pb-[5px] rounded-xs text-center text-xs font-semibold text-gray-800 cursor-grab active:cursor-grabbing ${draggingSourceDeliveryId === Number(delivery?.id ?? 0) ? 'opacity-0' : ''}`}
                                                                    onPointerDown={(event) => truckLoadingAreaRef.current?.startSourceDrag(event, delivery)}
                                                                    style={{
                                                                        backgroundColor: delivery.background,
                                                                        borderColor: delivery.borderBrush,
                                                                        touchAction: 'none',
                                                                    }}
                                                                    title="Dra till lastplaneringen"
                                                                >
                                                                    {delivery.palletInfo}
                                                                </div>
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>
                                            ))}
                                    </div>
                                </section>

                                <section aria-label="Övre 3dje" className="lg:sticky lg:top-[calc(52px+72px+1rem)] lg:z-10 lg:self-start">
                                    <TruckLoadingArea
                                        ref={truckLoadingAreaRef}
                                        pallets={truckPlanPallets}
                                        onPalletsChange={setTruckPlanPallets}
                                        onDraggingDeliveryIdChange={setDraggingSourceDeliveryId}
                                    />
                                </section>

                                <section aria-label="Övre fjärde" className="pl-10">

                                    {rightColumnView === 'ViewCostCalc' ? (
                                        <TransportOrderCostCalcArea ref={costCalcAreaRef} transportOrderId={id} onMessage={handleCostCalcMessage} />
                                    ) : (
                                        <>
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
                                        </>
                                    )}
                                </section>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default TransportOrder;
