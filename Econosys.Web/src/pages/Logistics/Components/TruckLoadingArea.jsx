import React, { forwardRef, useImperativeHandle, useRef, useState } from 'react';
import ConfirmationModal from '../../../components/ConfirmationModal';
import { getDeliveryPalletFootprint, getDeliveryPalletLabel, getDeliveryPalletPlaces } from '../../../helpers/deliveryEditing';
import { getAutoPlacement } from '../../../helpers/truckLoadingLayout';

export const TRUCK_AREA_WIDTH_CM = 240;
export const TRUCK_AREA_LENGTH_CM = 1350;
export const PALLET_WIDTH_CM = 80;
export const PALLET_LENGTH_CM = 120;

const TRUCK_AREA_SCALE = 0.5;
const TRUCK_AREA_BASE_WIDTH_PX = 300;
const TRUCK_AREA_WIDTH_PX = Math.round(TRUCK_AREA_BASE_WIDTH_PX * TRUCK_AREA_SCALE);
const TRUCK_AREA_HEIGHT_PX = Math.round((TRUCK_AREA_WIDTH_PX * TRUCK_AREA_LENGTH_CM) / TRUCK_AREA_WIDTH_CM);
const TRUCK_GRID_COLUMNS = 6;
const TRUCK_GRID_ROWS = 34;

const clamp = (value, min, max) => Math.min(Math.max(value, min), max);

const createTruckPallet = (source, xCm, yCm) => ({
    id: 0,
    clientId: `tmp-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    deliveryId: source.deliveryId,
    palletInfo: source.palletInfo,
    background: source.background,
    borderBrush: source.borderBrush,
    xCm,
    yCm,
    widthCm: source.widthCm,
    heightCm: source.heightCm,
    rotation: 0,
});

// Placement is driven by raw pointer events instead of native HTML5 drag-and-drop, since
// browser zoom makes dataTransfer/clientX drop coordinates unreliable across browsers.
const TruckLoadingArea = forwardRef(function TruckLoadingArea(
    { pallets, deliveries, onPalletsChange, onDraggingDeliveryIdChange },
    ref,
) {
    const [dragGhost, setDragGhost] = useState(null);
    const [pendingAutoPlace, setPendingAutoPlace] = useState(null);
    const [autoPlaceMessage, setAutoPlaceMessage] = useState('');
    const truckAreaRef = useRef(null);
    const dragSessionRef = useRef(null);
    const deliveryById = new Map(deliveries.map((delivery) => [delivery.id, delivery]));
    const palletCounts = new Map();
    const palletLabels = pallets.map((pallet) => {
        const index = palletCounts.get(pallet.deliveryId) ?? 0;
        palletCounts.set(pallet.deliveryId, index + 1);
        return getDeliveryPalletLabel(deliveryById.get(pallet.deliveryId), index);
    });

    const getTruckAreaPixelSize = () => {
        const areaElement = truckAreaRef.current;
        if (!areaElement) {
            return { widthPx: TRUCK_AREA_WIDTH_PX, heightPx: TRUCK_AREA_HEIGHT_PX };
        }

        return {
            widthPx: areaElement.clientWidth || TRUCK_AREA_WIDTH_PX,
            heightPx: areaElement.clientHeight || TRUCK_AREA_HEIGHT_PX,
        };
    };

    const clientToTruckAreaCm = (clientX, clientY) => {
        const areaElement = truckAreaRef.current;
        if (!areaElement) {
            return null;
        }

        const rect = areaElement.getBoundingClientRect();
        const contentLeft = rect.left + areaElement.clientLeft;
        const contentTop = rect.top + areaElement.clientTop;
        const pxPerCmX = areaElement.clientWidth / TRUCK_AREA_WIDTH_CM;
        const pxPerCmY = areaElement.clientHeight / TRUCK_AREA_LENGTH_CM;

        if (!Number.isFinite(pxPerCmX) || !Number.isFinite(pxPerCmY) || pxPerCmX <= 0 || pxPerCmY <= 0) {
            return null;
        }

        return {
            xCm: (clientX - contentLeft) / pxPerCmX,
            yCm: (clientY - contentTop) / pxPerCmY,
        };
    };

    const handleDragPointerMove = (event) => {
        const session = dragSessionRef.current;
        if (!session) {
            return;
        }

        if (session.kind === 'placed') {
            const point = clientToTruckAreaCm(event.clientX, event.clientY);
            if (!point) {
                return;
            }

            const xCm = clamp(point.xCm - session.offsetXCm, 0, TRUCK_AREA_WIDTH_CM - session.widthCm);
            const yCm = clamp(point.yCm - session.offsetYCm, 0, TRUCK_AREA_LENGTH_CM - session.heightCm);

            onPalletsChange((prev) => prev.map((item) => (
                item.clientId === session.palletClientId ? { ...item, xCm, yCm } : item
            )));
            return;
        }

        setDragGhost((prev) => (prev ? { ...prev, clientX: event.clientX, clientY: event.clientY } : prev));
    };

    const endDragSession = () => {
        window.removeEventListener('pointermove', handleDragPointerMove);
        window.removeEventListener('pointerup', handleDragPointerUp);
        window.removeEventListener('pointercancel', handleDragPointerUp);
        dragSessionRef.current = null;
        setDragGhost(null);
        onDraggingDeliveryIdChange?.(null);
    };

    const handleDragPointerUp = (event) => {
        const session = dragSessionRef.current;

        if (session?.kind === 'source') {
            const point = clientToTruckAreaCm(event.clientX, event.clientY);
            if (point) {
                const xCm = clamp(point.xCm - session.widthCm / 2, 0, TRUCK_AREA_WIDTH_CM - session.widthCm);
                const yCm = clamp(point.yCm - session.heightCm / 2, 0, TRUCK_AREA_LENGTH_CM - session.heightCm);

                const nextPallet = createTruckPallet(session, xCm, yCm);
                const isFirstForDelivery = !pallets.some((pallet) => pallet.deliveryId === session.deliveryId);

                if (isFirstForDelivery && session.palletPlaces > 0) {
                    setPendingAutoPlace({ session, pallet: nextPallet });
                } else {
                    onPalletsChange((prev) => [...prev, nextPallet]);
                }
            }
        }

        endDragSession();
    };

    const handleAutoPlaceConfirm = () => {
        const { session } = pendingAutoPlace;
        setPendingAutoPlace(null);
        const { positions, ...footprint } = getAutoPlacement(
            pallets,
            { widthCm: session.widthCm, heightCm: session.heightCm },
            session.palletPlaces,
            { areaWidthCm: TRUCK_AREA_WIDTH_CM, areaLengthCm: TRUCK_AREA_LENGTH_CM },
        );
        const newPallets = positions.map(({ xCm, yCm }) => ({ ...createTruckPallet(session, xCm, yCm), ...footprint }));
        onPalletsChange((prev) => [...prev, ...newPallets]);
        setAutoPlaceMessage(positions.length < session.palletPlaces ? 'Alla pallar fick ej plats.' : '');
    };

    const handleAutoPlaceDecline = () => {
        const { pallet } = pendingAutoPlace;
        setPendingAutoPlace(null);
        onPalletsChange((prev) => [...prev, pallet]);
    };

    const startDragSession = (session) => {
        dragSessionRef.current = session;
        window.addEventListener('pointermove', handleDragPointerMove);
        window.addEventListener('pointerup', handleDragPointerUp);
        window.addEventListener('pointercancel', handleDragPointerUp);
    };

    const startSourceDrag = (event, delivery) => {
        if (event.button !== 0) {
            return;
        }

        event.preventDefault();

        const deliveryId = Number(delivery?.id ?? 0);
        const palletInfo = String(delivery?.palletInfo ?? '');
        const background = String(delivery?.background ?? '#2F4E7388');
        const borderBrush = String(delivery?.borderBrush ?? '#2F4E73');
        const { widthCm, heightCm } = getDeliveryPalletFootprint(delivery);
        if (widthCm > TRUCK_AREA_WIDTH_CM || heightCm > TRUCK_AREA_LENGTH_CM) return;

        setAutoPlaceMessage('');
        startDragSession({
            kind: 'source',
            deliveryId,
            palletInfo,
            background,
            borderBrush,
            widthCm,
            heightCm,
            palletPlaces: getDeliveryPalletPlaces(delivery),
        });

        setDragGhost({
            clientX: event.clientX,
            clientY: event.clientY,
            widthCm,
            heightCm,
            palletInfo,
            label: getDeliveryPalletLabel(delivery, pallets.filter((pallet) => pallet.deliveryId === deliveryId).length),
            background,
            borderBrush,
            deliveryId,
        });

        onDraggingDeliveryIdChange?.(deliveryId);
    };

    useImperativeHandle(ref, () => ({ startSourceDrag }));

    const handlePlacedPalletPointerDown = (event, palletId) => {
        if (event.button !== 0 || event.target.closest('button')) {
            return;
        }

        event.preventDefault();

        const pallet = pallets.find((item) => item.clientId === palletId);
        if (!pallet) {
            return;
        }

        const point = clientToTruckAreaCm(event.clientX, event.clientY);
        const offsetXCm = point ? point.xCm - pallet.xCm : pallet.widthCm / 2;
        const offsetYCm = point ? point.yCm - pallet.yCm : pallet.heightCm / 2;

        startDragSession({
            kind: 'placed',
            palletClientId: palletId,
            offsetXCm,
            offsetYCm,
            widthCm: pallet.widthCm,
            heightCm: pallet.heightCm,
        });
    };

    const handleRemoveTruckPallet = (clientId) => {
        onPalletsChange((prev) => prev.filter((item) => item.clientId !== clientId));
    };

    const handleRotateTruckPallet = (clientId) => {
        onPalletsChange((prev) => prev.map((item) => {
            if (item.clientId !== clientId) {
                return item;
            }

            const nextWidthCm = item.heightCm;
            const nextHeightCm = item.widthCm;

            return {
                ...item,
                widthCm: nextWidthCm,
                heightCm: nextHeightCm,
                xCm: clamp(item.xCm, 0, TRUCK_AREA_WIDTH_CM - nextWidthCm),
                yCm: clamp(item.yCm, 0, TRUCK_AREA_LENGTH_CM - nextHeightCm),
                rotation: item.rotation === 90 ? 0 : 90,
            };
        }));
    };

    return (
        <>
            <div className="bg-slate-100/40" style={{ fontFamily: "'Neue Haas Unica', 'Helvetica Neue', Arial, sans-serif" }}>
                <div
                    ref={truckAreaRef}
                    className="relative mx-auto"
                    style={{
                        width: `${TRUCK_AREA_WIDTH_PX}px`,
                        height: `${TRUCK_AREA_HEIGHT_PX}px`,
                        backgroundColor: '#f8fafc',
                        backgroundImage: 'linear-gradient(to right, rgba(148,163,184,0.35) 1px, transparent 1px), linear-gradient(to bottom, rgba(148,163,184,0.35) 1px, transparent 1px)',
                        backgroundSize: `${TRUCK_AREA_WIDTH_PX / TRUCK_GRID_COLUMNS}px ${TRUCK_AREA_HEIGHT_PX / TRUCK_GRID_ROWS}px`,
                        border: '1px solid #94a3b8',
                    }}
                >
                    {pallets.map((pallet, index) => (
                        <div
                            key={pallet.clientId}
                            onPointerDown={(event) => handlePlacedPalletPointerDown(event, pallet.clientId)}
                            onDoubleClick={() => handleRotateTruckPallet(pallet.clientId)}
                            className="absolute border text-center text-tiny font-normal text-gray-900 cursor-grab active:cursor-grabbing select-none transition hover:z-10 hover:brightness-80 hover:shadow-md hover:ring-0 hover:ring-sky-500"
                            style={{
                                width: `${(pallet.widthCm / TRUCK_AREA_WIDTH_CM) * 100}%`,
                                height: `${(pallet.heightCm / TRUCK_AREA_LENGTH_CM) * 100}%`,
                                left: `${(pallet.xCm / TRUCK_AREA_WIDTH_CM) * 100}%`,
                                top: `${(pallet.yCm / TRUCK_AREA_LENGTH_CM) * 100}%`,
                                backgroundColor: pallet.background,
                                borderColor: pallet.borderBrush,
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                touchAction: 'none',
                            }}
                            title="Dra för att flytta pallen"
                        >
                            <button
                                type="button"
                                className="absolute right-0 top-0 h-4 w-4 bg-red-700 text-[8px] font-bold text-white"
                                onClick={(event) => {
                                    event.stopPropagation();
                                    handleRemoveTruckPallet(pallet.clientId);
                                }}
                                title="Ta bort pall"
                            >
                                ✕
                            </button>
                            <span className="pointer-events-none whitespace-pre-line leading-4">
                                {palletLabels[index]}
                            </span>
                        </div>
                    ))}
                </div>
                {autoPlaceMessage ? (
                    <p role="status" className="mt-1 text-center text-xs text-amber-700">{autoPlaceMessage}</p>
                ) : null}
            </div>
            <ConfirmationModal
                isOpen={Boolean(pendingAutoPlace)}
                onClose={handleAutoPlaceDecline}
                onConfirm={handleAutoPlaceConfirm}
                title="PLACERA PALLAR"
                message="Vill du placera ut alla pallar automatiskt?"
                confirmText="Ja"
                cancelText="Nej"
            />
            {dragGhost ? (
                <div
                    className="fixed z-50 flex items-center justify-center border text-center text-[11px] font-semibold text-gray-900 opacity-85"
                    style={{
                        left: `${dragGhost.clientX - (dragGhost.widthCm / TRUCK_AREA_WIDTH_CM) * getTruckAreaPixelSize().widthPx / 2}px`,
                        top: `${dragGhost.clientY - (dragGhost.heightCm / TRUCK_AREA_LENGTH_CM) * getTruckAreaPixelSize().heightPx / 2}px`,
                        width: `${(dragGhost.widthCm / TRUCK_AREA_WIDTH_CM) * getTruckAreaPixelSize().widthPx}px`,
                        height: `${(dragGhost.heightCm / TRUCK_AREA_LENGTH_CM) * getTruckAreaPixelSize().heightPx}px`,
                        backgroundColor: dragGhost.background,
                        borderColor: dragGhost.borderBrush,
                        pointerEvents: 'none',
                    }}
                >
                    <span className="whitespace-pre-line leading-4">{dragGhost.label}</span>
                </div>
            ) : null}
        </>
    );
});

export default TruckLoadingArea;
